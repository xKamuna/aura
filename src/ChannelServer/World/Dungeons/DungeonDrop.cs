using Aura.Channel.World.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aura.Channel.World.Dungeons
{
	public class DungeonDrop : Item
	{
		public decimal Chance { get; set; }
		public int Minimum = 1;
		public int Maximum = 1;

		public DungeonDrop(int pItem, decimal pChance)
			: base(pItem)
		{
			this.Chance = pChance;
		}

		public DungeonDrop(Item pItem, decimal pChance)
			: base(pItem)
		{
			this.Chance = pChance;
		}
	}
}
