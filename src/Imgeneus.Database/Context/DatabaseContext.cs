﻿using Imgeneus.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Imgeneus.Database.Context
{
    public class DatabaseContext : DbContext, IDatabase
    {
        /// <summary>
        /// Gets or sets users.
        /// </summary>
        public DbSet<DbUser> Users { get; set; }

        /// <summary>
        /// Gets or sets the characters.
        /// </summary>
        public DbSet<DbCharacter> Characters { get; set; }

        /// <summary>
        /// Gets or sets the character items.
        /// </summary>
        public DbSet<DbCharacterItems> CharacterItems { get; set; }

        /// <summary>
        /// Gets or sets chracter skills.
        /// </summary>
        public DbSet<DbCharacterSkill> CharacterSkills { get; set; }

        /// <summary>
        /// Gets or sets chracter quests.
        /// </summary>
        public DbSet<DbCharacterQuest> CharacterQuests { get; set; }

        /// <summary>
        /// Collection of friend pairs.
        /// </summary>
        public DbSet<DbCharacterFriend> Friends { get; set; }

        /// <summary>
        /// Collection of skills. Taken from original db.
        /// </summary>
        public DbSet<DbSkill> Skills { get; set; }

        /// <summary>
        /// Collection of characters' active buffs.
        /// </summary>
        public DbSet<DbCharacterActiveBuff> ActiveBuffs { get; set; }

        /// <summary>
        /// Collection of items. Taken from original db.
        /// </summary>
        public DbSet<DbItem> Items { get; set; }

        /// <summary>
        /// Collection of mobs. Taken from original db.
        /// </summary>
        public DbSet<DbMob> Mobs { get; set; }

        /// <summary>
        /// Available drop from a monster. Taken from original db.
        /// </summary>
        public DbSet<DbMobItems> MobItems { get; set; }

        /// <summary>
        /// Quick items. E.g. skills on skill bar or motion on skill bar or iventory item on skill bar.
        /// </summary>
        public DbSet<DbQuickSkillBarItem> QuickItems { get; set; }

        /// <summary>
        /// Available npcs. Taken from NPCQuest.Sdata.
        /// </summary>
        public DbSet<DbNpc> Npcs { get; set; }

        /// <summary>
        /// Available quests. Taken from NPCQuest.Sdata.
        /// </summary>
        public DbSet<DbQuest> Quests { get; set; }

        /// <summary>
        /// Collection of levels and required experience for them. Taken from original db.
        /// </summary>
        public DbSet<DbLevel> Levels { get; set; }

        public DatabaseContext(DbContextOptions options) : base(options)
        {
            Migrate();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbUser>().HasIndex(c => new { c.Username, c.Email }).IsUnique();

            modelBuilder.Entity<DbSkill>().HasIndex(s => new { s.SkillId, s.SkillLevel });

            modelBuilder.Entity<DbItem>().HasKey(x => new { x.Type, x.TypeId });

            modelBuilder.Entity<DbMobItems>().HasKey(x => new { x.MobId, x.ItemOrder });

            modelBuilder.Entity<DbCharacter>().HasMany(x => x.QuickItems).WithOne(x => x.Character).IsRequired();
            modelBuilder.Entity<DbCharacter>().HasMany(x => x.Friends).WithOne(x => x.Character);

            modelBuilder.Entity<DbCharacterFriend>().HasKey(x => new { x.CharacterId, x.FriendId });

            #region Many to many relations
            // Skills.
            modelBuilder.Entity<DbCharacterSkill>().HasKey(x => new { x.CharacterId, x.SkillId });
            modelBuilder.Entity<DbCharacterSkill>().HasOne(pt => pt.Character).WithMany(p => p.Skills).HasForeignKey(pt => pt.CharacterId);

            // Active buffs.
            modelBuilder.Entity<DbCharacterActiveBuff>().HasOne(b => b.Character).WithMany(c => c.ActiveBuffs).HasForeignKey(b => b.CharacterId);

            // Items
            modelBuilder.Entity<DbCharacterItems>().HasOne(pt => pt.Character).WithMany(p => p.Items).HasForeignKey(pt => pt.CharacterId);

            // Quests
            modelBuilder.Entity<DbCharacterQuest>().HasOne(pt => pt.Character).WithMany(p => p.Quests).HasForeignKey(pt => pt.CharacterId);
            #endregion
        }

        /// <summary>
        /// Migrates the database schema.
        /// </summary>
        public void Migrate() => this.Database.Migrate();

        /// <summary>
        /// Check if the database exists.
        /// </summary>
        /// <returns></returns>
        public bool DatabaseExists() => (this.GetService<IDatabaseCreator>() as RelationalDatabaseCreator).Exists();
    }
}
