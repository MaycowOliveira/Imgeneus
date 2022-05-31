﻿using Imgeneus.Database.Constants;
using Imgeneus.Database.Entities;
using Imgeneus.World.Game.Attack;
using Imgeneus.World.Game.Player;
using Imgeneus.World.Game.Skills;
using System;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Imgeneus.World.Game.Buffs
{
    public class Buff
    {
        private static uint Counter = 1;

        private readonly Skill _skill;

        public uint Id { get; private set; }

        private object SyncObj = new object();

        public int CountDownInSeconds { get => (int)ResetTime.Subtract(DateTime.UtcNow).TotalSeconds; }

        public int SkillUniqueId => _skill.Id;

        public ushort SkillId => _skill.SkillId;

        public byte SkillLevel => _skill.SkillLevel;

        public StateType StateType => _skill.StateType;

        public bool IsPassive => _skill.IsPassive;

        public bool ShouldClearAfterDeath => _skill.ShouldClearAfterDeath;

        public bool CanBeActivatedAndDisactivated => _skill.CanBeActivated;

        public byte LimitHP => _skill.LimitHP;

        /// <summary>
        /// Who has created this buff.
        /// </summary>
        public IKiller BuffCreator { get; }

        public Buff(IKiller maker, Skill skill)
        {
            lock (SyncObj)
            {
                Id = Counter++;
            }

            _skill = skill;

            BuffCreator = maker;

            _resetTimer.Elapsed += ResetTimer_Elapsed;
            _periodicalHealTimer.Elapsed += PeriodicalHealTimer_Elapsed;
            _periodicalDebuffTimer.Elapsed += PeriodicalDebuffTimer_Elapsed;
        }

        #region IsDebuff

        /// <summary>
        /// Indicator, that shows if this buff is "bad".
        /// </summary>
        public bool IsDebuff
        {
            get
            {
                switch (_skill.Type)
                {
                    case TypeDetail.EnergyDrain:
                    case TypeDetail.PeriodicalDebuff:
                    case TypeDetail.SubtractingDebuff:
                    case TypeDetail.DeathTouch:
                    case TypeDetail.Stun:
                    case TypeDetail.Immobilize:
                    case TypeDetail.Sleep:
                    case TypeDetail.PreventAttack:
                    case TypeDetail.RemoveAttribute:
                    case TypeDetail.EnergyBackhole:
                    case TypeDetail.MentalStormConfusion:
                    case TypeDetail.SoulMenace:
                    case TypeDetail.MentalStormDistortion:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Is skill elemental skin buff?
        /// </summary>
        public bool IsElementalProtection
        {
            get
            {
                return _skill.Type == TypeDetail.ElementalProtection;
            }
        }

        /// <summary>
        /// Is skill elemental weapon buff?
        /// </summary>
        public bool IsElementalWeapon
        {
            get
            {
                return _skill.Type == TypeDetail.ElementalAttack;
            }
        }

        /// <summary>
        /// Is skill makes entity untouchable?
        /// </summary>
        public bool IsUntouchable
        {
            get
            {
                return _skill.Type == TypeDetail.Untouchable;
            }
        }

        /// <summary>
        /// Is skill makes invisible?
        /// </summary>
        public bool IsStealth
        {
            get
            {
                return _skill.Type == TypeDetail.Stealth;
            }
        }

        #endregion

        #region Buff reset

        private DateTime _resetTime;
        /// <summary>
        /// Time, when buff is going to turn off.
        /// </summary>
        public DateTime ResetTime
        {
            get => _resetTime;
            set
            {
                _resetTime = value;

                // Set up timer.
                _resetTimer.Stop();
                _resetTimer.Interval = _resetTime.Subtract(DateTime.UtcNow).TotalMilliseconds > int.MaxValue ? int.MaxValue : _resetTime.Subtract(DateTime.UtcNow).TotalMilliseconds;
                _resetTimer.Start();
            }
        }

        /// <summary>
        /// Timer, that is called when it's time to remove buff.
        /// </summary>
        private readonly Timer _resetTimer = new Timer();

        private void ResetTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CancelBuff();
        }

        /// <summary>
        /// Event, that is fired, when it's time to remove buff.
        /// </summary>
        public event Action<Buff> OnReset;

        /// <summary>
        /// Removes buff from character.
        /// </summary>
        public void CancelBuff()
        {
            _resetTimer.Elapsed -= ResetTimer_Elapsed;
            _resetTimer.Stop();
            _periodicalHealTimer.Elapsed -= PeriodicalHealTimer_Elapsed;
            _periodicalHealTimer.Stop();
            _periodicalDebuffTimer.Elapsed -= PeriodicalDebuffTimer_Elapsed;
            _periodicalDebuffTimer.Stop();

            OnReset?.Invoke(this);
        }

        #endregion

        #region Periodical Heal

        /// <summary>
        /// Timer, that is called when it's time to make periodical heal (every 3 seconds).
        /// </summary>
        private readonly Timer _periodicalHealTimer = new Timer(3000);

        /// <summary>
        /// Event, that is fired, when it's time to make periodical heal.
        /// </summary>
        public event Action<Buff, AttackResult> OnPeriodicalHeal;

        public ushort TimeHealHP;

        public ushort TimeHealSP;

        public ushort TimeHealMP;

        private void PeriodicalHealTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            OnPeriodicalHeal?.Invoke(this, new AttackResult(AttackSuccess.Normal, new Damage(TimeHealHP, TimeHealSP, TimeHealMP)));
        }

        /// <summary>
        /// Starts periodical healing.
        /// </summary>
        public void StartPeriodicalHeal()
        {
            _periodicalHealTimer.Start();
        }

        #endregion

        #region Periodical debuff

        /// <summary>
        /// Timer, that is called when it's time to make periodical debuff (every second).
        /// </summary>
        private readonly Timer _periodicalDebuffTimer = new Timer(1200);

        /// <summary>
        /// Event, that is fired, when it's time to make periodical debuff.
        /// </summary>
        public event Action<Buff, AttackResult> OnPeriodicalDebuff;

        public ushort TimeHPDamage;

        public ushort TimeSPDamage;

        public ushort TimeMPDamage;

        public TimeDamageType TimeDamageType;

        public int RepeatTime
        {
            set
            {
                _periodicalDebuffTimer.Interval = value * 1000;
            }
        }

        private void PeriodicalDebuffTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            OnPeriodicalDebuff?.Invoke(this, new AttackResult(AttackSuccess.Normal, new Damage(TimeHPDamage, TimeSPDamage, TimeMPDamage)));
        }

        public void StartPeriodicalDebuff()
        {
            _periodicalDebuffTimer.Start();
        }

        #endregion

        public static Buff FromDbCharacterActiveBuff(DbCharacterActiveBuff buff, DbSkill dbSkill)
        {
            return new Buff(null, new Skill(dbSkill, 0, 0))
            {
                ResetTime = buff.ResetTime
            };
        }
    }
}
