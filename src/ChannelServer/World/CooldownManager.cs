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
			this.Creature = creature;
		} 

		public bool IsOnCooldown(object cooldownObject)
		{
			if(this.CooldownDictionary.ContainsKey(cooldownObject))
				return (DateTime.Now < this.CooldownDictionary[cooldownObject]);
			return false;
		}

		public bool IsOnCooldown(SkillId cooldownId)
		{
			var cooldownObject = this.Creature.Skills.Get(cooldownId);
			if (cooldownObject != null && this.CooldownDictionary.ContainsKey(cooldownObject))
				return (DateTime.Now < this.CooldownDictionary[cooldownObject]);
			return false;
		}

		public void SetCooldown(object cooldownObject, DateTime endTime)
		{
			if (this.CooldownDictionary.ContainsKey(cooldownObject))
				this.CooldownDictionary[cooldownObject] = endTime;
			else
				this.CooldownDictionary.Add(cooldownObject, endTime);
		}

		public void SetCooldown(SkillId cooldownId, DateTime endTime)
		{
			var cooldownObject = this.Creature.Skills.Get(cooldownId);
			if (cooldownObject == null)
				return;
			if (this.CooldownDictionary.ContainsKey(cooldownObject))
				this.CooldownDictionary[cooldownObject] = endTime;
			else
				this.CooldownDictionary.Add(cooldownObject, endTime);
		}

	}
}
