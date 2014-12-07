using Aura.Channel.Scripting.Scripts;
using Aura.Channel.World.Entities;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aura.Channel.World.Dungeons
{
	public class DungeonManager
	{
		private int _regionIndex = 10001;
		private int _instanceIndex = 0;

		private Dictionary<Tuple<DungeonLobby, DungeonLevel>, List<DungeonScript>> _scripts = new Dictionary<Tuple<DungeonLobby, DungeonLevel>, List<DungeonScript>>();
		private Dictionary<Tuple<DungeonLobby, DungeonLevel>, List<Dungeon>> _activeDungeons = new Dictionary<Tuple<DungeonLobby, DungeonLevel>, List<Dungeon>>();
		//TODO: Account for freed regions.

		public DungeonManager()
		{
		}

		public bool StartDungeon(Creature pCreator, DungeonLobby pLobby, DungeonLevel pLevel, Item pItem)
		{
			var script = this.FindScriptOrNull(pLobby, pLevel, pItem.Info.Id);
			if (script == null)
				return false;

			int nextAvailableRegion = _regionIndex;
			Log.Info("Next available Region: {0}", nextAvailableRegion);
			var newDungeon = new Dungeon(pCreator, _regionIndex, script, out nextAvailableRegion);

			if (newDungeon == null)
				return false;

			_regionIndex = nextAvailableRegion;
			var addKey = Tuple.Create(pLobby, pLevel);

			if (!_activeDungeons.ContainsKey(addKey))
				_activeDungeons[addKey] = new List<Dungeon>();

			_activeDungeons[addKey].Add(newDungeon);
			Log.Info("Starting dungeon...");
			newDungeon.Start();
			return true;
		}

		public void AddScript(DungeonScript pScript)
		{
			var key = Tuple.Create(pScript.Lobby, pScript.Level);
			if (!_scripts.ContainsKey(key))
				_scripts[key] = new List<DungeonScript>();

			_scripts[key].Add(pScript);
		}

		public bool HasScript(DungeonLobby pLobby, DungeonLevel pLevel, int itemId = -1)
		{
			var key = Tuple.Create(pLobby, pLevel);

			if (!_scripts.ContainsKey(key))
				return false;

			if (itemId == -1 && _scripts[key].Count > 0)
				return true;

			if (_scripts[key].Count(a => a.ItemPass.Contains(itemId)) > 0) //<<<Gross
				return true;

			return false;
		}

		public DungeonScript FindScriptOrNull(DungeonLobby pLobby, DungeonLevel pLevel, int itemId = -1)
		{
			var key = Tuple.Create(pLobby, pLevel);
			List<DungeonScript> allowableScripts;

			if (!this.HasScript(pLobby, pLevel, itemId))
			{
				//Doesn't have the exact script, let's see if it has one at all for the lobby or level
				if (!this.HasScript(pLobby, pLevel))
				{
					//this is awkward... We should just not allow this to happen at all
					return null;
				}
				//One for the lobby and level so let's just use that as a default
				allowableScripts = _scripts[key];
			}
			else
			{
				allowableScripts = _scripts[key].Where(a => a.ItemPass.Contains(itemId)).ToList();
			}

			if (allowableScripts.Count <= 0)
				return null;

			//Random script!
			var index = RandomProvider.Get().Next(0, allowableScripts.Count - 1);

			return allowableScripts[index];
		}

		public bool HandleDungeonDrop(Creature pOrigin, Item pItem)
		{
			if (!Enum.IsDefined(typeof(DungeonLobby), pOrigin.RegionId))
				return false;

			var pos = pOrigin.GetPosition();

			if (!(pos.X >= 3000 && pos.X <= 3400 && pos.Y >= 3000 && pos.Y <= 3400))
				return false;

			if (this.StartDungeon(pOrigin, DungeonLobby.Alby, DungeonLevel.Normal, pItem))
				return true;

			return false;
		}

		public Dungeon FindDungeonByCreature(Creature pCreature)
		{
			foreach (var dgList in _activeDungeons.Values)
			{
				foreach (var dungeon in dgList)
				{
					if (dungeon.Players.Contains(pCreature))
					{
						if (dungeon.EntryRegion.Id == pCreature.RegionId)
							return dungeon;

						foreach (var floor in dungeon.Floors)
						{
							if (floor.Region.Id == pCreature.RegionId) //Just in case
								return dungeon;
						}
					}
				}
			}

			//Could not be found, return null
			return null;
		}

		public int NewInstance()
		{
			return ++_instanceIndex;
		}
	}
}
