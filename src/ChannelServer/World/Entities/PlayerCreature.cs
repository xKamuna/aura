﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.World.Dungeons;
using Aura.Data;
using Aura.Mabi.Const;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aura.Channel.World.Entities
{
	/// <summary>
	/// Base class for creatures controlled by players.
	/// </summary>
	public abstract class PlayerCreature : Creature
	{
		private List<Entity> _visibleEntities = new List<Entity>();
		private object _lookAroundLock = new Object();

		/// <summary>
		/// Creature id, for creature database.
		/// </summary>
		public long CreatureId { get; set; }

		/// <summary>
		/// Server this creature exists on.
		/// </summary>
		public string Server { get; set; }

		/// <summary>
		/// Time at which the creature can be deleted.
		/// </summary>
		public DateTime DeletionTime { get; set; }

		/// <summary>
		/// Time at which the creature was created.
		/// </summary>
		public DateTime CreationTime { get; set; }

		/// <summary>
		/// Time of last rebirth.
		/// </summary>
		public DateTime LastRebirth { get; set; }

		/// <summary>
		/// How many times the character rebirthed.
		/// </summary>
		public int RebirthCount { get; set; }

		/// <summary>
		/// Time of last login.
		/// </summary>
		public DateTime LastLogin { get; set; }

		/// <summary>
		/// Time of last aging.
		/// </summary>
		public DateTime LastAging { get; set; }

		/// <summary>
		/// Specifies whether to update visible creatures or not.
		/// </summary>
		public bool Watching { get; set; }

		/// <summary>
		/// Set to true if creature is supposed to be saved.
		/// </summary>
		public bool Save { get; set; }

		/// <summary>
		/// Player's CP, based on stats and skills.
		/// </summary>
		public override float CombatPower
		{
			get
			{
				var cp = 0f;

				cp += this.Skills.HighestSkillCp;
				cp += this.Skills.SecondHighestSkillCp * 0.5f;
				cp += this.LifeMaxBase;
				cp += this.ManaMaxBase * 0.5f;
				cp += this.StaminaMaxBase * 0.5f;
				cp += this.StrBase;
				cp += this.IntBase * 0.2f;
				cp += this.DexBase * 0.1f;
				cp += this.WillBase * 0.5f;
				cp += this.LuckBase * 0.1f;

				return cp;
			}
		}

		/// <summary>
		/// Returns whether creature is able to receive exp and level up.
		/// </summary>
		public override bool LevelingEnabled { get { return true; } }

		/// <summary>
		/// Creatures new PlayerCreature.
		/// </summary>
		public PlayerCreature()
		{
			this.Watching = true;
			this.UseBattleStanceFromAOE = true;
		}

		/// <summary>
		/// Instructs client to move to target location.
		/// Returns false if region doesn't exist.
		/// </summary>
		/// <param name="regionId"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public override bool Warp(int regionId, int x, int y)
		{
			if (regionId == this.RegionId && this.Region != Region.Limbo)
			{
				// The Tin cutscene at the very beginning requires a Warp to
				// fade away, otherwise the screen will stay black. However,
				// the cutscene in the first dungeon includes a jump to the
				// treasure room, where we can't warp, because that would drop
				// the treasure chest key. This is basically a hack until we
				// find a better way to properly get out of the Tin cutscene.
				if (this.Temp.CurrentCutscene == null || this.Region.IsDungeon)
				{
					this.Jump(x, y);
					return true;
				}
			}

			var targetRegion = ChannelServer.Instance.World.GetRegion(regionId);
			if (targetRegion == null)
			{
				Send.ServerMessage(this, "Warp failed, region doesn't exist.");
				Log.Error("PC.Warp: Region '{0}' doesn't exist.", regionId);
				return false;
			}

			var currentRegionId = this.RegionId;
			var loc = new Location(currentRegionId, this.GetPosition());

			this.LastLocation = loc;
			this.WarpLocation = new Location(regionId, x, y);
			this.Warping = true;
			this.Lock(Locks.Default, true);

			// TODO: We don't have to send the "create warps" every time,
			//   only when the player is warped there for the first time.

			// Dynamic Region warp
			var dynamicRegion = targetRegion as DynamicRegion;
			if (dynamicRegion != null)
			{
				if (!this.Region.IsTemp)
					this.FallbackLocation = loc;

				Send.EnterDynamicRegion(this, currentRegionId, targetRegion, x, y);

				return true;
			}

			// Dungeon warp
			var dungeonRegion = targetRegion as DungeonRegion;
			if (dungeonRegion != null)
			{
				if (!this.Region.IsTemp)
				{
					this.FallbackLocation = loc;
					this.DungeonSaveLocation = this.WarpLocation;
				}

				Send.DungeonInfo(this, dungeonRegion.Dungeon);

				return true;
			}

			// Normal warp
			Send.EnterRegion(this, regionId, x, y);

			return true;
		}

		/// <summary>
		/// Updates visible creatures, sends Entities(Dis)Appear.
		/// </summary>
		public void LookAround()
		{
			if (!this.Watching)
				return;

			lock (_lookAroundLock)
			{
				var currentlyVisible = this.Region.GetVisibleEntities(this);

				var appear = currentlyVisible.Except(_visibleEntities);
				var disappear = _visibleEntities.Except(currentlyVisible);

				Send.EntitiesAppear(this.Client, appear);
				Send.EntitiesDisappear(this.Client, disappear);

				_visibleEntities = currentlyVisible;
			}
		}

		/// <summary>
		/// Returns whether player can target the given creature.
		/// </summary>
		/// <param name="creature"></param>
		/// <returns></returns>
		public override bool CanTarget(Creature creature)
		{
			if (!base.CanTarget(creature))
				return false;

			// Players can only target "bad" NPCs.
			if (creature.Has(CreatureStates.GoodNpc))
				return false;

			// Players can't target players (outside of PvP, TODO)
			if (creature.IsPlayer)
				return false;

			return true;
		}

		/// <summary>
		/// Players survive when they had more than half of their life left.
		/// </summary>
		/// <param name="damage"></param>
		/// <param name="from"></param>
		/// <param name="lifeBefore"></param>
		/// <returns></returns>
		protected override bool ShouldSurvive(float damage, Creature from, float lifeBefore)
		{
			// No surviving once you're in deadly
			if (lifeBefore < 0)
				return false;

			if (lifeBefore >= this.LifeMax / 2)
				return true;

			// Chance = Will/10, capped at 50%
			// (i.e 80 Will = 8%, 500+ Will = 50%)
			// Actual formula unknown
			// Added life proximity to the formula.
			var chance = Math.Min(50, this.Will / 10 + (LifeMax > 0 ? ((this.Life / this.LifeMax) * 10) : 0));
			return (RandomProvider.Get().Next(101) < chance);
		}

		/// <summary>
		/// Increases age by years and sends update packets.
		/// </summary>
		/// <param name="years"></param>
		public void AgeUp(short years)
		{
			if (years < 0 || this.Age + years > short.MaxValue)
				return;

			float life = 0, mana = 0, stamina = 0, str = 0, dex = 0, int_ = 0, will = 0, luck = 0;
			short ap = 0;

			var newAge = this.Age + years;
			while (this.Age < newAge)
			{
				// Increase age before requestin statUp, we want the stats
				// for the next age.
				this.Age++;

				var statUp = AuraData.StatsAgeUpDb.Find(this.RaceId, this.Age);
				if (statUp == null)
				{
					// Continue silently, creatures age past 25 without
					// bonuses, and if someone changes that we don't know what
					// the max will be.
					//Log.Debug("AgeUp: Missing stat data for race '{0}', age '{1}'.", this.Race, this.Age);
					continue;
				}

				// Collect bonuses for multi aging
				life += statUp.Life;
				mana += statUp.Mana;
				stamina += statUp.Stamina;
				str += statUp.Str;
				dex += statUp.Dex;
				int_ += statUp.Int;
				will += statUp.Will;
				luck += statUp.Luck;
				ap += statUp.AP;
			}

			// Apply stat bonuses
			this.LifeMaxBase += life;
			this.Life += life;
			this.ManaMaxBase += mana;
			this.Mana += mana;
			this.StaminaMaxBase += stamina;
			this.Stamina += stamina;
			this.StrBase += str;
			this.DexBase += dex;
			this.IntBase += int_;
			this.WillBase += will;
			this.LuckBase += luck;
			this.AbilityPoints += ap;

			this.LastAging = DateTime.Now;

			if (this is Character)
				this.Height = Math.Min(1.0f, 1.0f / 7.0f * (this.Age - 10.0f)); // 0 ~ 1.0

			// Send stat bonuses
			if (life != 0) Send.SimpleAcquireInfo(this, "life", mana);
			if (mana != 0) Send.SimpleAcquireInfo(this, "mana", mana);
			if (stamina != 0) Send.SimpleAcquireInfo(this, "stamina", stamina);
			if (str != 0) Send.SimpleAcquireInfo(this, "str", str);
			if (dex != 0) Send.SimpleAcquireInfo(this, "dex", dex);
			if (int_ != 0) Send.SimpleAcquireInfo(this, "int", int_);
			if (will != 0) Send.SimpleAcquireInfo(this, "will", will);
			if (luck != 0) Send.SimpleAcquireInfo(this, "luck", luck);
			if (ap != 0) Send.SimpleAcquireInfo(this, "ap", ap);

			Send.StatUpdateDefault(this);

			// XXX: Replace with effect and notice to allow something to happen past age 25?
			Send.AgeUpEffect(this, this.Age);
		}

		/// <summary>
		/// Aggroes target, setting target and putting creature in battle stance.
		/// </summary>
		/// <param name="creature"></param>
		public override void Aggro(Creature target, bool alert = false)
		{
			this.IsInBattleStance = true;
			if(this.Target == null)
			{
				this.Target = target;
				Send.SetCombatTarget(this, target.EntityId, TargetMode.Normal);
				Send.CombatTargetUpdate(this, target.EntityId);
			}
		}
	}
}
