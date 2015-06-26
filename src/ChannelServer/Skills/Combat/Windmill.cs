﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.Scripting.Scripts;
using Aura.Channel.Skills.Base;
using Aura.Channel.Skills.Magic;
using Aura.Channel.World.Entities;
using Aura.Mabi.Const;
using Aura.Mabi.Network;
using Aura.Shared.Network;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.Channel.Skills.Combat
{
	/// <summary>
	/// Windmill skill handler
	/// </summary>
	/// <remarks>
	/// Var 1: Damage multiplicator
	/// Var 2: ? (Life reduction? "10.0" for each rank)
	/// Var 3: ?
	/// </remarks>
	[Skill(SkillId.Windmill)]
	public class Windmill : IPreparable, IReadyable, IUseable, ICompletable, ICancelable, IInitiableSkillHandler
	{
		/// <summary>
		/// Units the enemy is knocked back.
		/// </summary>
		private const int KnockbackDistance = 450;

		/// <summary>
		/// Knock back required for WM to count as Counter.
		/// </summary>
		private const int CounterStability = 50;

		/// <summary>
		/// Subscribes to events needed for training.
		/// </summary>
		public virtual void Init()
		{
			ChannelServer.Instance.Events.CreatureAttack += this.OnCreatureAttack;
			ChannelServer.Instance.Events.CreatureAttacks += this.OnCreatureAttacks;
		}

		/// <summary>
		/// Prepares WM, empty skill init for only the loading sound.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Prepare(Creature creature, Skill skill, Packet packet)
		{
			creature.StopMove();

			Send.SkillInitEffect(creature, null);
			Send.SkillPrepare(creature, skill.Info.Id, skill.GetCastTime());

			return true;
		}

		/// <summary>
		/// Readies WM, sets stack so we know when it was used.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Ready(Creature creature, Skill skill, Packet packet)
		{
			skill.Stacks = 1;

			Send.SkillReady(creature, skill.Info.Id);

			return true;
		}

		/// <summary>
		/// Uses WM, attacking targets.
		/// </summary>
		/// <param name="attacker"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Use(Creature attacker, Skill skill, Packet packet)
		{
			if (attacker.IsOnAttackDelay)
			{
				Send.SkillUseSilentCancel(attacker);
				return;
			}
			var targetAreaId = packet.GetLong();

			// There exists a seemingly rare case where these parameters
			// aren't sent.
			var unkInt1 = (packet.Peek() != PacketElementType.None ? packet.GetInt() : 0);
			var unkInt2 = (packet.Peek() != PacketElementType.None ? packet.GetInt() : 0);

			this.Use(attacker, skill, targetAreaId, unkInt1, unkInt2);
		}

		/// <summary>
		/// Uses WM, attacking targets.
		/// </summary>
		/// <param name="attacker"></param>
		/// <param name="skill"></param>
		/// <param name="targetAreaId"></param>
		/// <param name="unkInt1"></param>
		/// <param name="unkInt2"></param>
		public void Use(Creature attacker, Skill skill, long targetAreaId = 0, int unkInt1 = 0, int unkInt2 = 0)
		{
			if(attacker.IsOnAttackDelay)
			{
				Send.SkillUseSilentCancel(attacker);
				return;
			}
			
			var range = this.GetRange(attacker, skill);

			var targets = attacker.GetTargetableCreaturesInRange(range, true);

			// Check targets
			if (targets.Count == 0)
			{
				Send.Notice(attacker, Localization.Get("There isn't a target nearby to use that on."));
				Send.SkillUseSilentCancel(attacker);
				return;
			}

			// Create actions
			var cap = new CombatActionPack(attacker, skill.Info.Id);

			var aAction = new AttackerAction(CombatActionType.SpecialHit, attacker, skill.Info.Id, targetAreaId);
			aAction.Set(AttackerOptions.Result);

			cap.Add(aAction);

			var survived = new List<Creature>();

			var skipped = new List<Creature>();

			foreach (var target in targets)
			{
				if (target.IsNotReadyToBeHit)
				{
					skipped.Add(target);
					if (skipped.Count == targets.Count)
					{
						Send.Notice(attacker, Localization.Get("There isn't a target nearby to use that on."));
						Send.SkillUseSilentCancel(attacker);
						return;
					}
					else
					{
						continue;
					}
				}

				target.StopMove();

				Skill smash = target.Skills.Get(SkillId.Smash);
				if (smash != null && target.Skills.IsReady(SkillId.Smash))
					attacker.InterceptingSkillId = SkillId.Smash;
				TargetAction tAction;
				if (attacker.InterceptingSkillId == SkillId.Smash && !target.GetPosition().InRange(attacker.GetPosition(), target.AttackRangeFor(attacker)))
				{
					aAction.Options |= AttackerOptions.Result;
					tAction = new TargetAction(CombatActionType.CounteredHit, target, attacker, SkillId.Smash);
					tAction.Options |= TargetOptions.Result;

				}
				else
				{
					tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, skill.Info.Id);
				}
				attacker.InterceptingSkillId = SkillId.None;
				tAction.Delay = 300; // Usually 300, sometimes 350?

				// Calculate damage
				var damage = attacker.GetRndTotalDamage();
				if (attacker.RightHand != null && (
				attacker.RightHand.Data.HasTag("/weapon/bow01/") ||
				attacker.RightHand.Data.HasTag("/weapon/bow/") ||
				attacker.RightHand.Data.HasTag("/weapon/crossbow/") ||
				attacker.RightHand.Data.HasTag("/weapon/shuriken/") ||
				attacker.RightHand.Data.HasTag("/weapon/atlatl/") ||
				attacker.RightHand.Data.HasTag("/weapon/gun/")))
				{
					damage = attacker.GetRndBareHandDamage();
				}
				damage *= skill.RankData.Var1 / 100f;

				// Handle skills and reductions
				CriticalHit.Handle(attacker, attacker.GetTotalCritChance(0), ref damage, tAction);
				SkillHelper.HandleDefenseProtection(target, ref damage);
				Defense.Handle(aAction, tAction, ref damage);
				ManaShield.Handle(target, ref damage, tAction);

				// Clean Hit if not defended nor critical
				if (!tAction.Is(CombatActionType.Defended) && !tAction.Has(TargetOptions.Critical))
					tAction.Set(TargetOptions.CleanHit);

				// Take damage if any is left
				if (damage > 0)
					target.TakeDamage(tAction.Damage = damage, attacker);

				// Finish if dead, knock down if not defended
				if (target.IsDead)
					tAction.Set(TargetOptions.KnockDownFinish);
				else if (!tAction.Is(CombatActionType.Defended))
					tAction.Set(TargetOptions.KnockDown);

				// Anger Management
				if (!target.IsDead)
				{
					survived.Add(target);
				}

				target.IsInBattleStance = true;

				// Stun & knock back
				aAction.Stun = CombatMastery.GetAttackerStun(attacker.AverageKnockCount, attacker.AverageAttackSpeed, true);

				if (!tAction.Is(CombatActionType.Defended))
				{
					tAction.Stun = CombatMastery.GetTargetStun(attacker.AverageKnockCount, attacker.AverageAttackSpeed, true);
					if (!target.IsDead)
					{
						if ((TargetOptions.KnockDown & tAction.Options) != 0)
						{
							//Timer for getting back up.
							System.Timers.Timer getUpTimer = new System.Timers.Timer(tAction.Stun-1000);

							getUpTimer.Elapsed += (sender, e) => { if (target != null) { target.GetBackUp(sender, e, getUpTimer); } };
							getUpTimer.Enabled = true;
						}
					}
					target.Stability = Creature.MinStability;
					attacker.Shove(target, KnockbackDistance);
				}

				// Add action
				cap.Add(tAction);
			}

			// Only select a random aggro if there is no aggro yet,
			// WM only aggroes one target at a time.
			if (survived.Count != 0 && attacker.Region.CountAggro(attacker) < 1)
			{
				var rnd = RandomProvider.Get();
				var aggroTarget = survived.Random();
				aggroTarget.Aggro(attacker);
			}

			

			// Spin it~
			Send.UseMotion(attacker, 8, 4);

			cap.Handle();

			if (skill.Info.Rank >= SkillRank.R9)
			{
				if(attacker.IsHuman)
                    skill.EndCooldownTime = DateTime.Now.AddMilliseconds(3500);
				else if (attacker.IsElf)
					skill.EndCooldownTime = DateTime.Now.AddMilliseconds(4000);
				else if (attacker.IsGiant)
					skill.EndCooldownTime = DateTime.Now.AddMilliseconds(3000);
			}
			else
			{
				if(attacker.IsHuman)
					skill.EndCooldownTime = DateTime.Now.AddMilliseconds(4000);
				else if (attacker.IsElf)
					skill.EndCooldownTime = DateTime.Now.AddMilliseconds(4500);
				else if (attacker.IsGiant)
					skill.EndCooldownTime = DateTime.Now.AddMilliseconds(3500);
			}

			Send.SkillUse(attacker, skill.Info.Id, targetAreaId, unkInt1, unkInt2);

			skill.Stacks = 0;
		}

		/// <summary>
		/// Completes WM.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Complete(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillComplete(creature, skill.Info.Id);
		}

		/// <summary>
		/// Cancels WM.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		public void Cancel(Creature creature, Skill skill)
		{
		}

		/// <summary>
		/// Calculates range based on equipment and skill rank.
		/// </summary>
		/// <param name="attacker"></param>
		/// <param name="skill"></param>
		/// <returns></returns>
		protected virtual int GetRange(Creature attacker, Skill skill)
		{
			var range = 300f;
			var knuckleMod = 0.4f;

			if (skill.Info.Rank >= SkillRank.R1)
			{
				range = 500f;
				knuckleMod = 0.6f;
			}
			else if (skill.Info.Rank >= SkillRank.R5)
			{
				range = 400f;
				knuckleMod = 0.5f;
			}
			

			if (attacker.RightHand != null && attacker.RightHand.Data.HasTag("/weapon/knuckle/"))
				range *= knuckleMod;

			return (int)range;
		}

		/// <summary>
		/// Handles WM training
		/// </summary>
		/// <param name="obj"></param>
		private void OnCreatureAttack(TargetAction tAction)
		{
			if (tAction.SkillId != SkillId.Windmill)
				return;

			var attackerSkill = tAction.Attacker.Skills.Get(SkillId.Windmill);
			if (attackerSkill == null) return;

			var rating = tAction.Attacker.GetPowerRating(tAction.Creature);
			var targets = tAction.Pack.GetTargets();

			var multipleEnemies = targets.Length >= 4;
			var multipleEnemiesDefeated = targets.Count(a => a.IsDead) >= 4;

			// rF-D, 1-2
			if (attackerSkill.Info.Rank >= SkillRank.RF && attackerSkill.Info.Rank <= SkillRank.RD)
			{
				attackerSkill.Train(1); // Attack an enemy.
				if (tAction.Creature.IsDead)
					attackerSkill.Train(2); // Defeat an enemy.
			}

			// rF, 3-5
			if (attackerSkill.Info.Rank == SkillRank.RF)
			{
				if (tAction.Attacker.Stability <= CounterStability)
					attackerSkill.Train(3); // Counterattack with Windmill.

				if (multipleEnemies) attackerSkill.Train(4); // Attack several enemies.
				if (multipleEnemiesDefeated) attackerSkill.Train(5); // Defeat several enemies.
			}

			// rE-D, 3-8
			if (attackerSkill.Info.Rank >= SkillRank.RE && attackerSkill.Info.Rank <= SkillRank.RD)
			{
				if (rating == PowerRating.Normal)
				{
					attackerSkill.Train(3); // Attack a similar ranked enemy.
					if (tAction.Creature.IsDead)
						attackerSkill.Train(4); // Defeat a similar ranked enemy.
				}

				if (rating == PowerRating.Strong && tAction.Creature.IsDead)
					attackerSkill.Train(5); // Defeat a powerful enemy.

				if (tAction.Attacker.Stability <= CounterStability)
					attackerSkill.Train(6); // Counterattack with Windmill.
			}

			// rC-B
			if (attackerSkill.Info.Rank >= SkillRank.RC && attackerSkill.Info.Rank <= SkillRank.RB)
			{
				if (rating == PowerRating.Normal)
				{
					attackerSkill.Train(1); // Attack a similar ranked enemy.
					if (tAction.Creature.IsDead)
						attackerSkill.Train(2); // Defeat a similar ranked enemy.
				}

				if (rating == PowerRating.Strong && tAction.Creature.IsDead)
					attackerSkill.Train(3); // Defeat a powerful enemy.

				if (rating == PowerRating.Awful && tAction.Creature.IsDead)
					attackerSkill.Train(4); // Defeat a very powerful enemy.

				if (tAction.Attacker.Stability <= CounterStability)
					attackerSkill.Train(5); // Counterattack with Windmill.
			}

			// rA-8
			if (attackerSkill.Info.Rank >= SkillRank.RA && attackerSkill.Info.Rank <= SkillRank.R8)
			{
				if (rating == PowerRating.Normal && tAction.Creature.IsDead)
					attackerSkill.Train(1); // Defeat a similar ranked enemy.

				if (rating == PowerRating.Strong && tAction.Creature.IsDead)
					attackerSkill.Train(2); // Defeat a powerful enemy.

				if (rating == PowerRating.Awful && tAction.Creature.IsDead)
					attackerSkill.Train(3); // Defeat a very powerful enemy.

				if (tAction.Attacker.Stability <= CounterStability)
					attackerSkill.Train(4); // Counterattack with Windmill.
			}

			// r7
			if (attackerSkill.Info.Rank == SkillRank.R7)
			{
				if (rating == PowerRating.Normal && tAction.Creature.IsDead)
					attackerSkill.Train(1); // Defeat a similar ranked enemy.

				if (rating == PowerRating.Strong && tAction.Creature.IsDead)
					attackerSkill.Train(2); // Defeat a powerful enemy.

				if (rating == PowerRating.Awful && tAction.Creature.IsDead)
					attackerSkill.Train(3); // Defeat a very powerful enemy.

				if (rating == PowerRating.Boss && tAction.Creature.IsDead)
					attackerSkill.Train(4); // Defeat a boss-level enemy.
			}

			// r6-1
			if (attackerSkill.Info.Rank >= SkillRank.R6 && attackerSkill.Info.Rank <= SkillRank.R1)
			{
				if (rating == PowerRating.Strong && tAction.Creature.IsDead)
					attackerSkill.Train(1); // Defeat a powerful enemy.

				if (rating == PowerRating.Awful && tAction.Creature.IsDead)
					attackerSkill.Train(2); // Defeat a very powerful enemy.

				if (rating == PowerRating.Boss && tAction.Creature.IsDead)
					attackerSkill.Train(3); // Defeat a boss-level enemy.
			}
		}

		/// <summary>
		/// Handles multi-target training.
		/// </summary>
		/// <remarks>
		/// Can't be handled in OnCreatureAttack because it would be done
		/// for every single target.
		/// </remarks>
		/// <param name="cap"></param>
		private void OnCreatureAttacks(AttackerAction aAction)
		{
			if (aAction.SkillId != SkillId.Windmill)
				return;

			var attackerSkill = aAction.Creature.Skills.Get(SkillId.Windmill);
			if (attackerSkill == null) return;

			var targets = aAction.Pack.GetTargets();
			var multipleEnemies = false;
			var multipleEnemiesDefeated = false;
			var trainingIdx = 4;

			switch (attackerSkill.Info.Rank)
			{
				case SkillRank.RE:
				case SkillRank.RD: trainingIdx = 7; break;
				case SkillRank.RC:
				case SkillRank.RB: trainingIdx = 6; break;
				case SkillRank.RA:
				case SkillRank.R9:
				case SkillRank.R8:
				case SkillRank.R7: trainingIdx = 5; break;
				case SkillRank.R6:
				case SkillRank.R5:
				case SkillRank.R4:
				case SkillRank.R3:
				case SkillRank.R2:
				case SkillRank.R1: trainingIdx = 4; break;
			}

			// rF, 3-5
			if (attackerSkill.Info.Rank == SkillRank.RF)
			{
				multipleEnemies = (targets.Length >= 4); // Attack several enemies.
				multipleEnemiesDefeated = (targets.Count(a => a.IsDead) >= 4); // Defeat several enemies.
			}

			// rE-D, 3-8
			if (attackerSkill.Info.Rank >= SkillRank.RE && attackerSkill.Info.Rank <= SkillRank.RD)
			{
				// "When training multiple hits/kills, the player must hit four or more targets.
				// To fulfill the "kill" condition, the player must finish all four targets simultaneously.
				// At least one must be "Strong" while the rest are either lower or equal in power or else you will not receive the points."
				// http://wiki.mabinogiworld.com/view/Windmill#Training_Method

				var matches = targets.Where(a => aAction.Creature.GetPowerRating(a) <= PowerRating.Normal);

				multipleEnemies = (matches.Count() >= 4 && matches.Any(a => aAction.Creature.GetPowerRating(a) == PowerRating.Normal)); // Attack several enemies of similar level.
				multipleEnemiesDefeated = (multipleEnemies && matches.Count(a => a.IsDead) >= 4); // Defeat several enemies of similar level.
			}

			// rC-1
			if (attackerSkill.Info.Rank >= SkillRank.RC && attackerSkill.Info.Rank <= SkillRank.R1)
			{
				var matches = targets.Where(a => aAction.Creature.GetPowerRating(a) <= PowerRating.Strong);

				multipleEnemies = (matches.Count() >= 4 && matches.Any(a => aAction.Creature.GetPowerRating(a) == PowerRating.Strong)); // Attack several powerful enemies.
				multipleEnemiesDefeated = (multipleEnemies && matches.Count(a => a.IsDead) >= 4); // Defeat several powerful enemies.
			}

			if (multipleEnemies) attackerSkill.Train(trainingIdx);
			if (multipleEnemiesDefeated) attackerSkill.Train(trainingIdx + 1);
		}
	}
}
