// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Skills;
using Aura.Channel.World.Entities;
using Aura.Mabi.Const;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.Channel.World
{
	public class CooldownManager
	{
		public Creature Creature { get; set; }
		private Dictionary<object, DateTime> _cooldownDictionary;
		public CooldownManager(Creature creature)
		{
			this._cooldownDictionary = new Dictionary<object, DateTime>();
			this.Creature = creature;
		}

		public bool IsOnCooldown(Skill skill)
		{
			if (this._cooldownDictionary.ContainsKey(skill.Info.Id))
				return (DateTime.Now < this._cooldownDictionary[skill.Info.Id]);
			return false;
		}

		public bool IsOnCooldown(SkillId skillId)
		{
			if (this._cooldownDictionary.ContainsKey(skillId))
				return (DateTime.Now < this._cooldownDictionary[skillId]);
			return false;
		}

		public void SetCooldown(Skill skill, DateTime endTime)
		{
			if (this._cooldownDictionary.ContainsKey(skill.Info.Id))
				this._cooldownDictionary[skill.Info.Id] = endTime;
			else
				this._cooldownDictionary.Add(skill.Info.Id, endTime);
		}

		/// <summary>
		/// Do not use this method if possible.  Instead, add a new method.
		/// </summary>
		/// <param name="skillId"></param>
		/// <param name="endTime"></param>
		public void SetCooldownUnsafe(object id, DateTime endTime)
		{
			if (this._cooldownDictionary.ContainsKey(id))
				this._cooldownDictionary[id] = endTime;
			else
				this._cooldownDictionary.Add(id, endTime);
		}

		public void SetCooldown(SkillId skillId, DateTime endTime)
		{
			if (this._cooldownDictionary.ContainsKey(skillId))
				this._cooldownDictionary[skillId] = endTime;
			else
				this._cooldownDictionary.Add(skillId, endTime);
		}

		public Dictionary<object, DateTime> GetDictionary()
		{
			return this._cooldownDictionary;
		}

	}
}
