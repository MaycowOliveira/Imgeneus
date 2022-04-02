﻿using Imgeneus.Network.Packets;
using Imgeneus.Network.Packets.Game;
using Imgeneus.World.Game;
using Imgeneus.World.Game.Guild;
using Imgeneus.World.Game.Inventory;
using Imgeneus.World.Game.Session;
using Imgeneus.World.Game.Teleport;
using Imgeneus.World.Game.Zone;
using Imgeneus.World.Game.Zone.MapConfig;
using Imgeneus.World.Game.Zone.Portals;
using Imgeneus.World.Packets;
using Microsoft.Extensions.Logging;
using Sylver.HandlerInvoker.Attributes;

namespace Imgeneus.World.Handlers
{
    [Handler]
    public class TeleportHandlers : BaseHandler
    {
        private readonly ILogger<TeleportHandlers> _logger;
        private readonly ITeleportationManager _teleportationManager;
        private readonly IMapProvider _mapProvider;
        private readonly IGameWorld _gameWorld;
        private readonly IGuildManager _guildManager;
        private readonly IInventoryManager _inventoryManager;
        private readonly IMapsLoader _mapLoader;

        public TeleportHandlers(ILogger<TeleportHandlers> logger, IGamePacketFactory packetFactory, IGameSession gameSession, ITeleportationManager teleportationManager, IMapProvider mapProvider, IGameWorld gameWorld, IGuildManager guildManager, IInventoryManager inventoryManager, IMapsLoader mapLoader) : base(packetFactory, gameSession)
        {
            _logger = logger;
            _teleportationManager = teleportationManager;
            _mapProvider = mapProvider;
            _gameWorld = gameWorld;
            _guildManager = guildManager;
            _inventoryManager = inventoryManager;
            _mapLoader = mapLoader;
        }

        [HandlerAction(PacketType.CHARACTER_ENTERED_PORTAL)]
        public void HandlePortalTeleport(WorldClient client, CharacterEnteredPortalPacket packet)
        {
            var success = _teleportationManager.TryTeleport(packet.PortalId, out var reason);

            if (!success)
                _packetFactory.SendPortalTeleportNotAllowed(client, reason);
        }

        [HandlerAction(PacketType.CHARACTER_TELEPORT_VIA_NPC)]
        public void HandleNpcTeleport(WorldClient client, CharacterTeleportViaNpcPacket packet)
        {

            var npc = _mapProvider.Map.GetNPC(_gameWorld.Players[_gameSession.CharId].CellId, packet.NpcId);
            if (npc is null)
            {
                _logger.LogWarning("Character {Id} is trying to get non-existing npc via teleport packet.", _gameSession.CharId);
                return;
            }

            if (!npc.ContainsGate(packet.GateId))
            {
                _logger.LogWarning("NPC type {type} type id {typeId} doesn't contain teleport gate {gateId}. Check it out!", npc.Type, npc.TypeId, packet.GateId);
                return;
            }

            if (_mapProvider.Map is GuildHouseMap)
            {
                if (!_guildManager.HasGuild)
                {
                    _packetFactory.SendGuildHouseActionError(client, GuildHouseActionError.LowRank, 30);
                    return;
                }

                var allowed = _guildManager.CanUseNpc(npc.Type, npc.TypeId, out var requiredRank);
                if (!allowed)
                {
                    _packetFactory.SendGuildHouseActionError(client, GuildHouseActionError.LowRank, requiredRank);
                    return;
                }

                allowed = _guildManager.HasNpcLevel(npc.Type, npc.TypeId);
                if (!allowed)
                {
                    _packetFactory.SendGuildHouseActionError(client, GuildHouseActionError.LowLevel, 0);
                    return;
                }
            }

            var gate = npc.Gates[packet.GateId];

            if (_inventoryManager.Gold < gate.Cost)
            {
                _packetFactory.SendTeleportViaNpc(client, NpcTeleportNotAllowedReason.NotEnoughMoney, _inventoryManager.Gold);
                return;
            }

            var mapConfig = _mapLoader.LoadMapConfiguration(gate.MapId);
            if (mapConfig is null)
            {
                _packetFactory.SendTeleportViaNpc(client, NpcTeleportNotAllowedReason.MapCapacityIsFull, _inventoryManager.Gold);
                return;
            }

            // TODO: there should be somewhere player's level check. But I can not find it in gate config.

            _inventoryManager.Gold = (uint)(_inventoryManager.Gold - gate.Cost);
            _packetFactory.SendTeleportViaNpc(client, NpcTeleportNotAllowedReason.Success, _inventoryManager.Gold);
            _teleportationManager.Teleport(gate.MapId, gate.X, gate.Y, gate.Z);
        }
    }
}