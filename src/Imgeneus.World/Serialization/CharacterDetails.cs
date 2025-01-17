﻿using BinarySerialization;
using Imgeneus.World.Game.Player;

namespace Imgeneus.Network.Serialization
{
    public class CharacterDetails : BaseSerializable
    {
        [FieldOrder(0)]
        public ushort Strength { get; }

        [FieldOrder(1)]
        public ushort Dexterity { get; }

        [FieldOrder(2)]
        public ushort Rec { get; }

        [FieldOrder(3)]
        public ushort Intelligence { get; }

        [FieldOrder(4)]
        public ushort Wisdom { get; }

        [FieldOrder(5)]
        public ushort Luck { get; }

        [FieldOrder(6)]
        public ushort StatPoint { get; }

        [FieldOrder(7)]
        public ushort SkillPoint { get; }

        [FieldOrder(8)]
        public int MaxHP { get; }

        [FieldOrder(9)]
        public int MaxMP { get; }

        [FieldOrder(10)]
        public int MaxSP { get; }

        [FieldOrder(11)]
        public ushort Angle { get; }

        [FieldOrder(12)]
        public uint StartLvlExp { get; }

        [FieldOrder(13)]
        public uint EndLvlExp { get; }

        [FieldOrder(14)]
        public uint CurrentExp { get; }

        [FieldOrder(15)]
        public uint Gold { get; }

        [FieldOrder(16)]
        public float PosX { get; }

        [FieldOrder(17)]
        public float PosY { get; }

        [FieldOrder(18)]
        public float PosZ { get; }

        [FieldOrder(19)]
        public uint Kills { get; }

        [FieldOrder(20)]
        public uint Deaths { get; }

        [FieldOrder(21)]
        public uint Victories { get; }

        [FieldOrder(22)]
        public uint Defeats { get; }

        [FieldOrder(23), FieldLength(25)]
        public string GuildName { get; }

        public CharacterDetails(Character character)
        {
#if !EP8_V2 // ep8 V2 crashes if these values are set.
            Strength = character.StatsManager.Strength;
            Dexterity = character.StatsManager.Dexterity;
            Rec = character.StatsManager.Reaction;
            Intelligence = character.StatsManager.Intelligence;
            Wisdom = character.StatsManager.Wisdom;
            Luck = character.StatsManager.Luck;
#endif
            StatPoint = character.StatsManager.StatPoint;
            SkillPoint = character.SkillsManager.SkillPoints;
            Angle = character.Angle;
            StartLvlExp = character.LevelingManager.MinLevelExp / 10; // Normalize experience for ep8 game
            EndLvlExp = character.LevelingManager.NextLevelExp / 10; // Normalize experience for ep8 game
            CurrentExp = character.LevelingManager.Exp / 10; // Normalize experience for ep8 game
            Gold = character.InventoryManager.Gold;
            PosX = character.PosX;
            PosY = character.PosY;
            PosZ = character.PosZ;
            Kills = character.KillsManager.Kills;
            Deaths = character.KillsManager.Deaths;
            Victories = character.KillsManager.Victories;
            Defeats = character.KillsManager.Defeats;
            MaxHP = character.HealthManager.MaxHP;
            MaxMP = character.HealthManager.MaxMP;
            MaxSP = character.HealthManager.MaxSP;
            GuildName = character.GuildManager.GuildName;
        }
    }
}
