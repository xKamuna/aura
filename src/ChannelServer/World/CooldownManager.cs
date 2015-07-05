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
		Dictionary<object, DateTime> CooldownDictionary { get; set; }
		public CooldownManager(Creature creature)
		{
			CooldownDictionary = new Dictionary<object, DateTime>();
			this.Creature = creature;
		} 

		public bool IsOnCooldown(Skill skill)
		{
			if(this.CooldownDictionary.ContainsKey(skill.Info.Id))
				return (DateTime.Now < this.CooldownDictionary[skill.Info.Id]);
			return false;
		}

		public bool IsOnCooldown(SkillId skillId)
		{
			if (this.CooldownDictionary.ContainsKey(skillId))
				return (DateTime.Now < this.CooldownDictionary[skillId]);
			return false;
		}

		public void SetCooldown(Skill skill, DateTime endTime)
		{
			if (this.CooldownDictionary.ContainsKey(skill.Info.Id))
				this.CooldownDictionary[skill.Info.Id] = endTime;
			else
				this.CooldownDictionary.Add(skill.Info.Id, endTime);
		}

		/// <summary>
		/// Do not use this method if possible.  Instead, add a new method.
		/// </summary>
		/// <param name="skillId"></param>
		/// <param name="endTime"></param>
		public void SetCooldownUnsafe(object id, DateTime endTime)
		{
			if (this.CooldownDictionary.ContainsKey(id))
				this.CooldownDictionary[id] = endTime;
			else
				this.CooldownDictionary.Add(id, endTime);
		}

		public void SetCooldown(SkillId skillId, DateTime endTime)
		{
			if (this.CooldownDictionary.ContainsKey(skillId))
				this.CooldownDictionary[skillId] = endTime;
			else
				this.CooldownDictionary.Add(skillId, endTime);
		}

		public Dictionary<object, DateTime> GetDictionary()
		{
			return CooldownDictionary;
		}

	}
}
