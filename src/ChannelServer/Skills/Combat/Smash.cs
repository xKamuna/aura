// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.Skills.Base;
using Aura.Channel.Skills.Magic;
using Aura.Channel.World;
using Aura.Channel.World.Entities;
using Aura.Data.Database;
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
	[Skill(SkillId.Smash)]
	public class Smash : CombatSkillHandler, IInitiableSkillHandler
	{
		/// <summary>
		/// Stuntime in ms for attacker and target.
		/// </summary>
		private const int StunTime = 3000;

		/// <summary>
		/// Stuntime in ms after usage...?
		/// (Really? Then what's that ^?)
		/// </summary>
		private const int AfterUseStun = 600;

		/// <summary>
		/// Units the enemy is knocked back.
		/// </summary>
		private const int KnockbackDistance = 450;

		/// <summary>
		/// Subscribes handlers to events required for training.
		/// </summary>
		public void Init()
		{
			ChannelServer.Instance.Events.CreatureAttackedByPlayer += this.OnCreatureAttackedByPlayer;
		}

		/// <summary>
		/// Prepares skill, called to start casting it.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public override bool Prepare(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillFlashEffect(creature);
			Send.SkillPrepare(creature, skill.Info.Id, skill.GetCastTime());

			return true;
		}

		/// <summary>
		/// Readies skill, called when casting is done.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public override bool Ready(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillReady(creature, skill.Info.Id);

			return true;
		}

		/// <summary>
		/// Completes skill usage, called after it was used successfully.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public override void Complete(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillComplete(creature, skill.Info.Id);
		}

		/// <summary>
		/// Handles skill usage.
		/// </summary>
		/// <param name="attacker"></param>
		/// <param name="skill"></param>
		/// <param name="targetEntityId"></param>
		/// <returns></returns>
		public override CombatSkillResult Use(Creature attacker, Skill skill, long targetEntityId)
		{
			// Check target
			var target = attacker.Region.GetCreature(targetEntityId);
			if (target == null)
				return CombatSkillResult.InvalidTarget;

			if (target.IsNotReadyToBeHit)
				return CombatSkillResult.Okay;

			// Check range
			var targetPosition = target.GetPosition();
			if (!attacker.GetPosition().InRange(targetPosition, attacker.AttackRangeFor(target)) && !attacker.IgnoreAttackRange)
				return CombatSkillResult.OutOfRange;
			attacker.IgnoreAttackRange = false;
			// Against Normal Attack
			Skill combatMastery = target.Skills.Get(SkillId.CombatMastery);
			if (combatMastery != null && (target.Skills.ActiveSkill == null || target.Skills.ActiveSkill == combatMastery || target.Skills.IsReady(SkillId.FinalHit)) && target.IsInBattleStance && target.Target == attacker && target.AttemptingAttack && !target.IsStunned)
			{
				target.InterceptingSkillId = SkillId.Smash;
				target.IgnoreAttackRange = true;
				var skillHandler = ChannelServer.Instance.SkillManager.GetHandler<ICombatSkill>(combatMastery.Info.Id);
				if (skillHandler == null)
				{
					Log.Error("Smash.Use: Target's skill handler not found for '{0}'.", combatMastery.Info.Id);
					return CombatSkillResult.Okay;
				}
				skillHandler.Use(target, combatMastery, attacker.EntityId);
				return CombatSkillResult.Okay;
			}

			// Against Windmill
			//TODO: Change this into the new NPC client system when it comes out.
			Skill windmill = target.Skills.Get(SkillId.Windmill);
			PlayerCreature player = attacker as PlayerCreature;
			if (windmill != null && target.Skills.IsReady(SkillId.Windmill) && player == null)
			{
				target.InterceptingSkillId = SkillId.Smash;
				var skillHandler = ChannelServer.Instance.SkillManager.GetHandler<IUseable>(windmill.Info.Id) as Windmill;
				if (skillHandler == null)
				{
					Log.Error("Smash.Use: Target's skill handler not found for '{0}'.", windmill.Info.Id);
					return CombatSkillResult.Okay;
				}
				skillHandler.Use(target, windmill);
				return CombatSkillResult.Okay;
			}

			// Against Smash
			Skill smash = target.Skills.Get(SkillId.Smash);
			
            if (smash != null && target.Skills.IsReady(SkillId.Smash) && target.IsInBattleStance && target.Target == attacker && !target.IsStunned)
			{
				var attackerStunTime = CombatMastery.GetAttackerStun(attacker, attacker.Inventory.RightHand, false);
				var targetStunTime = CombatMastery.GetAttackerStun(target, target.Inventory.RightHand, false);
				if ((target.LastKnockedBackBy == attacker && target.KnockDownTime > attacker.KnockDownTime || attackerStunTime > targetStunTime && !(attacker.LastKnockedBackBy == target && attacker.KnockDownTime > target.KnockDownTime)))
				{
					target.InterceptingSkillId = SkillId.Smash;
					target.IgnoreAttackRange = true;
					var skillHandler = ChannelServer.Instance.SkillManager.GetHandler<ICombatSkill>(smash.Info.Id);
					if (skillHandler == null)
					{
						Log.Error("Smash.Use: Target's skill handler not found for '{0}'.", smash.Info.Id);
						return CombatSkillResult.Okay;
					}
					skillHandler.Use(target, smash, attacker.EntityId);
					return CombatSkillResult.Okay;
				}
				else
				{
					attacker.InterceptingSkillId = SkillId.Smash;
				}
			}

			// Stop movement
			attacker.StopMove();
			target.StopMove();

			

			target.IgnoreAttackRange = false;

			// Counter
			if (Counterattack.Handle(target, attacker))
				return CombatSkillResult.Okay;

			// Prepare combat actions
			var aAction = new AttackerAction(CombatActionType.HardHit, attacker, skill.Info.Id, targetEntityId);
			aAction.Set(AttackerOptions.Result | AttackerOptions.KnockBackHit2);

			

			TargetAction tAction;
			if (attacker.InterceptingSkillId == SkillId.Smash)
			{
				aAction.Options |= AttackerOptions.Result;
				tAction = new TargetAction(CombatActionType.CounteredHit, target, attacker, SkillId.Smash);

			}
			else
			{
				tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, skill.Info.Id);
			}
			tAction.Set(TargetOptions.Result | TargetOptions.Smash);

			attacker.InterceptingSkillId = SkillId.None;

			var cap = new CombatActionPack(attacker, skill.Info.Id, tAction, aAction);

			// Calculate damage
			var damage = this.GetDamage(attacker, skill);
			var critChance = this.GetCritChance(attacker, target, skill);

			// Critical Hit
			CriticalHit.Handle(attacker, critChance, ref damage, tAction);

			// Subtract target def/prot
			SkillHelper.HandleDefenseProtection(target, ref damage);

			// Mana Shield
			ManaShield.Handle(target, ref damage, tAction);

			// Apply damage
			target.TakeDamage(tAction.Damage = damage, attacker);

			// Aggro
			target.Aggro(attacker);

			if (target.IsDead)
				tAction.Set(TargetOptions.FinishingHit | TargetOptions.Finished);

			// Set Stun/Knockback
			attacker.Stun = aAction.Stun = StunTime;
			target.Stun = tAction.Stun = StunTime;
			target.Stability = Creature.MinStability;

			if (!target.IsDead)
			{
				//Timer for getting back up.
				System.Timers.Timer getUpTimer = new System.Timers.Timer(tAction.Stun + AfterUseStun - 1000);

				getUpTimer.Elapsed += (sender, e) => target.GetBackUp(sender, e, getUpTimer);
				getUpTimer.Enabled = true;
			}

			// Set knockbacked position
			attacker.Shove(target, KnockbackDistance);

			// Response
			Send.SkillUseStun(attacker, skill.Info.Id, AfterUseStun, 1);

			// Update both weapons
			SkillHelper.UpdateWeapon(attacker, target, attacker.RightHand, attacker.LeftHand);

			// Action!
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
			var result = attacker.GetRndTotalDamage();
			result *= skill.RankData.Var1 / 100f;

			// +20% dmg for 2H
			if (attacker.RightHand != null && attacker.RightHand.Data.Type == ItemType.Weapon2H)
				result *= 1.20f;

			return result;
		}

		/// <summary>
		/// Returns the chance for a critical hit to happen.
		/// </summary>
		/// <param name="attacker"></param>
		/// <param name="target"></param>
		/// <param name="skill"></param>
		/// <returns></returns>
		protected float GetCritChance(Creature attacker, Creature target, Skill skill)
		{
			var result = attacker.GetTotalCritChance(target.Protection);

			// +5% crit for 2H
			if (attacker.RightHand != null && attacker.RightHand.Data.Type == ItemType.Weapon2H)
				result *= 1.05f;

			return result;
		}

		/// <summary>
		/// Training, called when someone attacks something.
		/// </summary>
		/// <param name="action"></param>
		public void OnCreatureAttackedByPlayer(TargetAction action)
		{
			// Only train if used skill was Smash
			if (action.SkillId != SkillId.Smash)
				return;

			// Get skill
			var attackerSkill = action.Attacker.Skills.Get(SkillId.Smash);
			if (attackerSkill == null) return; // Should be impossible.

			// Learning by attacking
			switch (attackerSkill.Info.Rank)
			{
				case SkillRank.RF:
				case SkillRank.RE:
					attackerSkill.Train(1); // Use the skill successfully.
					if (action.Has(TargetOptions.Critical)) attackerSkill.Train(2); // Critical Hit with Smash.
					if (action.Creature.IsDead) attackerSkill.Train(3); // Finishing blow with Smash.
					break;

				case SkillRank.RD:
				case SkillRank.RC:
				case SkillRank.RB:
				case SkillRank.RA:
				case SkillRank.R9:
				case SkillRank.R8:
				case SkillRank.R7:
					if (action.Has(TargetOptions.Critical) && action.Creature.IsDead)
						attackerSkill.Train(4); // Finishing blow with Critical Hit.
					goto case SkillRank.RF;

				case SkillRank.R6:
				case SkillRank.R5:
				case SkillRank.R4:
				case SkillRank.R3:
				case SkillRank.R2:
				case SkillRank.R1:
					if (action.Has(TargetOptions.Critical)) attackerSkill.Train(1); // Critical Hit with Smash.
					if (action.Creature.IsDead) attackerSkill.Train(2); // Finishing blow with Smash.
					if (action.Has(TargetOptions.Critical) && action.Creature.IsDead) attackerSkill.Train(3); // Finishing blow with Critical Hit.
					break;
			}
		}
	}
}
