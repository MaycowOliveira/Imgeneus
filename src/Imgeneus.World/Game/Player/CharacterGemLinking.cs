﻿using Imgeneus.Database.Constants;
using Imgeneus.DatabaseBackgroundService.Handlers;
using Imgeneus.World.Game.Zone;
using System.Collections.Generic;
using System.Linq;
using System.Security;

namespace Imgeneus.World.Game.Player
{
    public partial class Character
    {
        public void AddGem(byte bag, byte slot, byte destinationBag, byte destinationSlot, byte hammerBag, byte hammerSlot)
        {
            InventoryItems.TryGetValue((bag, slot), out var gem);
            if (gem is null || gem.Type != Item.GEM_ITEM_TYPE)
                return;

            var linkingGold = _linkingManager.GetGold(gem);
            if (Gold < linkingGold)
            {
                // TODO: send warning, that not enough money?
                return;
            }

            InventoryItems.TryGetValue((destinationBag, destinationSlot), out var item);
            if (item is null || item.FreeSlots == 0 || item.ContainsGem(gem.TypeId))
                return;

            Item hammer = null;
            if (hammerBag != 0)
                InventoryItems.TryGetValue((hammerBag, hammerSlot), out hammer);

            Item saveItem = null;
            if (gem.ReqVg > 0)
            {
                saveItem = InventoryItems.Select(itm => itm.Value).FirstOrDefault(itm => itm.Special == SpecialEffect.LuckyCharm);
                if (saveItem != null)
                    TryUseItem(saveItem.Bag, saveItem.Slot);
            }

            var result = _linkingManager.AddGem(item, gem, hammer, CalculateExtraRate());
            ChangeGold((uint)(Gold - linkingGold));
            if (gem.Count > 0)
            {
                _taskQueue.Enqueue(ActionType.UPDATE_ITEM_COUNT_IN_INVENTORY,
                                   Id, gem.Bag, gem.Slot, gem.Count);
            }
            else
            {
                InventoryItems.TryRemove((gem.Bag, gem.Slot), out var removedGem);
                _taskQueue.Enqueue(ActionType.REMOVE_ITEM_FROM_INVENTORY,
                                   Id, gem.Bag, gem.Slot);
            }

            if (result.Success)
                _taskQueue.Enqueue(ActionType.UPDATE_GEM, Id, item.Bag, item.Slot, result.Slot, (int)gem.TypeId);

            if (hammer != null)
                TryUseItem(hammer.Bag, hammer.Slot);

            _packetsHelper.SendAddGem(Client, result.Success, gem, item, result.Slot, Gold, saveItem, hammer);

            if (result.Success && item.Bag == 0)
            {
                StatsManager.ExtraStr += gem.Str;
                StatsManager.ExtraDex += gem.Dex;
                StatsManager.ExtraRec += gem.Rec;
                StatsManager.ExtraInt += gem.Int;
                StatsManager.ExtraLuc += gem.Luc;
                StatsManager.ExtraWis += gem.Wis;
                StatsManager.ExtraHP += gem.HP;
                StatsManager.ExtraSP += gem.SP;
                StatsManager.ExtraMP += gem.MP;
                StatsManager.ExtraDefense += gem.Defense;
                StatsManager.ExtraResistance += gem.Resistance;
                StatsManager.Absorption += gem.Absorb;
                MoveSpeed += gem.MoveSpeed;
                SetAttackSpeedModifier(gem.AttackSpeed);

                if (gem.Str != 0 || gem.Dex != 0 || gem.Rec != 0 || gem.Wis != 0 || gem.Int != 0 || gem.Luc != 0 || gem.MinAttack != 0 || gem.MaxAttack != 0)
                    SendAdditionalStats();

                if (gem.AttackSpeed != 0 || gem.MoveSpeed != 0)
                    InvokeAttackOrMoveChanged();
            }

            if (!result.Success && saveItem == null && gem.ReqVg > 0)
            {
                RemoveItemFromInventory(item);
                SendRemoveItemFromInventory(item, true);

                if (item.Bag == 0)
                {
                    /*if (item == InventoryManager.Helmet)
                        InventoryManager.Helmet = null;
                    else if (item == Armor)
                        Armor = null;
                    else if (item == Pants)
                        Pants = null;
                    else if (item == Gauntlet)
                        Gauntlet = null;
                    else if (item == Boots)
                        Boots = null;
                    else if (item == Weapon)
                        Weapon = null;
                    else if (item == Shield)
                        Shield = null;
                    else if (item == Cape)
                        Cape = null;
                    else if (item == Amulet)
                        Amulet = null;
                    else if (item == Ring1)
                        Ring1 = null;
                    else if (item == Ring2)
                        Ring2 = null;
                    else if (item == Bracelet1)
                        Bracelet1 = null;
                    else if (item == Bracelet2)
                        Bracelet2 = null;
                    else if (item == Mount)
                        Mount = null;
                    else if (item == Pet)
                        Pet = null;
                    else if (item == Costume)
                        Costume = null;*/
                }
            }
        }

        public void AddGemPossibility(byte gemBag, byte gemSlot, byte destinationBag, byte destinationSlot, byte hammerBag, byte hammerSlot)
        {
            InventoryItems.TryGetValue((gemBag, gemSlot), out var gem);
            if (gem is null)
                return;

            Item hammer = null;
            if (hammerBag != 0)
                InventoryItems.TryGetValue((hammerBag, hammerSlot), out hammer);

            var rate = _linkingManager.GetRate(gem, hammer, CalculateExtraRate());
            var gold = _linkingManager.GetGold(gem);

            _packetsHelper.SendGemPossibility(Client, rate, gold);
        }

        public void RemoveGem(byte bag, byte slot, bool shouldRemoveSpecificGem, byte gemPosition, byte hammerBag, byte hammerSlot)
        {
            InventoryItems.TryGetValue((bag, slot), out var item);
            if (item is null)
                return;

            bool success = false;
            int spentGold = 0;
            var gemItems = new List<Item>() { null, null, null, null, null, null };
            var savedGems = new List<Gem>();
            var removedGems = new List<Gem>();
            if (shouldRemoveSpecificGem)
            {
                Gem gem = null;
                switch (gemPosition)
                {
                    case 0:
                        gem = item.Gem1;
                        item.Gem1 = null;
                        break;

                    case 1:
                        gem = item.Gem2;
                        item.Gem2 = null;
                        break;

                    case 2:
                        gem = item.Gem3;
                        item.Gem3 = null;
                        break;

                    case 3:
                        gem = item.Gem4;
                        item.Gem4 = null;
                        break;

                    case 4:
                        gem = item.Gem5;
                        item.Gem5 = null;
                        break;

                    case 5:
                        gem = item.Gem6;
                        item.Gem6 = null;
                        break;
                }

                if (gem is null)
                    return;

                InventoryItems.TryGetValue((hammerBag, hammerSlot), out var hammer);
                if (hammer != null)
                    TryUseItem(hammer.Bag, hammer.Slot);

                success = _linkingManager.RemoveGem(item, gem, hammer, CalculateExtraRate());
                spentGold += _linkingManager.GetRemoveGold(gem);

                if (success)
                {
                    savedGems.Add(gem);
                    var gemItem = new Item(_databasePreloader, Item.GEM_ITEM_TYPE, (byte)gem.TypeId);
                    AddItemToInventory(gemItem);

                    if (gemItem != null)
                        gemItems[gem.Position] = gemItem;
                    //else // Not enough place in inventory.
                    // Map.AddItem(); ?
                }
                removedGems.Add(gem);
            }
            else
            {
                var gems = new List<Gem>();

                if (item.Gem1 != null)
                    gems.Add(item.Gem1);

                if (item.Gem2 != null)
                    gems.Add(item.Gem2);

                if (item.Gem3 != null)
                    gems.Add(item.Gem3);

                if (item.Gem4 != null)
                    gems.Add(item.Gem4);

                if (item.Gem5 != null)
                    gems.Add(item.Gem5);

                if (item.Gem6 != null)
                    gems.Add(item.Gem6);

                foreach (var gem in gems)
                {
                    success = _linkingManager.RemoveGem(item, gem, null);
                    spentGold += _linkingManager.GetRemoveGold(gem);

                    if (success)
                    {
                        savedGems.Add(gem);
                        var gemItem = new Item(_databasePreloader, Item.GEM_ITEM_TYPE, (byte)gem.TypeId);
                        AddItemToInventory(gemItem);

                        if (gemItem != null)
                            gemItems[gem.Position] = gemItem;
                        //else // Not enough place in inventory.
                        // Map.AddItem(); ?
                    }
                }

                removedGems.AddRange(gems);
                gemPosition = 255; // when remove all gems
            }

            ChangeGold((uint)(Gold - spentGold));

            _packetsHelper.SendRemoveGem(Client, gemItems.Count > 0, item, gemPosition, gemItems, Gold);

            bool itemDestroyed = false;
            foreach (var gem in removedGems)
            {
                if (gem.ReqVg > 0 && !savedGems.Contains(gem))
                {
                    itemDestroyed = true;
                    break;
                }
            }

            if (item.Bag == 0)
            {
                if (itemDestroyed)
                {
                    /*if (item == Helmet)
                        Helmet = null;
                    else if (item == Armor)
                        Armor = null;
                    else if (item == Pants)
                        Pants = null;
                    else if (item == Gauntlet)
                        Gauntlet = null;
                    else if (item == Boots)
                        Boots = null;
                    else if (item == Weapon)
                        Weapon = null;
                    else if (item == Shield)
                        Shield = null;
                    else if (item == Cape)
                        Cape = null;
                    else if (item == Amulet)
                        Amulet = null;
                    else if (item == Ring1)
                        Ring1 = null;
                    else if (item == Ring2)
                        Ring2 = null;
                    else if (item == Bracelet1)
                        Bracelet1 = null;
                    else if (item == Bracelet2)
                        Bracelet2 = null;
                    else if (item == Mount)
                        Mount = null;
                    else if (item == Pet)
                        Pet = null;
                    else if (item == Costume)
                        Costume = null;*/
                }
                else
                {
                    foreach (var gem in removedGems)
                    {
                        StatsManager.ExtraStr -= gem.Str;
                        StatsManager.ExtraDex -= gem.Dex;
                        StatsManager.ExtraRec -= gem.Rec;
                        StatsManager.ExtraInt -= gem.Int;
                        StatsManager.ExtraLuc -= gem.Luc;
                        StatsManager.ExtraWis -= gem.Wis;
                        StatsManager.ExtraHP -= gem.HP;
                        StatsManager.ExtraSP -= gem.SP;
                        StatsManager.ExtraMP -= gem.MP;
                        StatsManager.ExtraDefense -= gem.Defense;
                        StatsManager.ExtraResistance -= gem.Resistance;
                        StatsManager.Absorption -= gem.Absorb;
                        MoveSpeed -= gem.MoveSpeed;
                        SetAttackSpeedModifier(gem.AttackSpeed * (-1));

                        if (gem.Str != 0 || gem.Dex != 0 || gem.Rec != 0 || gem.Wis != 0 || gem.Int != 0 || gem.Luc != 0 || gem.MinAttack != 0 || gem.PlusAttack != 0)
                            SendAdditionalStats();

                        if (gem.AttackSpeed != 0 || gem.MoveSpeed != 0)
                            InvokeAttackOrMoveChanged();
                    }
                }
            }

            // Send gem update to db.
            if (itemDestroyed)
            {
                RemoveItemFromInventory(item);
                SendRemoveItemFromInventory(item, true);
            }
            else
            {
                foreach (var gem in removedGems)
                {
                    switch (gem.Position)
                    {
                        case 0:
                            item.Gem1 = null;
                            break;

                        case 1:
                            item.Gem2 = null;
                            break;

                        case 2:
                            item.Gem3 = null;
                            break;

                        case 3:
                            item.Gem4 = null;
                            break;

                        case 4:
                            item.Gem5 = null;
                            break;

                        case 5:
                            item.Gem6 = null;
                            break;
                    }
                    _taskQueue.Enqueue(ActionType.UPDATE_GEM, Id, item.Bag, item.Slot, gem.Position, 0);
                }
            }
        }

        public void GemRemovePossibility(byte bag, byte slot, bool shouldRemoveSpecificGem, byte gemPosition, byte hammerBag, byte hammerSlot)
        {
            InventoryItems.TryGetValue((bag, slot), out var item);
            if (item is null)
                return;

            double rate = 0;
            int gold = 0;

            if (shouldRemoveSpecificGem)
            {
                Gem gem = null;
                switch (gemPosition)
                {
                    case 0:
                        gem = item.Gem1;
                        break;

                    case 1:
                        gem = item.Gem2;
                        break;

                    case 2:
                        gem = item.Gem3;
                        break;

                    case 3:
                        gem = item.Gem4;
                        break;

                    case 4:
                        gem = item.Gem5;
                        break;

                    case 5:
                        gem = item.Gem6;
                        break;
                }

                if (gem is null)
                    return;

                InventoryItems.TryGetValue((hammerBag, hammerSlot), out var hammer);

                rate = _linkingManager.GetRemoveRate(gem, hammer, CalculateExtraRate());
                gold = _linkingManager.GetRemoveGold(gem);
            }
            else
            {
                var gems = new List<Gem>();

                if (item.Gem1 != null)
                    gems.Add(item.Gem1);
                if (item.Gem2 != null)
                    gems.Add(item.Gem2);
                if (item.Gem3 != null)
                    gems.Add(item.Gem3);
                if (item.Gem4 != null)
                    gems.Add(item.Gem4);
                if (item.Gem5 != null)
                    gems.Add(item.Gem5);
                if (item.Gem6 != null)
                    gems.Add(item.Gem6);

                foreach (var gem in gems)
                {
                    rate *= _linkingManager.GetRemoveRate(gem, null) / 100;
                    gold += _linkingManager.GetRemoveGold(gem);
                }

                rate = rate * 100;
            }

            _packetsHelper.SendGemRemovePossibility(Client, rate, gold);
        }

        /// <summary>
        /// Extra rate is made of guild house blacksmith rate + bless rate.
        /// </summary>
        /// <returns></returns>
        private byte CalculateExtraRate()
        {
            byte extraRate = 0;
            if (HasGuild && Map is GuildHouseMap)
            {
                var rates = _guildManager.GetBlacksmithRates((int)GuildId);
                extraRate += rates.LinkRate;
            }

            // TODO: add bless rate.

            return extraRate;
        }
    }
}
