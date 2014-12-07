using Aura.Channel.World;
using Aura.Channel.World.Dungeons;
using Aura.Channel.World.Entities;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aura.Channel.Scripting.Scripts
{
	public abstract class DungeonScript : GeneralScript
	{
		private Dungeon _dungeon;
		private int _regionIndex;

		public Dungeon Dungeon { get { return _dungeon; } set { _dungeon = value; } }
		public int RegionIndex { get { return _regionIndex; } set { _regionIndex = value; } }
		public DungeonLobby Lobby = DungeonLobby.Alby;
		public DungeonLevel Level = DungeonLevel.Normal;
		public int ItemClass = 2000;
		public bool ItemRequired = false; //If true, only fired when the item itself is dropped
		public List<int> ItemPass = new List<int>(); //If ItemRequired, only items in this list will open the dungeon
		public string Design = "";
		public uint Seed = 2263997264; //Is this duplicated across dungeon types? We'll find out!
		public int Floorplan = 0; //Unkown but required Seen 0 and 41 possibly a hall layout
		public bool TestDG = false;
		private List<DungeonDrop> _drops = new List<DungeonDrop>();

		public override bool Init()
		{
			this.OnLoad();

			Log.Info("Loaded dungeon script {0}", this.Seed);
			ChannelServer.Instance.World.DungeonManager.AddScript(this);
			return true;
		}

		public virtual void OnLoad()
		{
			//Set Local Properties here
		}

		public virtual void OnStart() { }
		public virtual void OnEnd() { }
		public virtual void OnBossOpen() { }
		public virtual void OnBossKill() { }
		public virtual void OnRoomClear() { }
		public virtual void OnFloorDown() { }
		public virtual void OnRoomEnter() { }
		public virtual void OnRoomLeave() { }

		public virtual void Build()
		{
		}

		/// <summary>
		/// Sets the dungeon to testing, only developers will be able to enter.
		/// </summary>
		public void IsTestDungeon()
		{
			this.TestDG = true;
		}

		public List<DungeonDrop> GetRewards()
		{
			return _drops;
		}

		public void SetLobby(DungeonLobby pLobby)
		{
			this.Lobby = pLobby;
		}

		public void SetLevel(DungeonLevel pLevel)
		{
			this.Level = pLevel;
		}

		public void SetItemRequired(bool pToggle)
		{
			this.ItemRequired = pToggle;
		}

		public void SetDesign(string pDesign)
		{
			this.Design = pDesign;
		}

		public void SetEntrance(DungeonFloor pFloor, byte pX, byte pY, short pArea, Door pExitPosition, params Door[] pDoors)
		{
			pFloor.SetEntrance(pX, pY, pArea, pExitPosition);

			foreach (var door in pDoors)
				pFloor.Entrance.AddDoor(door);
		}

		public void SetBossroom(DungeonFloor pFloor, byte pX, byte pY, short pArea)
		{
			pFloor.SetBossroom(pX, pY, pArea);
		}

		public void SetExit(DungeonFloor pFloor, byte pX, byte pY, short pArea, Door pExitPosition, params Door[] pDoors)
		{
			pFloor.SetExit(pX, pY, pArea, pExitPosition);

			foreach (var door in pDoors)
				pFloor.Exit.AddDoor(door);
		}

		public void SetFloorplan(int pFloorplan)
		{
			this.Floorplan = pFloorplan;
		}

		public void SetSeed(uint pSeed)
		{
			this.Seed = pSeed;
		}

		public void SetItemDropped(int pItemClass)
		{
			this.ItemClass = pItemClass;
		}

		public DungeonFloor AddFloor(uint pFloorSeed, int pEntryX, int pEntryY)
		{
			return new DungeonFloor(_dungeon, pFloorSeed, _regionIndex++, new Position(pEntryX, pEntryY));
		}

		public DungeonRoom AddRoom(DungeonFloor pFloor, byte pX, byte pY, short pArea, Puzzle pPuzzle, params Door[] pDoors)
		{
			return new DungeonRoom(pFloor, pX, pY, pArea, pPuzzle, pDoors);
		}

		public void AddSpawnGroup(DungeonRoom pRoom, DungeonKey pKeyDrop, Tuple<int, int> pFirstGroup, params Tuple<int, int>[] pSpawns)
		{
			pRoom.Spawns.AddSpawns(pKeyDrop, pFirstGroup, pSpawns);
		}

		public void SpawnBoss(Tuple<int, int> firstGroup, params Tuple<int, int>[] pSpawns)
		{
			var bossroom = _dungeon.Floors[_dungeon.Floors.Count - 1].BossRoom;
			bossroom.Spawns.AddSpawns(DungeonKey.None, firstGroup, pSpawns);
			bossroom.Spawns.OnClearFunc = new SpawnPattern.OnClear(() =>
			{
				_dungeon.Cleared();
			});

			bossroom.Spawns.Start();
		}

		public Tuple<int, int> Spawn(int pRace, int pAmount)
		{
			return Tuple.Create(pRace, pAmount);
		}

		public void AddDrop(int pItemClass, decimal pChance, int pMinAmount, int pMaxAmount)
		{
			DungeonDrop drop = new DungeonDrop(pItemClass, pChance);
			drop.Minimum = pMinAmount;
			drop.Maximum = pMaxAmount;
			_drops.Add(drop);
		}
	}
}