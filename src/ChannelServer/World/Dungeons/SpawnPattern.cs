using Aura.Channel.World.Entities;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aura.Channel.World.Dungeons
{
	public class SpawnPattern
	{
		private List<SpawnGroup> _groups = new List<SpawnGroup>();

		private DungeonRoom _parent;

		public delegate void OnGroupClear();
		public delegate void OnClear();

		public OnGroupClear OnGroupClearFunc { get; set; }
		public OnClear OnClearFunc { get; set; }

		public bool Active
		{
			get
			{
				if (_groups.Count <= 0) return false;
				return _groups[0].Active;
			}
		}

		public SpawnPattern(DungeonRoom pParent, DungeonKey keyDrop, params Tuple<int, int>[] spawnInfo)
		{
			_parent = pParent;

			for (var i = 0; i < spawnInfo.Length; i++)
			{
				this.AddSpawn(spawnInfo[i].Item1, spawnInfo[i].Item2, ((i + 1 == spawnInfo.Length) ? keyDrop : DungeonKey.None));
			}
		}

		public SpawnPattern(DungeonRoom pParent)
		{
			_parent = pParent;
		}

		public void AddSpawn(int pSpawnClass, int pAmount, DungeonKey pKey = DungeonKey.None)
		{
			_groups.Add(new SpawnGroup(this, pSpawnClass, pAmount, pKey));
		}

		public void AddSpawns(DungeonKey pKey, Tuple<int, int> firstGroup, params Tuple<int, int>[] spawnGroups)
		{
			var sg = new SpawnGroup(this, firstGroup.Item1, firstGroup.Item2, pKey);

			for (var i = 0; i < spawnGroups.Length; i++)
				sg.AddSpawn(spawnGroups[i].Item1, spawnGroups[i].Item2);

			_groups.Add(sg);
		}

		public void KillNotify(Creature spawn, Creature killer)
		{
			if (_groups.Count == 0)
				return;

			if (_parent.IsBossRoom)
				Log.Info("Notifying of boss kill...");

			_groups[0].NotifyKill(spawn, killer);
		}

		public void ClearNotify(SpawnGroup group)
		{
			if (!_groups.Contains(group))
				return;

			_groups.Remove(group);

			if (this.OnGroupClearFunc != null)
				this.OnGroupClearFunc();

			if (_groups.Count > 0)
			{
				_groups[0].Start(_parent);
			}
			else
			{
				if (OnClearFunc != null)
					OnClearFunc();
			}
		}

		public void Start()
		{
			if (_groups[0].Active)
				return;

			_groups[0].Start(_parent);
		}

		public class SpawnGroup
		{
			private List<Creature> _alive = new List<Creature>();
			private DungeonKey _key = DungeonKey.None;
			private bool _active;

			public List<Tuple<int, int>> Spawns = new List<Tuple<int, int>>();

			public bool DropsKey { get { return (_key != DungeonKey.None); } }
			public bool Active { get { return _active; } }

			public SpawnPattern Parent { get; set; }

			public delegate void OnKill(Creature spawn, Creature killer);
			public delegate void OnStart();
			public delegate void OnClear();

			public OnKill OnKillFunc { get; set; }
			public OnStart OnStartFunc { get; set; }
			public OnClear OnClearFunc { get; set; }

			public SpawnGroup(SpawnPattern pParent, int pSpawnClass, int pAmount, DungeonKey pKey = DungeonKey.None)
			{
				this.Parent = pParent;
				this.Spawns.Add(Tuple.Create(pSpawnClass, pAmount));
				this._key = pKey;
			}

			public void AddSpawn(int pSpawnClass, int pAmount)
			{
				this.Spawns.Add(Tuple.Create(pSpawnClass, pAmount));
			}

			public void Start(DungeonRoom target)
			{
				if (this.Active)
					return;

				var keyAdded = false;

				for (var i = 0; i < this.Spawns.Count; i++)
				{

					var spawn = this.Spawns[i];

					if (!keyAdded && this.DropsKey)
					{
						if (RandomProvider.Get().Next(0, 50) <= 10 || (i + 1) >= this.Spawns.Count)
						{
							var creatures = target.SpawnCreature(spawn.Item1, spawn.Item2, _key);
							keyAdded = true;
							_alive.AddRange(creatures);
						}
					}
					else
					{
						var creatures = target.SpawnCreature(spawn.Item1, spawn.Item2);
						_alive.AddRange(creatures);
					}
				}

				_active = true;
				if (OnStartFunc != null)
					this.OnStartFunc();
			}

			public void NotifyKill(Creature spawn, Creature killer)
			{
				if (!_alive.Contains(spawn))
					return;

				_alive.Remove(spawn);

				if (_alive.Count == 0)
					this.Clear();
			}

			public void Clear()
			{
				Parent.ClearNotify(this);

				if (OnClearFunc != null)
					this.OnClearFunc();
			}
		}
	}
}
