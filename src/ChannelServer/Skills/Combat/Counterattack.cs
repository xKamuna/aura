// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.Skills.Base;
using Aura.Channel.Skills.Magic;
using Aura.Channel.World;
using Aura.Channel.World.Entities;
using Aura.Data;
using Aura.Mabi.Const;
using Aura.Mabi.Network;
using Aura.Shared.Network;
using System;

namespace Aura.Channel.Skills.Combat
{
	/// <summary>
	/// Handler for the Counterattack skill.
	/// </summary>
	/// <remarks>
	/// Var 1: Target damage rate
	/// Var 2: Attacker damage rate
	/// Var 3: Crit bonus
	/// </remarks>
	[Skill(SkillId.Counterattack)]
	public class Counterattack : StandardPrepareHandler
	{
		/// <summary>
		/// Time in milliseconds that attacker and creature are stunned for
		/// after use.
		/// </summary>
		private const short StunTime = 3000;

		/// <summary>
		/// Units the enemy is knocked back.
		/// </summary>
		private const int KnockbackDistance = 450;

		/// <summary>
		/// Handles skill preparation.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public override bool Prepare(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillFlashEffect(creature);
			Send.SkillPrepare(creature, skill.Info.Id, skill.GetCastTime());

			// Disable movement and update client if renovation isn't enabled.
			if (!AuraData.FeaturesDb.IsEnabled("TalentRenovationCloseCombat"))
				creature.Lock(Locks.Move, true);
			// Disable running if combat weapon is equipped
			else if (creature.RightHand != null && creature.RightHand.HasTag("/weapontype_combat/"))
				creature.Lock(Locks.Run);
			// Disable movement
			else
				creature.Lock(Locks.Move);

			return true;
		}

		/// <summary>
		/// Handles redying the skill, called when finishing casting it.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public override bool Ready(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillReady(creature, skill.Info.Id);

			// Training
			if (skill.Info.Rank == SkillRank.RF)
				skill.Train(1); // Use Counterattack.

			return true;
		}

		/// <summary>
		/// Cancels special effects.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		public override void Cancel(Creature creature, Skill skill)
		{
			// Updating unlock because of the updating lock for pre-renovation
			if (!AuraData.FeaturesDb.IsEnabled("TalentRenovationCloseCombat"))
			{
				creature.Unlock(Locks.Run, true);
				creature.Unlock(Locks.Move, true);
			}
		}

		/// <summary>
		/// Returns true if target has counter active and used it.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="attacker"></param>
		/// <returns></returns>
		public static bool Handle(Creature target, Creature attacker)
		{
			if (!target.Skills.IsReady(SkillId.Counterattack))
				return false;
			var counterSkill = target.Skills.Get(SkillId.Counterattack);
			if (counterSkill == null)
				return false;
			if (counterSkill.IsOnCooldown)
				return false;
			counterSkill.State = SkillState.Used;

			var handler = ChannelServer.Instance.SkillManager.GetHandler<Counterattack>(SkillId.Counterattack);
			handler.Use(target, attacker);

			// TODO: Centralize this so we don't have to maintain the active
			//   skill and the regens in multiple places.
			// TODO: Remove the need for this null check... AIs reset ActiveSkill
			//   in Complete, which is called from the combat action handler
			//   before we get back here.
			if (target.Skills.ActiveSkill != null)
				target.Skills.ActiveSkill.State = SkillState.Used;
			target.Regens.Remove("ActiveSkillWait");

			return true;
		}

		/// <summary>
		/// Handles usage of the skill.
		/// </summary>
		/// <param name="attacker"></param>
		/// <param name="target"></param>
		public void Use(Creature attacker, Creature target)
		{
			// Updating unlock because of the updating lock for pre-renovation
			// Has to be done here because we can't have an updating unlock
			// after the combat action, it resets the stun.
			if (!AuraData.FeaturesDb.IsEnabled("TalentRenovationCloseCombat"))
			{
				attacker.Unlock(Locks.Run, true);
				attacker.Unlock(Locks.Move, true);
			}

			var skill = attacker.Skills.Get(SkillId.Counterattack);

			var aAction = new AttackerAction(CombatActionType.RangeHit, attacker, SkillId.Counterattack, target.EntityId);
			aAction.Options |= AttackerOptions.Result | AttackerOptions.KnockBackHit2;

			var tAction = new TargetAction(CombatActionType.CounteredHit2, target, attacker, target.Skills.IsReady(SkillId.Smash) ? SkillId.Smash : SkillId.CombatMastery);
			tAction.Options |= TargetOptions.Result | TargetOptions.Smash;

			var cap = new CombatActionPack(attacker, skill.Info.Id);
			cap.Add(aAction, tAction);

			float damage;
			if(attacker.RightHand != null && attacker.RightHand.Data.HasTag("/weapon/gun/"))   //TODO: Only do this when out of ammo.
            {
				damage = (attacker.GetRndBareHandDamage() * (skill.RankData.Var2 / 100f)) +
				(target.GetRndTotalDamage() * (skill.RankData.Var1 / 100f));
			}
			else
            {
				damage = (attacker.GetRndTotalDamage() * (skill.RankData.Var2 / 100f)) +
				(target.GetRndTotalDamage() * (skill.RankData.Var1 / 100f));
			}


			var critShieldReduction = (target.LeftHand != null ? target.LeftHand.Data.DefenseBonusCrit : 0);
			var critChance = attacker.GetTotalCritChance(target.Protection + critShieldReduction) + skill.RankData.Var3;

			CriticalHit.Handle(attacker, critChance, ref damage, tAction, true);
			var maxDamage = damage; //Damage without Defense and Protection
			SkillHelper.HandleDefenseProtection(target, ref damage, true, true);
			ManaShield.Handle(target, ref damage, tAction, maxDamage);

			target.TakeDamage(tAction.Damage = damage, attacker);

			target.Aggro(attacker);

			if (target.IsDead)
				tAction.Options |= TargetOptions.FinishingKnockDown;

			
			if(attacker.IsCharacter && AuraData.FeaturesDb.IsEnabled("CombatSystemRenewal") && StunTime > 2000)
			{
				aAction.Stun = 2000;
			}
			else
			{
				aAction.Stun = StunTime;
			}
			tAction.Stun = StunTime;

			if (!target.IsDead)
			{
					//Timer for getting back up.
					System.Timers.Timer getUpTimer = new System.Timers.Timer(tAction.Stun-1000);

					getUpTimer.Elapsed += (sender, e) => { if (target != null) { target.GetBackUp(sender, e, getUpTimer); } };
					getUpTimer.Enabled = true;
			}

			target.Stability = Creature.MinStability;
			attacker.Shove(target, KnockbackDistance);

			// Update both weapons
			SkillHelper.UpdateWeapon(attacker, target, attacker.RightHand, attacker.LeftHand);

			if (AuraData.FeaturesDb.IsEnabled("CombatSystemRenewal"))
			{
				skill.EndCooldownTime = DateTime.Now.AddMilliseconds(7000);
			}
			else
			{
				Send.ResetCooldown(attacker, skill.Info.Id);
			}

			Send.SkillUseStun(attacker, skill.Info.Id, aAction.Stun, 1);

			this.Training(aAction, tAction);

			cap.Handle();
		}

		/// <summary>
		/// Trains the skill for attacker and target, based on what happened.
		/// </summary>
		/// <param name="aAction"></param>
		/// <param name="tAction"></param>
		public void Training(AttackerAction aAction, TargetAction tAction)
		{
			var attackerSkill = aAction.Creature.Skills.Get(SkillId.Counterattack);
			var targetSkill = tAction.Creature.Skills.Get(SkillId.Counterattack);

			if (attackerSkill.Info.Rank == SkillRank.RF)
			{
				attackerSkill.Train(2); // Successfully counter enemy's attack.

				if (tAction.SkillId == SkillId.Smash)
					attackerSkill.Train(4); // Counter enemy's special attack.

				if (tAction.Has(TargetOptions.Critical))
					attackerSkill.Train(5); // Counter with critical hit.
			}
			else
			{
				attackerSkill.Train(1); // Successfully counter enemy's attack.

				if (tAction.SkillId == SkillId.Smash)
					attackerSkill.Train(2); // Counter enemy's special attack.

				if (tAction.Has(TargetOptions.Critical))
					attackerSkill.Train(4); // Counter with critical hit.
			}

			if (targetSkill != null)
				targetSkill.Train(3); // Learn from the enemy's counter attack.
			else if (tAction.Creature.LearningSkillsEnabled)
				tAction.Creature.Skills.Give(SkillId.Counterattack, SkillRank.Novice); // Obtaining the Skill
		}
	}
}
