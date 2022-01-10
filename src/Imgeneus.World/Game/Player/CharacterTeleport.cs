﻿using Imgeneus.DatabaseBackgroundService.Handlers;
using Imgeneus.World.Game.Duel;
using Imgeneus.World.Game.Zone;
using Imgeneus.World.Game.Zone.Portals;
using Microsoft.Extensions.Logging;

namespace Imgeneus.World.Game.Player
{
    public partial class Character
    {
        /// <summary>
        /// Indicator if character is teleporting between maps.
        /// </summary>
        public bool IsTeleporting { get; private set; }

        protected override void OnMapSet()
        {
            IsTeleporting = false;

            // Send map values.
            //SendWeather();
            //SendObelisks();
            //SendMyShape(); // Should fix the issue with dye color, when first connection.
        }

        /// <summary>
        /// Teleports character inside one map or to another map.
        /// </summary>
        /// <param name="mapId">map id, where to teleport</param>
        /// <param name="X">x coordinate, where to teleport</param>
        /// <param name="Y">y coordinate, where to teleport</param>
        /// <param name="Z">z coordinate, where to teleport</param>
        /// <param name="teleportedByAdmin">Indicates whether the teleport was issued by an admin or not</param>
        public void Teleport(ushort mapId, float x, float y, float z, bool teleportedByAdmin = false)
        {
            IsTeleporting = true;

            var prevMapId = MapId;
            MapId = mapId;
            PosX = x;
            PosY = y;
            PosZ = z;
            _taskQueue.Enqueue(ActionType.SAVE_MAP_ID, Id, MapId);
            _taskQueue.Enqueue(ActionType.SAVE_CHARACTER_MOVE, Id, x, y, z, Angle);

            // If player is teleported inside one map, we should notify other players about this teleport.
            // If player is teleported to another map, we should only send player left map packet (in Map.UnloadPlayer call).
            if (prevMapId == MapId)
            {
                Map.TeleportPlayer(Id, teleportedByAdmin);
                IsTeleporting = false;
            }
            else // But we must always send the teleport packet directly to the player.
            {
                _packetsHelper.SendCharacterTeleport(Client, this, teleportedByAdmin);
                if (IsDuelApproved)
                    FinishDuel(DuelCancelReason.TooFarAway);
                Map.UnloadPlayer(this);
            }
        }

        /// <summary>
        /// Teleports character with the help of the portal, if it's possible.
        /// </summary>
        public bool TryTeleport(byte portalIndex, out PortalTeleportNotAllowedReason reason)
        {
            reason = PortalTeleportNotAllowedReason.Unknown;
            if (Map.Portals.Count <= portalIndex)
            {
                _logger.LogWarning($"Unknown portal {portalIndex} for map {Map.Id}. Send from character {Id}.");
                return false;
            }

            var portal = Map.Portals[portalIndex];
            if (!portal.IsInPortalZone(PosX, PosY, PosZ))
            {
                _logger.LogWarning($"Character position is not in portal, map {Map.Id}. Portal index {portalIndex}. Send from character {Id}.");
                return false;
            }

            if (!portal.IsSameFaction(Country))
            {
                return false;
            }

            if (!portal.IsRightLevel(Level))
            {
                return false;
            }

            if (_gameWorld.CanTeleport(this, portal.MapId, out reason))
            {
                Teleport(portal.MapId, portal.Destination_X, portal.Destination_Y, portal.Destination_Z);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
