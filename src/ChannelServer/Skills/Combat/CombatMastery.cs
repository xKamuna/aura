﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aura.Channel.Skills.Base;
using Aura.Mabi.Const;
using Aura.Channel.World.Entities;
using Aura.Shared.Util;
using Aura.Data.Database;
using Aura.Channel.World;
using Aura.Channel.Skills.Life;
using Aura.Mabi;
using Aura.Channel.Skills.Magic;
using Aura.Channel.Network.Sending;
using Aura.Data;

namespace Aura.Channel.Skills.Combat
{
	/// <summary>
	/// Combat Mastery
	/// </summary>
	/// <remarks>
	/// Normal attack for 99% of all races.
	/// </remarks>
	[Skill(SkillId.CombatMastery)]
	public class CombatMastery : ICombatSkill, IInitiableSkillHandler
	{
		/// <summary>
		/// Units an enemy is knocked back.
		/// </summary>
		private const int KnockBackDistance = 450;

		/// <summary>
		/// Subscribes skill to events needed for training.
		/// </summary>
		public void Init()
		{
			ChannelServer.Instance.Events.CreatureAttackedByPlayer += this.OnCreatureAttackedByPlayer;
		}

		/// <summary>
		/// Handles attack.
		/// </summary>
		/// <param name="attacker">The creature attacking.</param>
		/// <param name="skill">The skill being used.</param>
		/// <param name="targetEntityId">The entity id of the target.</param>
		/// <returns></returns>
		public CombatSkillResult Use(Creature attacker, Skill skill, long targetEntityId)
		{
			var target = attacker.Region.GetCreature(targetEntityId);
			if (target == null)
				return CombatSkillResult.Okay;

			if (target.IsNotReadyToBeHit)
				return CombatSkillResult.Okay;

			if ((attacker.IsStunned || attacker.IsOnAttackDelay) && attacker.InterceptingSkillId == SkillId.None)
				return CombatSkillResult.Okay;

			var attackerPosition = attacker.GetPosition();
			var targetPosition = target.GetPosition();
			if (!attacker.IgnoreAttackRange &&
				(!attackerPosition.InRange(targetPosition, attacker.AttackRangeFor(target))))
			{ return CombatSkillResult.OutOfRange; }
			if (!attacker.IgnoreAttackRange &&
				(attacker.Region.Collisions.Any(attackerPosition, targetPosition) // Check collisions between position
				|| target.Conditions.Has(ConditionsA.Invisible))) // Check visiblility (GM)
			{ return CombatSkillResult.Okay; }

			attacker.IgnoreAttackRange = false;

			//Against Smash
			Skill smash = target.Skills.Get(SkillId.Smash);
			if (smash != null && target.Skills.IsReady(SkillId.Smash) && attacker.CanAttack(target))
				attacker.InterceptingSkillId = SkillId.Smash;

			var rightWeapon = attacker.RightHand;
			var leftWeapon = attacker.Inventory.LeftHand;
			var dualWield = (rightWeapon != null && leftWeapon != null && leftWeapon.Data.WeaponType != 0 && (leftWeapon.HasTag("/weapon/edged/") || leftWeapon.HasTag("/weapon/blunt/")));


			var staminaUsage = (rightWeapon != null && rightWeapon.Data.StaminaUsage != 0 ? rightWeapon.Data.StaminaUsage : 0.7f) + (dualWield ? leftWeapon.Data.StaminaUsage : 0f);
			var lowStamina = false;
			if (attacker.Stamina < staminaUsage)
			{
				lowStamina = true;
				Send.Notice(attacker, Localization.Get("Your stamina is too low to attack properly!"));
			}
			attacker.Stamina -= staminaUsage;
			Send.StatUpdate(attacker, StatUpdateType.Private, Stat.Stamina);

			// Against Combat Mastery
			Skill combatMastery = target.Skills.Get(SkillId.CombatMastery);
			var simultaneousAttackStun = 0;
			if (attacker.InterceptingSkillId != SkillId.CombatMastery && target.InterceptingSkillId != SkillId.CombatMastery)
			{
				if (combatMastery != null && (target.Skills.ActiveSkill == null || target.Skills.ActiveSkill == combatMastery || target.Skills.IsReady(SkillId.FinalHit)) && target.IsInBattleStance && target.Target == attacker && target.AttemptingAttack && (!target.IsStunned || target.IsKnockedDown) && attacker.CanAttack(target))
				{
					var attackerStunTime = CombatMastery.GetAttackerStun(attacker, attacker.RightHand, false);
					var targetStunTime = CombatMastery.GetAttackerStun(target, target.Inventory.RightHand, false);
					if ((target.LastKnockedBackBy == attacker && target.KnockDownTime > attacker.KnockDownTime &&
						target.KnockDownTime.AddMilliseconds(targetStunTime) < DateTime.Now //If last knocked down within the time it takes for you to finish attacking.
						|| attackerStunTime > targetStunTime &&
						!Math2.Probability(((2725 - attackerStunTime) / 2500) * 100) //Probability in percentage that you will not lose.  2725 is 2500 (Slowest stun) + 225 (Fastest stun divided by two so that the fastest stun isn't 100%)
						&& !(attacker.LastKnockedBackBy == target && attacker.KnockDownTime > target.KnockDownTime && attacker.KnockDownTime.AddMilliseconds(attackerStunTime) < DateTime.Now)))
					{
						if (target.CanAttack(attacker))
						{
							target.InterceptingSkillId = SkillId.CombatMastery;
							target.IgnoreAttackRange = true;
							var skillHandler = ChannelServer.Instance.SkillManager.GetHandler<ICombatSkill>(combatMastery.Info.Id);
							if (skillHandler == null)
							{
								Log.Error("CombatMastery.Use: Target's skill handler not found for '{0}'.", combatMastery.Info.Id);
								return CombatSkillResult.Okay;
							}
							skillHandler.Use(target, combatMastery, attacker.EntityId);
							return CombatSkillResult.Okay;
						}
					}
					else
					{
						if (Math2.Probability(((2725 - attackerStunTime) / 2500) * 100)) //Probability in percentage that it will be an interception instead of a double hit.
						{
							attacker.InterceptingSkillId = SkillId.CombatMastery;
						}
						else
						{
							attacker.InterceptingSkillId = SkillId.CombatMastery;
							if (target.CanAttack(attacker))
							{
								target.InterceptingSkillId = SkillId.CombatMastery;
								target.IgnoreAttackRange = true;
								var skillHandler = ChannelServer.Instance.SkillManager.GetHandler<ICombatSkill>(combatMastery.Info.Id);
								if (skillHandler == null)
								{
									Log.Error("CombatMastery.Use: Target's skill handler not found for '{0}'.", combatMastery.Info.Id);
								}
								else
								{
									skillHandler.Use(target, combatMastery, attacker.EntityId);
									simultaneousAttackStun = attacker.Stun;
									attacker.Stun = 0;
								}
							}
						}
					}
				}
			}

			attacker.StopMove();
			targetPosition = target.StopMove();

			// Counter
			if (Counterattack.Handle(target, attacker))
				return CombatSkillResult.Okay;

			ICollection<Creature> targets = null;
			if (rightWeapon != null && rightWeapon.Data.SplashRadius != 0 && rightWeapon.Data.SplashAngle != 0 || rightWeapon == null)
			{
				targets = attacker.GetTargetableCreaturesInCone(rightWeapon != null ? (int)rightWeapon.Data.SplashRadius : 204, rightWeapon != null ? (int)rightWeapon.Data.SplashAngle : 60);

				foreach (var splashTarget in targets)
				{
					if (splashTarget != target)
					{
						// Counter
						if (Counterattack.Handle(target, attacker))
							return CombatSkillResult.Okay;
					}

				}
			}

			var magazine = attacker.Inventory.Magazine;
			var maxHits = (byte)(dualWield ? 2 : 1);
			int prevId = 0;

			var defenseStun = 0;
			for (byte i = 1; i <= maxHits; ++i)
			{
				var weapon = (i == 1 ? rightWeapon : leftWeapon);
				var weaponIsKnuckle = (weapon != null && weapon.Data.HasTag("/knuckle/"));

				AttackerAction aAction;
				TargetAction tAction;
				if (attacker.InterceptingSkillId == SkillId.Smash)
				{
					aAction = new AttackerAction(CombatActionType.SimultaneousHit, attacker, SkillId.CombatMastery, target.EntityId);
					aAction.Options |= AttackerOptions.Result;
					tAction = new TargetAction(CombatActionType.CounteredHit, target, attacker, SkillId.Smash);
					tAction.Options |= TargetOptions.Result;

				}
				else if (attacker.InterceptingSkillId == SkillId.CombatMastery)
				{
					aAction = new AttackerAction(CombatActionType.SimultaneousHit, attacker, SkillId.CombatMastery, target.EntityId);
					aAction.Options |= AttackerOptions.Result;
					tAction = new TargetAction(CombatActionType.CounteredHit, target, attacker, target.Skills.IsReady(SkillId.FinalHit) ? SkillId.FinalHit : SkillId.CombatMastery);
					tAction.Options |= TargetOptions.Result;

				}
				else
				{
					aAction = new AttackerAction(CombatActionType.Hit, attacker, skill.Info.Id, targetEntityId);
					tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, target.Skills.IsReady(SkillId.FinalHit) ? SkillId.FinalHit : SkillId.CombatMastery);
					aAction.Options |= AttackerOptions.Result;
					tAction.Options |= TargetOptions.Result;
				}

				attacker.InterceptingSkillId = SkillId.None;

				var cap = new CombatActionPack(attacker, skill.Info.Id, tAction, aAction);
				cap.Hit = i;
				cap.MaxHits = maxHits;
				cap.PrevId = prevId;
				prevId = cap.Id;

				// Default attacker options
				aAction.Set(AttackerOptions.Result);
				if (dualWield)
					aAction.Set(AttackerOptions.DualWield);

				// Base damage
				var damage = (i == 1 ? attacker.GetRndRightHandDamage() : attacker.GetRndLeftHandDamage());
				if (lowStamina)
				{
					damage = attacker.GetRndBareHandDamage();
				}

				// Critical Hit
				var critShieldReduction = (target.LeftHand != null ? target.LeftHand.Data.DefenseBonusCrit : 0);
				var critChance = (i == 1 ? attacker.GetRightCritChance(target.Protection + critShieldReduction) : attacker.GetLeftCritChance(target.Protection + critShieldReduction));
				CriticalHit.Handle(attacker, critChance, ref damage, tAction);

				var maxDamage = damage; //Damage without Defense and Protection
										// Subtract target def/prot
				SkillHelper.HandleDefenseProtection(target, ref damage);

				// Defense
				var tActionOldType = tAction.Type;
				Defense.Handle(aAction, tAction, ref damage);
				if (i == 1 && tAction.Type == CombatActionType.Defended)
				{
					defenseStun = tAction.Stun;
				}


				// Mana Shield
				ManaShield.Handle(target, ref damage, tAction, maxDamage);

				// Deal with it!
				if (damage > 0)
					target.TakeDamage(tAction.Damage = damage, attacker);

				if (tAction.Type == CombatActionType.Defended && target.Life <= 0)
				{
					tAction.Type = tActionOldType;
				}

				// Aggro
				target.Aggro(attacker);

				// Evaluate caused damage
				if (!target.IsDead)
				{
					if (tAction.Type != CombatActionType.Defended)
					{
						if (!target.Skills.IsReady(SkillId.FinalHit))
						{
							target.Stability -= this.GetStabilityReduction(attacker, weapon) / maxHits;

							// React normal for CombatMastery, knock down if 
							// FH and not dual wield, don't knock at all if dual.
							if (skill.Info.Id != SkillId.FinalHit)
							{
								// Originally we thought you knock enemies back, unless it's a critical
								// hit, but apparently you knock *down* under normal circumstances.
								// More research to be done.
								if (target.IsUnstable && target.Is(RaceStands.KnockBackable))
									//tAction.Set(tAction.Has(TargetOptions.Critical) ? TargetOptions.KnockDown : TargetOptions.KnockBack);
									tAction.Set(TargetOptions.KnockDown);
								if (target.Life < 0)
									tAction.Set(TargetOptions.KnockDown);
							}
							else if (!dualWield && !weaponIsKnuckle)
							{
								target.Stability = Creature.MinStability;
								tAction.Set(TargetOptions.KnockDown);
							}
						}
					}
				}
				else
				{
					tAction.Set(TargetOptions.FinishingKnockDown);
				}

				// React to knock back
				if (tAction.IsKnockBack && tAction.Type != CombatActionType.Defended)
				{
					if (!target.Skills.IsReady(SkillId.FinalHit))
					{
						attacker.Shove(target, KnockBackDistance);
					}

					aAction.Set(AttackerOptions.KnockBackHit2);

					// Remove dual wield option if last hit doesn't come from
					// the second weapon.
					if (cap.MaxHits != cap.Hit)
						aAction.Options &= ~AttackerOptions.DualWield;


				}
				else if (tAction.Type == CombatActionType.Defended)
				{
					// Remove dual wield option if last hit doesn't come from
					// the second weapon.
					if (cap.MaxHits != cap.Hit)
						aAction.Options &= ~AttackerOptions.DualWield;
				}


				// Set stun time
				if (tAction.Type != CombatActionType.Defended)
				{
					if (simultaneousAttackStun == 0)
					{
						aAction.Stun = GetAttackerStun(attacker, weapon, tAction.IsKnockBack && ((skill.Info.Id != SkillId.FinalHit) && !target.IsDead || AuraData.FeaturesDb.IsEnabled("CombatSystemRenewal")));
					}
					else
					{
						aAction.Stun = (short)simultaneousAttackStun;
					}
					if (!target.Skills.IsReady(SkillId.FinalHit))
					{
						tAction.Stun = GetTargetStun(attacker, weapon, tAction.IsKnockBack);
					}

					if (target.IsDead && skill.Info.Id != SkillId.FinalHit)
					{
						attacker.AttackDelayTime = DateTime.Now.AddMilliseconds(GetAttackerStun(attacker, weapon, true));
					}
				}

				// Second hit doubles stun time for normal hits
				if (cap.Hit == 2 && !tAction.IsKnockBack)
					aAction.Stun *= 2;

				// Update current weapon
				SkillHelper.UpdateWeapon(attacker, target, weapon);

				var critSkill = attacker.Skills.Get(SkillId.CriticalHit);
				if (weapon != null && weapon.Data.SplashRadius != 0 && weapon.Data.SplashAngle != 0)
				{
					foreach (var splashTarget in targets)
					{
						if (splashTarget != target)
						{
							if (splashTarget.IsNotReadyToBeHit)
								continue;
							TargetAction tSplashAction = new TargetAction(CombatActionType.TakeHit, splashTarget, attacker, skill.Info.Id);

							// Base damage
							float damageSplash;
							if (lowStamina)
							{
								damageSplash = attacker.GetRndBareHandDamage();
							}
							else
							{
								damageSplash = (i == 1 ? attacker.GetRndRightHandDamage() : attacker.GetRndLeftHandDamage());
                            }
							attacker.CalculateSplashDamage(splashTarget, ref damageSplash, skill, critSkill, aAction, tAction, tSplashAction, weapon);

							// Deal with it!
							if (damageSplash > 0)
								splashTarget.TakeDamage(tSplashAction.Damage = damageSplash, attacker);

							// Alert
							splashTarget.Aggro(attacker, true);

							// Evaluate caused damage
							if (!splashTarget.IsDead)
							{
								if (tSplashAction.Type != CombatActionType.Defended)
								{
									if (!splashTarget.Skills.IsReady(SkillId.FinalHit))
									{

										splashTarget.Stability -= (this.GetStabilityReduction(attacker, weapon) / maxHits) / 2;  //Less stability reduction for splash damage.

										// React normal for CombatMastery, knock down if 
										// FH and not dual wield, don't knock at all if dual.
										if (skill.Info.Id != SkillId.FinalHit)
										{
											// Originally we thought you knock enemies back, unless it's a critical
											// hit, but apparently you knock *down* under normal circumstances.
											// More research to be done.
											if (splashTarget.IsUnstable && splashTarget.Is(RaceStands.KnockBackable))
												//tSplashAction.Set(tSplashAction.Has(TargetOptions.Critical) ? TargetOptions.KnockDown : TargetOptions.KnockBack);
												tSplashAction.Set(TargetOptions.KnockDown);
											if (splashTarget.Life < 0)
												tSplashAction.Set(TargetOptions.KnockDown);
										}
										else if (!dualWield && !weaponIsKnuckle)
										{
											splashTarget.Stability = Creature.MinStability;
											tSplashAction.Set(TargetOptions.KnockDown);
										}
									}
								}
							}
							else
							{
								tSplashAction.Set(TargetOptions.FinishingKnockDown);
							}

							// React to knock back
							if (tSplashAction.IsKnockBack && tSplashAction.Type != CombatActionType.Defended)
							{

								if (!splashTarget.Skills.IsReady(SkillId.FinalHit))
								{
									attacker.Shove(splashTarget, KnockBackDistance);
								}
							}


							// Set stun time
							if (tSplashAction.Type != CombatActionType.Defended)
							{
								if (!splashTarget.Skills.IsReady(SkillId.FinalHit))
								{
									if (defenseStun != 0)
										tSplashAction.Stun = (short)defenseStun;
									else
										tSplashAction.Stun = GetTargetStun(attacker, weapon, tSplashAction.IsKnockBack);
								}
							}

							cap.Add(tSplashAction);
						}

					}
				}

				cap.Handle();

				// No second hit if target was knocked back or defended.
				if (tAction.IsKnockBack || tAction.Type == CombatActionType.Defended)
					break;
			}
			attacker.AttemptingAttack = false;
			return CombatSkillResult.Okay;
		}

		/// <summary>
		/// Returns stun time for the attacker.
		/// </summary>
		/// <param name="weapon"></param>
		/// <param name="knockback"></param>
		/// <returns></returns>
		public static short GetAttackerStun(Creature creature, Item weapon, bool knockback)
		{
			var count = weapon != null && (weapon.HasTag("/weapon/") || weapon.HasTag("/ego_weapon/") || weapon.HasTag("/instrument/")) ? weapon.Info.KnockCount + 1 : creature.RaceData.KnockCount + 1;
			var speed = weapon != null && (weapon.HasTag("/weapon/") || weapon.HasTag("/ego_weapon/") || weapon.HasTag("/instrument/")) ? (AttackSpeed)weapon.Data.AttackSpeed : (AttackSpeed)creature.RaceData.AttackSpeed;

			return GetAttackerStun(count, speed, knockback);
		}

		/// <summary>
		/// Returns stun time for the attacker.
		/// </summary>
		/// <param name="count"></param>
		/// <param name="speed"></param>
		/// <param name="knockback"></param>
		/// <returns></returns>
		public static short GetAttackerStun(int count, AttackSpeed speed, bool knockback)
		{
			if (knockback)
				return 2500;

			// Speeds commented with "?" weren't logged, but taken from the weapon data.
			// Stun *seems* to always be the same, needs confirmation. Except for 1-hit,
			// which is always knock-back.

			switch (count)
			{
				case 1:
					switch (speed)
					{
						case AttackSpeed.VerySlow: return 2500;
					}
					break;

				case 2:
					switch (speed)
					{
						case AttackSpeed.VerySlow: return 1000;
						case AttackSpeed.Slow: return 800;
						case AttackSpeed.Normal: return 600; // ?
						case AttackSpeed.Fast: return 520; // ?
						case AttackSpeed.VeryFast: return 450; // ?
					}
					break;

				case 3:
					switch (speed)
					{
						case AttackSpeed.VerySlow: return 1000;
						case AttackSpeed.Slow: return 800;
						case AttackSpeed.Normal: return 600;
						case AttackSpeed.Fast: return 520;
						case AttackSpeed.VeryFast: return 450; // ?
					}
					break;

				case 4:
					switch (speed)
					{
						case AttackSpeed.VerySlow: return 1000; // ?
						case AttackSpeed.Slow: return 800; // ?
						case AttackSpeed.Normal: return 600; // ?
						case AttackSpeed.Fast: return 520; // ?
						case AttackSpeed.VeryFast: return 450; // ?
					}
					break;

				case 5:
					switch (speed)
					{
						case AttackSpeed.VerySlow: return 1000; // ?
						case AttackSpeed.Slow: return 800; // ?
						case AttackSpeed.Normal: return 600; // ?
						case AttackSpeed.Fast: return 520; // ?
						case AttackSpeed.VeryFast: return 450;
					}
					break;
			}

			Log.Unimplemented("GetAttackerStun: Combination {0} {1} Hit", speed, count);

			return 600;
		}

		/// <summary>
		/// Returns stun time for the target.
		/// </summary>
		/// <param name="weapon"></param>
		/// <param name="knockback"></param>
		/// <returns></returns>
		public static short GetTargetStun(Creature creature, Item weapon, bool knockback)
		{
			var count = weapon != null && (weapon.HasTag("/weapon/") || weapon.HasTag("/ego_weapon/") || weapon.HasTag("/instrument/")) ? weapon.Info.KnockCount + 1 : creature.RaceData.KnockCount + 1;
			var speed = weapon != null && (weapon.HasTag("/weapon/") || weapon.HasTag("/ego_weapon/") || weapon.HasTag("/instrument/")) ? (AttackSpeed)weapon.Data.AttackSpeed : (AttackSpeed)creature.RaceData.AttackSpeed;

			return GetTargetStun(count, speed, knockback);
		}

		/// <summary>
		/// Returns stun time for the target.
		/// </summary>
		/// <param name="count"></param>
		/// <param name="speed"></param>
		/// <param name="knockback"></param>
		/// <returns></returns>
		public static short GetTargetStun(int count, AttackSpeed speed, bool knockback)
		{
			if (knockback)
				return 3000;

			// Speeds commented with "?" weren't logged, but taken from the weapon data.

			switch (count)
			{
				case 1:
					switch (speed)
					{
						case AttackSpeed.VerySlow: return 3000;
					}
					break;

				case 2:
					switch (speed)
					{
						case AttackSpeed.VerySlow: return 3000;
						case AttackSpeed.Slow: return 2800;
						case AttackSpeed.Normal: return 2600; // ?
						case AttackSpeed.Fast: return 2400; // ?
						case AttackSpeed.VeryFast: return 2200; // ?
					}
					break;

				case 3:
					switch (speed)
					{
						case AttackSpeed.VerySlow: return 2200;
						case AttackSpeed.Slow: return 2100;
						case AttackSpeed.Normal: return 2000;
						case AttackSpeed.Fast: return 1700;
						case AttackSpeed.VeryFast: return 1500; // ?
					}
					break;

				case 4:
					switch (speed)
					{
						case AttackSpeed.VerySlow: return 1900; // ?
						case AttackSpeed.Slow: return 1800; // ?
						case AttackSpeed.Normal: return 1700; // ?
						case AttackSpeed.Fast: return 1500; // ?
						case AttackSpeed.VeryFast: return 1300; // ?
					}
					break;

				case 5:
					switch (speed)
					{
						case AttackSpeed.VerySlow: return 1700; // ?
						case AttackSpeed.Slow: return 1600; // ?
						case AttackSpeed.Normal: return 1500; // ?
						case AttackSpeed.Fast: return 1400; // ?
						case AttackSpeed.VeryFast: return 1200;
					}
					break;
			}

			Log.Unimplemented("GetTargetStun: Combination {0} {1} Hit", speed, count);

			return 2000;
		}

		/// <summary>
		/// Returns stability reduction for creature and weapon.
		/// </summary>
		/// <remarks>
		/// http://wiki.mabinogiworld.com/view/Knock_down_gauge#Knockdown_Timer_Rates
		/// </remarks>
		/// <param name="weapon"></param>
		/// <returns></returns>
		public float GetStabilityReduction(Creature creature, Item weapon)
		{
			var count = weapon != null && (weapon.HasTag("/weapon/") || weapon.HasTag("/ego_weapon/") || weapon.HasTag("/instrument/")) ? weapon.Info.KnockCount + 1 : creature.RaceData.KnockCount + 1;
			var speed = weapon != null && (weapon.HasTag("/weapon/") || weapon.HasTag("/ego_weapon/") || weapon.HasTag("/instrument/")) ? (AttackSpeed)weapon.Data.AttackSpeed : (AttackSpeed)creature.RaceData.AttackSpeed;

			// All values have been taken from the weapons data, the values in
			// comments were estimates, mainly based on logs.

			switch (count)
			{
				default:
				case 1:
					return 105;

				case 2:
					switch (speed)
					{
						default:
						case AttackSpeed.VerySlow: return 67; // 70
						case AttackSpeed.Slow: return 65; // 68
						case AttackSpeed.Normal: return 65; // 68
						case AttackSpeed.Fast: return 65; // 68
						case AttackSpeed.VeryFast: return 65;
					}

				case 3:
					switch (speed)
					{
						default:
						case AttackSpeed.VerySlow: return 55; // 60
						case AttackSpeed.Slow: return 52; // 56
						case AttackSpeed.Normal: return 50; // 53
						case AttackSpeed.Fast: return 49; // 50
						case AttackSpeed.VeryFast: return 48;
					}

				case 4:
					switch (speed)
					{
						default:
						case AttackSpeed.VerySlow: return 42;
						case AttackSpeed.Slow: return 40;
						case AttackSpeed.Normal: return 39;
						case AttackSpeed.Fast: return 36;
						case AttackSpeed.VeryFast: return 37;
					}

				case 5:
					switch (speed)
					{
						default:
						case AttackSpeed.VerySlow: return 36;
						case AttackSpeed.Slow: return 33;
						case AttackSpeed.Normal: return 31.5f;
						case AttackSpeed.Fast: return 30; // 40
						case AttackSpeed.VeryFast: return 29.5f; // 35
					}
			}
		}

		/// <summary>
		/// Training, called when someone attacks something.
		/// </summary>
		/// <param name="action"></param>
		public void OnCreatureAttackedByPlayer(TargetAction action)
		{
			// Get skill
			var attackerSkill = action.Attacker.Skills.Get(SkillId.CombatMastery);
			if (attackerSkill == null) return; // Should be impossible.
			var targetSkill = action.Creature.Skills.Get(SkillId.CombatMastery);
			if (targetSkill == null) return; // Should be impossible.

			var rating = action.Attacker.GetPowerRating(action.Creature);
			var targetRating = action.Creature.GetPowerRating(action.Attacker);

			// TODO: Check for multiple hits...?

			// Learning by attacking
			switch (attackerSkill.Info.Rank)
			{
				case SkillRank.Novice:
					attackerSkill.Train(1); // Attack Anything.
					break;

				case SkillRank.RF:
					attackerSkill.Train(1); // Attack anything.
					attackerSkill.Train(2); // Attack an enemy.
					if (action.IsKnockBack) attackerSkill.Train(3); // Knock down an enemy with multiple hits.
					if (action.Creature.IsDead) attackerSkill.Train(4); // Kill an enemy.
					break;

				case SkillRank.RE:
					if (rating == PowerRating.Normal) attackerSkill.Train(3); // Attack a same level enemy.

					if (action.IsKnockBack)
					{
						attackerSkill.Train(1); // Knock down an enemy with multiple hits.
						if (rating == PowerRating.Normal) attackerSkill.Train(4); // Knockdown a same level enemy.
						if (rating == PowerRating.Strong) attackerSkill.Train(7); // Knockdown a strong enemy.
					}

					if (action.Creature.IsDead)
					{
						attackerSkill.Train(2); // Kill an enemy.
						if (rating == PowerRating.Normal) attackerSkill.Train(6); // Kill a same level enemy.
						if (rating == PowerRating.Strong) attackerSkill.Train(8); // Kill a strong enemy.
					}

					break;

				case SkillRank.RD:
					attackerSkill.Train(1); // Attack an enemy.
					if (rating == PowerRating.Normal) attackerSkill.Train(4); // Attack a same level enemy.

					if (action.IsKnockBack)
					{
						attackerSkill.Train(2); // Knock down an enemy with multiple hits.
						if (rating == PowerRating.Normal) attackerSkill.Train(5); // Knockdown a same level enemy.
						if (rating == PowerRating.Strong) attackerSkill.Train(7); // Knockdown a strong enemy.
					}

					if (action.Creature.IsDead)
					{
						attackerSkill.Train(3); // Kill an enemy.
						if (rating == PowerRating.Normal) attackerSkill.Train(6); // Kill a same level enemy.
						if (rating == PowerRating.Strong) attackerSkill.Train(8); // Kill a strong enemy.
					}

					break;

				case SkillRank.RC:
				case SkillRank.RB:
					if (rating == PowerRating.Normal) attackerSkill.Train(1); // Attack a same level enemy.

					if (action.IsKnockBack)
					{
						if (rating == PowerRating.Normal) attackerSkill.Train(2); // Knockdown a same level enemy.
						if (rating == PowerRating.Strong) attackerSkill.Train(4); // Knockdown a strong level enemy.
						if (rating == PowerRating.Awful) attackerSkill.Train(6); // Knockdown an awful level enemy.
					}

					if (action.Creature.IsDead)
					{
						if (rating == PowerRating.Normal) attackerSkill.Train(3); // Kill a same level enemy.
						if (rating == PowerRating.Strong) attackerSkill.Train(5); // Kill a strong level enemy.
						if (rating == PowerRating.Awful) attackerSkill.Train(7); // Kill an awful level enemy.
					}

					break;

				case SkillRank.RA:
				case SkillRank.R9:
					if (action.IsKnockBack)
					{
						if (rating == PowerRating.Normal) attackerSkill.Train(1); // Knockdown a same level enemy.
						if (rating == PowerRating.Strong) attackerSkill.Train(3); // Knockdown a strong level enemy.
						if (rating == PowerRating.Awful) attackerSkill.Train(5); // Knockdown an awful level enemy.
					}

					if (action.Creature.IsDead)
					{
						if (rating == PowerRating.Normal) attackerSkill.Train(2); // Kill a same level enemy.
						if (rating == PowerRating.Strong) attackerSkill.Train(4); // Kill a strong level enemy.
						if (rating == PowerRating.Awful) attackerSkill.Train(6); // Kill an awful level enemy.
					}

					break;

				case SkillRank.R8:
					if (action.IsKnockBack)
					{
						if (rating == PowerRating.Normal) attackerSkill.Train(1); // Knockdown a same level enemy.
						if (rating == PowerRating.Strong) attackerSkill.Train(3); // Knockdown a strong level enemy.
						if (rating == PowerRating.Awful) attackerSkill.Train(5); // Knockdown an awful level enemy.
						if (rating == PowerRating.Boss) attackerSkill.Train(7); // Knockdown a boss level enemy.
					}

					if (action.Creature.IsDead)
					{
						if (rating == PowerRating.Normal) attackerSkill.Train(2); // Kill a same level enemy.
						if (rating == PowerRating.Strong) attackerSkill.Train(4); // Kill a strong level enemy.
						if (rating == PowerRating.Awful) attackerSkill.Train(6); // Kill an awful level enemy.
						if (rating == PowerRating.Boss) attackerSkill.Train(8); // Kill a boss level enemy.
					}

					break;

				case SkillRank.R7:
					if (action.IsKnockBack)
					{
						if (rating == PowerRating.Strong) attackerSkill.Train(2); // Knockdown a strong level enemy.
						if (rating == PowerRating.Awful) attackerSkill.Train(4); // Knockdown an awful level enemy.
						if (rating == PowerRating.Boss) attackerSkill.Train(6); // Knockdown a boss level enemy.
					}

					if (action.Creature.IsDead)
					{
						if (rating == PowerRating.Normal) attackerSkill.Train(1); // Kill a same level enemy.
						if (rating == PowerRating.Strong) attackerSkill.Train(3); // Kill a strong level enemy.
						if (rating == PowerRating.Awful) attackerSkill.Train(5); // Kill an awful level enemy.
						if (rating == PowerRating.Boss) attackerSkill.Train(7); // Kill a boss level enemy.
					}

					break;

				case SkillRank.R6:
				case SkillRank.R5:
				case SkillRank.R4:
				case SkillRank.R3:
				case SkillRank.R2:
				case SkillRank.R1:
					if (action.IsKnockBack)
					{
						if (rating == PowerRating.Strong) attackerSkill.Train(1); // Knockdown a strong level enemy.
						if (rating == PowerRating.Awful) attackerSkill.Train(3); // Knockdown an awful level enemy.
						if (rating == PowerRating.Boss) attackerSkill.Train(5); // Knockdown a boss level enemy.
					}

					if (action.Creature.IsDead)
					{
						if (rating == PowerRating.Strong) attackerSkill.Train(2); // Kill a strong level enemy.
						if (rating == PowerRating.Awful) attackerSkill.Train(4); // Kill an awful level enemy.
						if (rating == PowerRating.Boss) attackerSkill.Train(6); // Kill a boss level enemy.
					}

					break;
			}

			// Learning by being attacked
			switch (targetSkill.Info.Rank)
			{
				case SkillRank.RF:
					if (action.IsKnockBack) targetSkill.Train(5); // Learn something by falling down.
					if (action.Creature.IsDead) targetSkill.Train(6); // Learn through losing.
					break;

				case SkillRank.RE:
					if (action.IsKnockBack) targetSkill.Train(5); // Get knocked down. 
					break;

				case SkillRank.RD:
					if (targetRating == PowerRating.Strong) targetSkill.Train(9); // Get hit by an awful level enemy.
					break;
			}
		}
	}
}
