﻿using System;
using Imgeneus.Network.Packets.Game;
using Imgeneus.World.Game.Monster;
using Imgeneus.World.Game.Zone.Obelisks;
using Imgeneus.World.Game.Zone.Portals;
using System.Linq;
using Imgeneus.World.Game.Guild;
using Imgeneus.Database.Entities;
using Imgeneus.Network.Server;
using System.Collections.Generic;
using Imgeneus.World.Game.Health;
using Imgeneus.World.Game.Buffs;
using Imgeneus.World.Game.Skills;
using Imgeneus.World.Game.Inventory;

namespace Imgeneus.World.Game.Player
{
    public partial class Character
    {
        /// <summary>
        /// Sends to client character start-up information.
        /// </summary>
        public void SendCharacterInfo()
        {
            // SendWorldDay(); // TODO: why do we need it?
            SendGuildList();
            SendGuildMembersOnline();
            SendDetails();
            //SendAdditionalStats();
            //SendCurrentHitpoints();
            SendInventoryItems();
            SendLearnedSkills();
            SendOpenQuests();
            SendFinishedQuests();
            //SendActiveBuffs();
            SendMoveAndAttackSpeed();
            SendFriends();
            SendBlessAmount();
            SendBankItems();
            SendGuildNpcLvlList();
            SendAutoStats();
#if !EP8_V2
            SendAccountPoints(); // WARNING: This is necessary if you have an in-game item mall.
#endif
        }

        private void SendWorldDay() => _packetsHelper.SendWorldDay(Client);

        private void SendDetails() => _packetsHelper.SendDetails(Client, this);

        private void SendInventoryItems()
        {
            var inventoryItems = InventoryItems.Values.ToArray();
            _packetsHelper.SendInventoryItems(Client, inventoryItems); // WARNING: some servers expanded invetory to 6 bags(os is 5 bags), if you send item in 6 bag, client will crash!

            foreach (var item in inventoryItems.Where(i => i.ExpirationTime != null))
                SendItemExpiration(item);
        }

        private void SendAdditionalStats() => _packetsHelper.SendAdditionalStats(Client, this);

        private void SendItemExpiration(Item item) => _packetsHelper.SendItemExpiration(Client, item);

        private void SendLearnedSkills() => _packetsHelper.SendLearnedSkills(Client, this);

        private void SendOpenQuests() => _packetsHelper.SendQuests(Client, Quests.Where(q => !q.IsFinished));

        private void SendFinishedQuests() => _packetsHelper.SendFinishedQuests(Client, Quests.Where(q => q.IsFinished));

        private void SendQuestStarted(Quest quest, int npcId = 0) => _packetsHelper.SendQuestStarted(Client, quest.Id, npcId);

        private void SendQuestFinished(Quest quest, int npcId = 0) => _packetsHelper.SendQuestFinished(Client, quest, npcId);

        private void SendFriendRequest(Character requester) => _packetsHelper.SendFriendRequest(Client, requester);

        private void SendFriendOnline(int friendId, bool isOnline) => _packetsHelper.SendFriendOnline(Client, friendId, isOnline);

        private void SendFriends() => _packetsHelper.SendFriends(Client, Friends.Values);

        private void SendFriendAdd(Character friend) => _packetsHelper.SendFriendAdded(Client, friend);

        private void SendFriendResponse(bool accepted) => _packetsHelper.SendFriendResponse(Client, accepted);

        private void SendFriendDelete(int id) => _packetsHelper.SendFriendDelete(Client, id);

        private void SendQuestCountUpdate(ushort questId, byte index, byte count) => _packetsHelper.SendQuestCountUpdate(Client, questId, index, count);

        //private void SendActiveBuffs() => _packetsHelper.SendActiveBuffs(Client, ActiveBuffs);

        private void SendAddBuff(Buff buff) => _packetsHelper.SendAddBuff(Client, buff);

        private void SendRemoveBuff(Buff buff) => _packetsHelper.SendRemoveBuff(Client, buff);

        private void SendAutoStats() => _packetsHelper.SendAutoStats(Client, this);

        //private void SendMaxHP() => _packetsHelper.SendMaxHitpoints(Client, this, HitpointType.HP);

        //private void SendMaxSP() => _packetsHelper.SendMaxHitpoints(Client, this, HitpointType.SP);

        //private void SendMaxMP() => _packetsHelper.SendMaxHitpoints(Client, this, HitpointType.MP);

        private void SendAttackStart() => _packetsHelper.SendAttackStart(Client);

        private void SendAutoAttackWrongTarget(IKillable target) => _packetsHelper.SendAutoAttackWrongTarget(Client, this, target);

        private void SendAutoAttackWrongEquipment(IKillable target) =>
            _packetsHelper.SendAutoAttackWrongEquipment(Client, this, target);

        private void SendAutoAttackCanNotAttack(IKillable target) => _packetsHelper.SendAutoAttackCanNotAttack(Client, this, target);

        private void SendSkillAttackCanNotAttack(IKillable target, Skill skill) => _packetsHelper.SendSkillAttackCanNotAttack(Client, this, skill, target);

        private void SendSkillWrongTarget(IKillable target, Skill skill) => _packetsHelper.SendSkillWrongTarget(Client, this, skill, target);

        private void SendSkillWrongEquipment(IKillable target, Skill skill) => _packetsHelper.SendSkillWrongEquipment(Client, this, target, skill);

        private void SendNotEnoughMPSP(IKillable target, Skill skill) => _packetsHelper.SendNotEnoughMPSP(Client, this, target, skill);

        private void SendUseSMMP(ushort needMP, ushort needSP) => _packetsHelper.SendUseSMMP(Client, needMP, needSP);

        private void SendCooldownNotOver(IKillable target, Skill skill) => _packetsHelper.SendCooldownNotOver(Client, this, target, skill);

        protected void SendMoveAndAttackSpeed()
        {
            if (Client != null) _packetsHelper.SendMoveAndAttackSpeed(Client, this);
        }

        private void SendRunMode() => _packetsHelper.SendRunMode(Client, this);

        private void SendTargetAddBuff(int targetId, Buff buff, bool isMob) => _packetsHelper.SendTargetAddBuff(Client, targetId, buff, isMob);

        private void SendTargetRemoveBuff(int targetId, Buff buff, bool isMob) => _packetsHelper.SendTargetRemoveBuff(Client, targetId, buff, isMob);

        public void SendAddItemToInventory(Item item)
        {
            _packetsHelper.SendAddItem(Client, item);

            if (item.ExpirationTime != null)
                _packetsHelper.SendItemExpiration(Client, item);
        }

        public void SendRemoveItemFromInventory(Item item, bool fullRemove) => _packetsHelper.SendRemoveItem(Client, item, fullRemove);

        public void SendWeather() => _packetsHelper.SendWeather(Client, Map);

        public void SendObelisks() => _packetsHelper.SendObelisks(Client, Map.Obelisks.Values);

        public void SendObeliskBroken(Obelisk obelisk) => _packetsHelper.SendObeliskBroken(Client, obelisk);

        public void SendPortalTeleportNotAllowed(PortalTeleportNotAllowedReason reason) => _packetsHelper.SendPortalTeleportNotAllowed(Client, reason);

        public void SendTeleportViaNpc(NpcTeleportNotAllowedReason reason) => _packetsHelper.SendTeleportViaNpc(Client, reason, InventoryManager.Gold);

        public void SendUseVehicle(bool success, bool status) => _packetsHelper.SendUseVehicle(Client, success, status);

        public void SendVehicleResponse(VehicleResponse status) => _packetsHelper.SendVehicleResponse(Client, status);

        public void SendVehicleRequest(int requesterId) => _packetsHelper.SendVehicleRequest(Client, requesterId);

        public void SendMyShape() => _packetsHelper.SendCharacterShape(Client, this);

        private void TargetChanged(IKillable target)
        {
            if (target is Mob)
            {
                _packetsHelper.SetMobInTarget(Client, (Mob)target);
            }
            else
            {
                _packetsHelper.SetPlayerInTarget(Client, (Character)target);
            }
        }

        public void SendAttribute(CharacterAttributeEnum attribute) =>
            _packetsHelper.SendAttribute(Client, attribute, GetAttributeValue(attribute));

        public void SendExperienceGain(ushort expAmount) => _packetsHelper.SendExperienceGain(Client, expAmount);

        public void SendWarning(string message) => _packetsHelper.SendWarning(Client, message);

        public void SendBankItems() => _packetsHelper.SendBankItems(Client, BankItems.Values.ToList());

        public void SendBankItemClaim(byte bankSlot, Item item) => _packetsHelper.SendBankItemClaim(Client, bankSlot, item);
        public void SendAccountPoints() => _packetsHelper.SendAccountPoints(Client, Points);

        public void SendResetSkills() => _packetsHelper.SendResetSkills(Client, SkillsManager.SkillPoints);

        public void SendGuildCreateFailed(GuildCreateFailedReason reason) => _packetsHelper.SendGuildCreateFailed(Client, reason);

        public void SendGuildCreateSuccess(int guildId, byte rank, string guildName, string guildMessage) => _packetsHelper.SendGuildCreateSuccess(Client, guildId, rank, guildName, guildMessage);

        public void SendGuildCreateRequest(int creatorId, string guildName, string guildMessage) => _packetsHelper.SendGuildCreateRequest(Client, creatorId, guildName, guildMessage);

        public void SendGuildMemberIsOnline(int playerId) => _packetsHelper.SendGuildMemberIsOnline(Client, playerId);

        public void SendGuildMemberIsOffline(int playerId) => _packetsHelper.SendGuildMemberIsOffline(Client, playerId);

        public void SendGuildJoinRequestAdd(DbCharacter character) => _packetsHelper.SendGuildJoinRequestAdd(Client, character);

        public void SendGuildJoinRequestRemove(int playerId) => _packetsHelper.SendGuildJoinRequestRemove(Client, playerId);

        public void SendGuildJoinResult(bool ok, DbGuild guild) => _packetsHelper.SendGuildJoinResult(Client, ok, guild);

        public void SendGuildUserListAdd(DbCharacter character, bool online) => _packetsHelper.SendGuildUserListAdd(Client, character, online);

        public void SendGuildKickMember(bool ok, int characterId) => _packetsHelper.SendGuildKickMember(Client, ok, characterId);

        public void SendGuildMemberRemove(int characterId) => _packetsHelper.SendGuildMemberRemove(Client, characterId);

        public void SendGuildUserChangeRank(int characterId, byte rank) => _packetsHelper.SendGuildUserChangeRank(Client, characterId, rank);

        public void SendGuildMemberLeave(int characterId) => _packetsHelper.SendGuildMemberLeave(Client, characterId);

        public void SendGuildMemberLeaveResult(bool ok) => _packetsHelper.SendGuildMemberLeaveResult(Client, ok);

        public void SendGuildDismantle() => _packetsHelper.SendGuildDismantle(Client);

        public void SendGuildListAdd(DbGuild guild) => _packetsHelper.SendGuildListAdd(Client, guild);

        public void SendGuildListRemove(int guildId) => _packetsHelper.SendGuildListRemove(Client, guildId);

        public void SendGBRPoints(int currentPoints, int maxPoints, int topGuild) => _packetsHelper.SendGBRPoints(Client, currentPoints, maxPoints, topGuild);

        public void SendGRBStartsSoon() => _packetsHelper.SendGRBNotice(Client, GRBNotice.StartsSoon);

        public void SendGRBStarted() => _packetsHelper.SendGRBNotice(Client, GRBNotice.Started);

        public void SendGRB10MinsLeft() => _packetsHelper.SendGRBNotice(Client, GRBNotice.Min10);

        public void SendGRB1MinLeft() => _packetsHelper.SendGRBNotice(Client, GRBNotice.Min1);

        public void SendGuildRanksCalculated(IEnumerable<(int GuildId, int Points, byte Rank)> results) => _packetsHelper.SendGuildRanksCalculated(Client, results);

        public void SendGoldUpdate() => _packetsHelper.SendGoldUpdate(Client, InventoryManager.Gold);
    }
}
