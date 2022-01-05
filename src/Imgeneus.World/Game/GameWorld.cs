﻿using Imgeneus.Database;
using Imgeneus.Database.Entities;
using Imgeneus.Network.Packets.Game;
using Imgeneus.Network.Server;
using Imgeneus.World.Game.Blessing;
using Imgeneus.World.Game.Duel;
using Imgeneus.World.Game.Guild;
using Imgeneus.World.Game.PartyAndRaid;
using Imgeneus.World.Game.Player;
using Imgeneus.World.Game.Time;
using Imgeneus.World.Game.Trade;
using Imgeneus.World.Game.Zone;
using Imgeneus.World.Game.Zone.MapConfig;
using Imgeneus.World.Game.Zone.Portals;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Imgeneus.World.Game
{
    /// <summary>
    /// The virtual representation of game world.
    /// </summary>
    public class GameWorld : IGameWorld
    {
        private readonly ILogger<GameWorld> _logger;
        private readonly IMapsLoader _mapsLoader;
        private readonly IMapFactory _mapFactory;
        private readonly ICharacterFactory _characterFactory;
        private readonly ITimeService _timeService;
        private readonly IGuildRankingManager _guildRankingManager;
        private MapDefinitions _mapDefinitions;

        public GameWorld(ILogger<GameWorld> logger, IMapsLoader mapsLoader, IMapFactory mapFactory, ICharacterFactory characterFactory, ITimeService timeService, IGuildRankingManager guildRankingManager)
        {
            _logger = logger;
            _mapsLoader = mapsLoader;
            _mapFactory = mapFactory;
            _characterFactory = characterFactory;
            _timeService = timeService;
            _guildRankingManager = guildRankingManager;

            InitMaps();
            InitGRB();
        }

        #region Maps

        /// <inheritdoc/>
        public IList<ushort> AvailableMapIds { get; private set; } = new List<ushort>();

        /// <summary>
        /// Thread-safe dictionary of maps. Where key is map id.
        /// </summary>
        public ConcurrentDictionary<ushort, IMap> Maps { get; private set; } = new ConcurrentDictionary<ushort, IMap>();

        /// <summary>
        /// Initializes maps with startup values like mobs, npc, areas, obelisks etc.
        /// </summary>
        private void InitMaps()
        {
            _mapDefinitions = _mapsLoader.LoadMapDefinitions();
            foreach (var mapDefinition in _mapDefinitions.Maps)
            {
                var config = _mapsLoader.LoadMapConfiguration(mapDefinition.Id);

                if (mapDefinition.CreateType == CreateType.Default)
                {
                    config.Obelisks = _mapsLoader.GetObelisks(mapDefinition.Id);

                    var map = _mapFactory.CreateMap(mapDefinition.Id, mapDefinition, config);
                    if (Maps.TryAdd(mapDefinition.Id, map))
                        _logger.LogInformation($"Map {map.Id} was successfully loaded.");
                }

                AvailableMapIds.Add(mapDefinition.Id);
            }
        }

        /// <inheritdoc />
        public bool CanTeleport(Character player, ushort destinationMapId, out PortalTeleportNotAllowedReason reason)
        {
            reason = PortalTeleportNotAllowedReason.Unknown;

            if (Maps.ContainsKey(destinationMapId))
            {
                return true;
            }
            else // Not "usual" map.
            {
                var destinationMapDef = _mapDefinitions.Maps.FirstOrDefault(d => d.Id == destinationMapId);

                if (destinationMapDef is null)
                {
                    _logger.LogWarning($"Map {destinationMapId} is not found in map definitions.");
                    return false;
                }

                if (destinationMapDef.CreateType == CreateType.Party)
                {
                    if (player.Party is null)
                    {
                        reason = PortalTeleportNotAllowedReason.OnlyForParty;
                        return false;
                    }

                    if (player.Party != null && (player.Party.Members.Count < destinationMapDef.MinMembersCount || (destinationMapDef.MaxMembersCount != 0 && player.Party.Members.Count > destinationMapDef.MaxMembersCount)))
                    {
                        reason = PortalTeleportNotAllowedReason.NotEnoughPartyMembers;
                        return false;
                    }

                    return true;
                }

                if (destinationMapDef.CreateType == CreateType.GuildHouse)
                {
                    if (!player.HasGuild)
                    {
                        reason = PortalTeleportNotAllowedReason.OnlyForGuilds;
                        return false;
                    }

                    if (!player.GuildHasHouse)
                    {
                        reason = PortalTeleportNotAllowedReason.NoGuildHouse;
                        return false;
                    }

                    if (!player.GuildHasTopRank)
                    {
                        reason = PortalTeleportNotAllowedReason.OnlyTop30Guilds;
                        return false;
                    }

                    // TODO: check weekly payment FeeNotPaid.

                    return true;
                }

                if (destinationMapDef.CreateType == CreateType.GRB)
                {
                    if (!player.HasGuild)
                    {
                        reason = PortalTeleportNotAllowedReason.OnlyForGuilds;
                        return false;
                    }

                    if (!destinationMapDef.IsOpen(_timeService.UtcNow))
                    {
                        reason = PortalTeleportNotAllowedReason.NotTimeForRankingBattle;
                        return false;
                    }

                    if (_guildRankingManager.ParticipatedPlayers.Contains(player.Id))
                    {
                        reason = PortalTeleportNotAllowedReason.AlreadyParticipatedInBattle;
                        return false;
                    }

                    return true;
                }

                if (!destinationMapDef.IsOpen(_timeService.UtcNow))
                {
                    reason = PortalTeleportNotAllowedReason.OnlyForPartyAndOnTime;
                    return false;
                }

                return true;
            }
        }

        /// <inheritdoc/>
        public void EnsureMap(DbCharacter dbCharacter)
        {
            if (Maps.ContainsKey(dbCharacter.Map)) // All fine, map is presented on server.
                return;

            // Map was completely deleted from the server. Fallback to map 0.
            if (!AvailableMapIds.Contains(dbCharacter.Map))
            {
                var coordinates = Maps[0].GetNearestSpawn(0, 0, 0, dbCharacter.User.Faction);
                dbCharacter.Map = 0;
                dbCharacter.PosX = coordinates.X;
                dbCharacter.PosY = coordinates.Y;
                dbCharacter.PosZ = coordinates.Z;
                return;
            }

            // Map is an instance map. Likely for guild or party. Find out what is the rebirth map.
            if (!Maps.ContainsKey(dbCharacter.Map))
            {
                var definition = _mapDefinitions.Maps.First(m => m.Id == dbCharacter.Map);

                if (definition.RebirthMap != null) // Rebirth map for both factions set.
                {
                    dbCharacter.Map = definition.RebirthMap.MapId;
                    dbCharacter.PosX = definition.RebirthMap.PosX;
                    dbCharacter.PosY = definition.RebirthMap.PosY;
                    dbCharacter.PosZ = definition.RebirthMap.PosZ;
                    return;
                }

                if (dbCharacter.User.Faction == Fraction.Light)
                {
                    dbCharacter.Map = definition.LightRebirthMap.MapId;
                    dbCharacter.PosX = definition.LightRebirthMap.PosX;
                    dbCharacter.PosY = definition.LightRebirthMap.PosY;
                    dbCharacter.PosZ = definition.LightRebirthMap.PosZ;
                    return;
                }

                if (dbCharacter.User.Faction == Fraction.Dark)
                {
                    dbCharacter.Map = definition.DarkRebirthMap.MapId;
                    dbCharacter.PosX = definition.DarkRebirthMap.PosX;
                    dbCharacter.PosY = definition.DarkRebirthMap.PosY;
                    dbCharacter.PosZ = definition.DarkRebirthMap.PosZ;
                    return;
                }
            }

            _logger.LogError($"Couldn't ensure map {dbCharacter.Map} for player {dbCharacter.Id}! Check it manually!");
        }


        #endregion

        #region Party Maps

        /// <summary>
        /// Thread-safe dictionary of maps. Where key is party id.
        /// </summary>
        public ConcurrentDictionary<Guid, IPartyMap> PartyMaps { get; private set; } = new ConcurrentDictionary<Guid, IPartyMap>();

        private void PartyMap_OnAllMembersLeft(IPartyMap senser)
        {
            senser.OnAllMembersLeft -= PartyMap_OnAllMembersLeft;
            PartyMaps.TryRemove(senser.PartyId, out var removed);
            removed.Dispose();
        }

        #endregion

        #region Guild

        /// <summary>
        /// Thread-safe dictionary of maps. Where key is guild id.
        /// </summary>
        public ConcurrentDictionary<int, IGuildMap> GuildHouseMaps { get; private set; } = new ConcurrentDictionary<int, IGuildMap>();

        /// <summary>
        /// Thread-safe dictionary of maps. Where key is guild id.
        /// </summary>
        public ConcurrentDictionary<int, IGuildMap> GRBMaps { get; private set; } = new ConcurrentDictionary<int, IGuildMap>();

        /// <summary>
        /// Inits guild ranking battle timers.
        /// </summary>
        private void InitGRB()
        {
            _guildRankingManager.OnStartSoon += GuildRankingManager_OnStartSoon;
            _guildRankingManager.OnStarted += GuildRankingManager_OnStarted;
            _guildRankingManager.On10MinsLeft += GuildRankingManager_On10MinsLeft;
            _guildRankingManager.On1MinLeft += GuildRankingManager_On1MinLeft;
            _guildRankingManager.OnRanksCalculated += GuildRankingManager_OnRanksCalculated;
        }

        private void GuildRankingManager_OnStartSoon()
        {
            foreach (var player in Players.Values.ToList())
                player.SendGRBStartsSoon();
        }

        private void GuildRankingManager_OnStarted()
        {
            foreach (var player in Players.Values.ToList())
                player.SendGRBStarted();
        }

        private void GuildRankingManager_On10MinsLeft()
        {
            foreach (var player in Players.Values.ToList())
                player.SendGRB10MinsLeft();
        }

        private void GuildRankingManager_On1MinLeft()
        {
            foreach (var player in Players.Values.ToList())
                player.SendGRB1MinLeft();
        }

        private void GuildRankingManager_OnRanksCalculated(IEnumerable<(int guildId, int points, byte rank)> results)
        {
            foreach (var player in Players.Values.ToList())
            {
                player.ReloadGuildRanks(results);
                player.SendGuildRanksCalculated(results);
            }
        }

        #endregion

        #region Players

        /// <inheritdoc />
        public ConcurrentDictionary<int, Character> Players { get; private set; } = new ConcurrentDictionary<int, Character>();

        public ConcurrentDictionary<int, TradeManager> TradeManagers { get; private set; } = new ConcurrentDictionary<int, TradeManager>();

        public ConcurrentDictionary<int, PartyManager> PartyManagers { get; private set; } = new ConcurrentDictionary<int, PartyManager>();

        public ConcurrentDictionary<int, DuelManager> DuelManagers { get; private set; } = new ConcurrentDictionary<int, DuelManager>();

        /// <inheritdoc />
        public async Task<Character> LoadPlayer(int characterId, WorldClient client)
        {
            var newPlayer = await _characterFactory.CreateCharacter(characterId, client);
            if (newPlayer is null)
                return null;

            Players.TryAdd(newPlayer.Id, newPlayer);
            TradeManagers.TryAdd(newPlayer.Id, new TradeManager(this, newPlayer));
            PartyManagers.TryAdd(newPlayer.Id, new PartyManager(this, newPlayer));
            DuelManagers.TryAdd(newPlayer.Id, new DuelManager(this, newPlayer));

            _logger.LogDebug($"Player {newPlayer.Id} connected to game world");
            //newPlayer.Client.OnPacketArrived += Client_OnPacketArrived;

            return newPlayer;
        }

        /*private void Client_OnPacketArrived(ServerClient sender, IDeserializedPacket packet)
        {
            switch (packet)
            {
                case CharacterEnteredMapPacket enteredMapPacket:
                    LoadPlayerInMap(((WorldClient)sender).CharID);
                    break;
            }

        }*/

        /// <inheritdoc />
        public void LoadPlayerInMap(int characterId)
        {
            var player = Players[characterId];
            if (Maps.ContainsKey(player.MapId))
            {
                Maps[player.MapId].LoadPlayer(player);
            }
            else
            {
                var mapDef = _mapDefinitions.Maps.FirstOrDefault(d => d.Id == player.MapId);

                // Map is not found.
                if (mapDef is null)
                {
                    _logger.LogWarning($"Unknown map {player.MapId} for character {player.Id}. Fallback to 0 map.");
                    var town = Maps[0].GetNearestSpawn(player.PosX, player.PosY, player.PosZ, player.Country);
                    player.Teleport(0, town.X, town.Y, town.Z);
                    return;
                }

                if (mapDef.CreateType == CreateType.Party)
                {
                    IPartyMap map;
                    Guid partyId;

                    if (player.Party is null)
                    // This is very uncommon, but if:
                    // * player is an admin he can load into map even without party.
                    // * player entered portal, while being in party, but while he was loading, all party members left.
                    {
                        partyId = player.PreviousPartyId;
                    }
                    else
                    {
                        partyId = player.Party.Id;
                    }

                    PartyMaps.TryGetValue(partyId, out map);
                    if (map is null)
                    {
                        map = _mapFactory.CreatePartyMap(mapDef.Id, mapDef, _mapsLoader.LoadMapConfiguration(mapDef.Id), player.Party);
                        map.OnAllMembersLeft += PartyMap_OnAllMembersLeft;
                        PartyMaps.TryAdd(partyId, map);
                    }

                    map.LoadPlayer(player);
                }

                if (mapDef.CreateType == CreateType.GuildHouse || mapDef.CreateType == CreateType.GRB)
                {
                    int guildId = 0;
                    if (player.GuildId is null) // probably guild id has changed during loading in portal? Or it's admin without guild tries to load into GBR map.
                    {
                        _logger.LogWarning($"Trying to load character {player.Id} without guild id to guild specific map. Fallback to 0.");
                    }
                    else
                    {
                        guildId = (int)player.GuildId;
                    }

                    if (mapDef.CreateType == CreateType.GuildHouse)
                    {
                        IGuildMap map;
                        GuildHouseMaps.TryGetValue(guildId, out map);
                        if (map is null)
                        {
                            map = _mapFactory.CreateGuildMap(mapDef.Id, mapDef, _mapsLoader.LoadMapConfiguration(mapDef.Id), guildId);
                            GuildHouseMaps.TryAdd(guildId, map);
                        }

                        map.LoadPlayer(player);
                        return;
                    }

                    if (mapDef.CreateType == CreateType.GRB)
                    {
                        IGuildMap map;
                        GRBMaps.TryGetValue(guildId, out map);
                        if (map is null)
                        {
                            map = _mapFactory.CreateGuildMap(mapDef.Id, mapDef, _mapsLoader.LoadMapConfiguration(mapDef.Id), guildId);
                            GRBMaps.TryAdd(guildId, map);
                        }

                        map.LoadPlayer(player);
                        return;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void RemovePlayer(int characterId)
        {
            Character player;
            if (Players.TryRemove(characterId, out player))
            {
                _logger.LogDebug($"Player {characterId} left game world");

                TradeManagers.TryRemove(characterId, out var tradeManager);
                tradeManager.Dispose();

                PartyManagers.TryRemove(characterId, out var partyManager);
                partyManager.Dispose();

                DuelManagers.TryRemove(characterId, out var duelManager);
                duelManager.Dispose();

                //player.Client.OnPacketArrived -= Client_OnPacketArrived;

                IMap map = null;

                // Try find player's map.
                if (Maps.ContainsKey(player.MapId))
                    map = Maps[player.MapId];
                else if (player.Party != null && PartyMaps.ContainsKey(player.Party.Id))
                    map = PartyMaps[player.Party.Id];
                else if (PartyMaps.ContainsKey(player.PreviousPartyId))
                    map = PartyMaps[player.PreviousPartyId];
                else if (player.HasGuild && GuildHouseMaps.ContainsKey((int)player.GuildId))
                    map = GuildHouseMaps[(int)player.GuildId];
                else if (player.HasGuild && GRBMaps.ContainsKey((int)player.GuildId))
                    map = GRBMaps[(int)player.GuildId];

                if (map is null)
                    _logger.LogError($"Couldn't find character's {characterId} map {player.MapId}.");
                else
                    map.UnloadPlayer(player);

                player.Dispose();
            }
            else
            {
                // 0 means, that connection with client was lost, when he was in character selection screen.
                if (characterId != 0)
                {
                    _logger.LogError($"Couldn't remove player {characterId} from game world");
                }
            }

        }

        #endregion

        #region Bless

        /// <summary>
        /// Goddess bless.
        /// </summary>
        public Bless Bless { get; private set; } = Bless.Instance;

        #endregion
    }
}
