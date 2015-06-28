﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.Skills.Base;
using Aura.Channel.Skills.Magic;
using Aura.Channel.World.Entities;
using Aura.Mabi.Const;
using Aura.Mabi.Network;
using Aura.Shared.Util;
using System.Collections.Generic;

namespace Aura.Channel.Skills.Combat
{
	/// <summary>
	/// Magnum Shot handler
	/// </summary>
	/// <remarks>
	/// Var1: Damage
	/// Var4: Splash Damage
	/// Var5: Splash Distance
	/// Var6: ?
	/// </remarks>
	[Skill(SkillId.MagnumShot)]
	public class MagnumShot : ISkillHandler, IPreparable, IReadyable, ICompletable, ICancelable, ICombatSkill,
		IInitiableSkillHandler
	{
		/// <summary>
		/// Distance to knock the target back.
		/// </summary>
		private const int KnockBackDistance = 450;

		/// <summary>
		/// Stun for the attacker
		/// </summary>
		/// <remarks>
		/// There are actions with 520 stun, dependent on something?
		/// </remarks>
		private const int AttackerStun = 600;

		/// <summary>
		/// Stun for the target
		/// </summary>
		private const int TargetStun = 3000;

		/// <summary>
		/// Bonus damage for fire arrows
		/// </summary>
		public const float FireBonus = 1.5f;

		/// <summary>
		/// Sets up subscriptions for skill training.
		/// </summary>
		public void Init()
		{
			ChannelServer.Instance.Events.CreatureAttack += this.OnCreatureAttacks;
		}

		/// <summary>
		/// Prepares skill.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Prepare(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillFlashEffect(creature);
			Send.SkillPrepare(creature, skill.Info.Id, skill.GetCastTime());

			creature.Lock(Locks.Run);

			return true;
		}

		/// <summary>
		/// Readies skill, activates fire effect.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Ready(Creature creature, Skill skill, Packet packet)
		{
			creature.Temp.FireArrow = creature.Region.GetProps(a => a.Info.Id == 203 && a.GetPosition().InRange(creature.GetPosition(), 500)).Count > 0;
			if (creature.Temp.FireArrow)
				Send.Effect(creature, Effect.FireArrow, true);

			Send.SkillReady(creature, skill.Info.Id);

			return true;
		}

		/// <summary>
		/// Completes skill, stopping the aim meter and disabling the fire effect.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Complete(Creature creature, Skill skill, Packet packet)
		{
			this.Cancel(creature, skill);
			Send.SkillComplete(creature, skill.Info.Id);
		}

		/// <summary>
		/// Cancels skill, stopping the aim meter and disabling the fire effect.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		public void Cancel(Creature creature, Skill skill)
		{
			creature.AimMeter.Stop();

			// Disable fire arrow effect
			if (creature.Temp.FireArrow)
				Send.Effect(creature, Effect.FireArrow, false);
		}

		/// <summary>
		/// Uses the skill.
		/// </summary>
		/// <param name="attacker"></param>
		/// <param name="skill"></param>
		/// <param name="targetEntityId"></param>
		/// <returns></returns>
		public CombatSkillResult Use(Creature attacker, Skill skill, long targetEntityId)
		{
			// Get target
			var target = attacker.Region.GetCreature(targetEntityId);
			if (target == null)
				return CombatSkillResult.InvalidTarget;

			if (target.IsNotReadyToBeHit)
				return CombatSkillResult.Okay;

			// Actions
			var cap = new CombatActionPack(attacker, skill.Info.Id);

			var aAction = new AttackerAction(CombatActionType.RangeHit, attacker, skill.Info.Id, targetEntityId);
			aAction.Set(AttackerOptions.Result);
			aAction.Stun = AttackerStun;
			cap.Add(aAction);

			// Hit by chance
			var chance = attacker.AimMeter.GetAimChance(target);
			var rnd = RandomProvider.Get();
			if (rnd.NextDouble() * 100 < chance)
			{
				aAction.Set(AttackerOptions.KnockBackHit2);

				var tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, skill.Info.Id);
				tAction.Set(TargetOptions.Result | TargetOptions.CleanHit);
				tAction.Stun = TargetStun;
				cap.Add(tAction);

				// Damage
				var damage = this.GetDamage(attacker, skill);

				// More damage with fire arrow
				if (attacker.Temp.FireArrow)
					damage *= FireBonus;

				// Critical Hit
				var critShieldReduction = (target.LeftHand != null ? target.LeftHand.Data.DefenseBonusCrit : 0);
				var critChance = attacker.GetRightCritChance(target.Protection + critShieldReduction);
				CriticalHit.Handle(attacker, critChance, ref damage, tAction);

				var maxDamage = damage; //Damage without Defense and Protection
				// Subtract target def/prot
				SkillHelper.HandleDefenseProtection(target, ref damage);

				// Defense
				Defense.Handle(aAction, tAction, ref damage);

				// Mana Shield
				ManaShield.Handle(target, ref damage, tAction, maxDamage);

				// Deal with it!
				if (damage > 0)
					target.TakeDamage(tAction.Damage = damage, attacker);

				// TODO: We have to calculate knockback distance right
				// TODO: Target with Defense and shield shouldn't be knocked back
				attacker.Shove(target, KnockBackDistance);

				// Aggro
				target.Aggro(attacker);

				tAction.Set(TargetOptions.KnockDownFinish);

				if (target.IsDead)
				{
					aAction.Set(AttackerOptions.KnockBackHit1);
					tAction.Set(TargetOptions.Finished);
				}
				else
				{
					if ((TargetOptions.KnockDown & tAction.Options) != 0)
					{
						//Timer for getting back up.
						System.Timers.Timer getUpTimer = new System.Timers.Timer(tAction.Stun - 1000);

						getUpTimer.Elapsed += (sender, e) => { if (target != null) { target.GetBackUp(sender, e, getUpTimer); } };
						getUpTimer.Enabled = true;
					}
				}
				var weapon = attacker.RightHand;
				var critSkill = attacker.Skills.Get(SkillId.CriticalHit);
				if (skill.Info.Rank >= SkillRank.R5 && weapon != null && weapon.Data.SplashRadius != 0 && weapon.Data.SplashAngle != 0)
				{
					ICollection<Creature> targets = attacker.GetTargetableCreaturesInCone(weapon != null ? (int)weapon.Data.SplashRadius : 200, weapon != null ? (int)weapon.Data.SplashAngle : 20);
					foreach (var splashTarget in targets)
					{
						if (splashTarget != target)
						{
							if (splashTarget.IsNotReadyToBeHit)
								continue;
							TargetAction tSplashAction = new TargetAction(CombatActionType.TakeHit, splashTarget, attacker, skill.Info.Id);
							tSplashAction.Set(TargetOptions.Result | TargetOptions.CleanHit);
							splashTarget.Stun = TargetStun;

							// Base damage
							var damageSplash = this.GetDamage(attacker, skill);

							// Critical Hit

							if (critSkill != null && tAction.Has(TargetOptions.Critical))
							{
								// Add crit bonus
								var bonus = critSkill.RankData.Var1 / 100f;
								damageSplash = damageSplash + (damageSplash * bonus);

								// Set splashTarget option
								tSplashAction.Set(TargetOptions.Critical);
							}

							var maxDamageSplash = damage; //Damage without Defense and Protection
							// Subtract splashTarget def/prot
							SkillHelper.HandleDefenseProtection(splashTarget, ref damageSplash);

							// Defense
							Defense.Handle(aAction, tSplashAction, ref damageSplash, true);

							// Mana Shield
							ManaShield.Handle(splashTarget, ref damageSplash, tSplashAction, maxDamageSplash);

							//Splash Damage Reduction
							damageSplash *= skill.Info.Rank < SkillRank.R1 ? 0.1f : 0.2f;

							// Deal with it!
							if (damageSplash > 0)
								splashTarget.TakeDamage(tSplashAction.Damage = damageSplash, attacker);

							attacker.Shove(splashTarget, KnockBackDistance);

							// Alert
							Network.Sending.Send.SetCombatTarget(splashTarget, attacker.EntityId, TargetMode.Alert);

							tAction.Set(TargetOptions.KnockDownFinish);

							if (splashTarget.IsDead)
							{
								aAction.Set(AttackerOptions.KnockBackHit1);
								tSplashAction.Set(TargetOptions.Finished);
							}
							else
							{
								if ((TargetOptions.KnockDown & tSplashAction.Options) != 0)
								{
									//Timer for getting back up.
									System.Timers.Timer getUpTimer = new System.Timers.Timer(tAction.Stun - 1000);

									getUpTimer.Elapsed += (sender, e) => splashTarget.GetBackUp(sender, e, getUpTimer);
									getUpTimer.Enabled = true;
								}
							}

							cap.Add(tSplashAction);
						}

					}

				}
			}
			else
			{
				aAction.Set(AttackerOptions.Missed);
			}

			// Reduce arrows
			if (attacker.Magazine != null && !ChannelServer.Instance.Conf.World.InfiniteArrows)
				attacker.Inventory.Decrement(attacker.Magazine);

			// Disable fire arrow effect
			if (attacker.Temp.FireArrow)
				Send.Effect(attacker, Effect.FireArrow, false);

			// "Cancels" the skill
			// 800 = old load time? == aAction.Stun? Varies? Doesn't seem to be a stun.
			Send.SkillUse(attacker, skill.Info.Id, 800, 1);

			cap.Handle();

			return CombatSkillResult.Okay;
		}

		/// <summary>
		/// Returns the raw damage to be done.
		/// </summary>
		/// <param name="attacker"></param>
		/// <param name="skill"></param>
		/// <returns></returns>
		protected float GetDamage(Creature attacker, Skill skill)
		{
			var result = attacker.GetRndRangedDamage();
			result *= skill.RankData.Var1 / 100f;

			return result;
		}

		/// <summary>
		/// Handles the majority of the skill training.
		/// </summary>
		/// <param name="obj"></param>
		private void OnCreatureAttacks(TargetAction tAction)
		{
			if (tAction.SkillId != SkillId.MagnumShot)
				return;

			var attackerSkill = tAction.Attacker.Skills.Get(SkillId.MagnumShot);

			if (attackerSkill != null)
			{
				attackerSkill.Train(1); // Attack an enemy.
				if (tAction.Has(TargetOptions.Critical))
					attackerSkill.Train(2);

				if (tAction.Creature.IsDead)  // Kill an enemy.
				{
					attackerSkill.Train(3);
					if (tAction.Has(TargetOptions.Critical))
						attackerSkill.Train(4);
				}
			}
		}
	}
}
