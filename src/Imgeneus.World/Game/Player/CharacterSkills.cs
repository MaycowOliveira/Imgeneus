﻿using Imgeneus.Database.Constants;
using Imgeneus.DatabaseBackgroundService.Handlers;
using Imgeneus.Database.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imgeneus.World.Game.Player
{
    public partial class Character : IKillable
    {
        /// <summary>
        /// Collection of available skills.
        /// </summary>
        public Dictionary<int, Skill> Skills { get; private set; } = new Dictionary<int, Skill>();

        /// <summary>
        /// Event, that is fired, when character uses any skill.
        /// </summary>
        public event Action<IKiller, IKillable, Skill, AttackResult> OnUsedSkill;

        /// <summary>
        /// Event, that is fired, when character uses only range skill.
        /// </summary>
        public event Action<IKiller, IKillable, Skill, AttackResult> OnUsedRangeSkill;

        /// <summary>
        /// Player learns new skill.
        /// </summary>
        /// <param name="skillId">skill id</param>
        /// <param name="skillLevel">skill level</param>
        /// <returns>successful or not</returns>
        public void LearnNewSkill(ushort skillId, byte skillLevel)
        {
            if (Skills.Values.Any(s => s.SkillId == skillId && s.SkillLevel == skillLevel))
            {
                // Character has already learned this skill.
                // TODO: log it or throw exception?
                return;
            }

            // Find learned skill.
            var dbSkill = _databasePreloader.Skills[(skillId, skillLevel)];
            if (SkillPoint < dbSkill.SkillPoint)
            {
                // Not enough skill points.
                // TODO: log it or throw exception?
                return;
            }

            byte skillNumber = 0;

            // Find out if the character has already learned the same skill, but lower level.
            var isSkillLearned = Skills.Values.FirstOrDefault(s => s.SkillId == skillId);
            // If there is skill of lower level => delete it.
            if (isSkillLearned != null)
            {
                _taskQueue.Enqueue(ActionType.REMOVE_SKILL,
                                    Id, isSkillLearned.SkillId, isSkillLearned.SkillLevel);

                skillNumber = isSkillLearned.Number;
            }
            // No such skill. Generate new number.
            else
            {
                if (Skills.Any())
                {
                    // Find the next skill number.
                    skillNumber = Skills.Values.Select(s => s.Number).Max();
                    skillNumber++;
                }
                else
                {
                    // No learned skills at all.
                }
            }

            // Save char and learned skill.
            _taskQueue.Enqueue(ActionType.SAVE_SKILL, Id, dbSkill.SkillId, dbSkill.SkillLevel, skillNumber);

            // Remove previously learned skill.
            if (isSkillLearned != null) Skills.Remove(skillNumber);

            SkillPoint -= dbSkill.SkillPoint;
            _taskQueue.Enqueue(ActionType.SAVE_CHARACTER_SKILLPOINT, Id, SkillPoint);

            var skill = new Skill(dbSkill, skillNumber, 0);
            Skills.Add(skillNumber, skill);

            if (Client != null)
                _packetsHelper.SendLearnedNewSkill(Client, skill);

            _logger.LogDebug($"Character {Id} learned skill {skill.SkillId} of level {skill.SkillLevel}");

            // Activate passive skill as soon as it's learned.
            if (skill.IsPassive)
                UseSkill(skill);
        }

        /// <summary>
        /// Calculates healing result.
        /// </summary>
        public AttackResult UsedHealingSkill(Skill skill, IKillable target)
        {
            var healHP = StatsManager.TotalWis * 4 + skill.HealHP;
            var healSP = skill.HealSP;
            var healMP = skill.HealMP;
            AttackResult result = new AttackResult(AttackSuccess.Normal, new Damage((ushort)healHP, healSP, healMP));

            target.HealthManager.IncreaseHP(healHP);
            target.HealthManager.CurrentMP += healMP;
            target.HealthManager.CurrentSP += healSP;

            return result;
        }

        /// <summary>
        /// Makes target invisible.
        /// </summary>
        public AttackResult UsedStealthSkill(Skill skill, IKillable target)
        {
            target.AddActiveBuff(skill, this);
            return new AttackResult(AttackSuccess.Normal, new Damage());
        }

        /// <summary>
        /// Clears debuffs.
        /// </summary>
        public AttackResult UsedDispelSkill(Skill skill, IKillable target)
        {
            var debuffs = target.ActiveBuffs.Where(b => b.IsDebuff).ToList();
            foreach (var debuff in debuffs)
            {
                debuff.CancelBuff();
            }

            return new AttackResult(AttackSuccess.Normal, new Damage());
        }

        /// <summary>
        /// Initialize passive skills.
        /// </summary>
        public void InitPassiveSkills()
        {
            foreach (var skill in Skills.Values.Where(s => s.IsPassive && s.Type != TypeDetail.Stealth))
            {
                UseSkill(skill);
            }
        }

        /// <summary>
        /// Clears skills and adds skill points.
        /// </summary>
        public void ResetSkills()
        {
            ushort skillFactor = _characterConfig.GetLevelStatSkillPoints(LevelingManager.Grow).SkillPoint;

            SkillPoint = (ushort)(skillFactor * (LevelProvider.Level - 1));

            _taskQueue.Enqueue(ActionType.REMOVE_ALL_SKILLS, Id);
            _taskQueue.Enqueue(ActionType.SAVE_CHARACTER_SKILLPOINT, Id, SkillPoint);

            SendResetSkills();

            foreach (var passive in PassiveBuffs.ToList())
                passive.CancelBuff();

            Skills.Clear();
        }
    }
}
