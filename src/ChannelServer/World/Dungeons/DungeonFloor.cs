using Aura.Channel.World.Entities;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aura.Channel.World.Dungeons
{
	public class DungeonFloor
	{
		private Dungeon _parent;
		public Region Region;

		//Grid Objects
		public DungeonRoom Entrance;
		public Position EntrancePosition;
		public DungeonRoom Exit;
		public Position ExitPosition;
		public DungeonRoom BossRoom;
		public Dictionary<Tuple<byte, byte>, DungeonRoom> Rooms = new Dictionary<Tuple<byte, byte>, DungeonRoom>();

		//TODO: Improve Keys
		private List<int> _keyColors = new List<int>();

		//Properties
		public Dungeon Parent { get { return _parent; } }
		public uint Seed { get; set; }
		public int Index
		{
			get
			{
				return this.Parent.Floors.IndexOf(this);
			}
		}

		public DungeonFloor(Dungeon pParent, uint pSeed, int pRegionId, Position pEntrancePosition, Position pExitPosition = new Position())
		{
			_parent = pParent;

			this.Seed = pSeed;
			this.EntrancePosition = pEntrancePosition;
			this.ExitPosition = pExitPosition;

			if (!ChannelServer.Instance.World.HasRegion(pRegionId))
				ChannelServer.Instance.World.AddRegion(pRegionId);

			this.Region = ChannelServer.Instance.World.GetRegion(pRegionId);

			if (this.Region == null)
				return;

			this.Parent.Floors.Add(this);
		}

		public int GenerateKey(int forceColor = 0x000000)
		{
			//Generate a random color...
			int color = this.GenerateColor();

			if (forceColor > 0)
				color = forceColor;

			if (_keyColors.Contains(color) && forceColor != 0) //Boss key exception
				return this.GenerateKey();

			//Unique color
			_keyColors.Add(color);

			return color;
		}

		public int GenerateColor()
		{
			//Generate a random color...
			int color = RandomProvider.Get().Next(0xFFFFFF);

			if (color == 0xFF0000) //Boss key exception
				return this.GenerateColor();

			return color;
		}

		public int GetKey()
		{
			int value = 0xFFFFFF;
			if (_keyColors.Count > 0)
				value = _keyColors[0];

			return value;
		}

		public void RemoveKey(int keyColor)
		{
			if (_keyColors.Contains(keyColor))
				_keyColors.Remove(keyColor);
		}

		public DungeonRoom GetRoomOrDefault(byte x, byte y)
		{
			return Rooms[Tuple.Create(x, y)];
		}

		public void SetEntrance(byte pX, byte pY, short pArea, Door pUpstairsExit)
		{
			this.Entrance = new DungeonRoom(this, pX, pY, pArea, Puzzle.FloorUp, pUpstairsExit);
		}

		public void SetExit(byte pX, byte pY, short pArea, Door pDownstairsExit)
		{
			this.Exit = new DungeonRoom(this, pX, pY, pArea, Puzzle.FloorUp, pDownstairsExit);
		}

		public void SetBossroom(byte pX, byte pY, short pArea)
		{
			this.BossRoom = new DungeonRoom(this, pX, pY, pArea, Puzzle.Boss);
			this.Exit = new DungeonRoom(this, pX, (byte)(pY + 2), (short)(pArea + 0x0001), Puzzle.Reward);
		}

		public void Build()
		{
			if (this.Entrance != null)
				this.Entrance.Build();

			foreach (var room in Rooms.Values)
				room.Build();

			if (this.BossRoom != null)
				this.BossRoom.Build();

			if (this.Exit != null)
				this.Exit.Build();
		}

		public void NotifyKill(Creature pCreature, Creature pKiller)
		{
			foreach (var room in Rooms.Values)
			{
				if (room.Spawns.Active)
					room.Spawns.KillNotify(pCreature, pKiller);
			}

			if (this.BossRoom != null)
			{
				this.BossRoom.KillNotify(pCreature, pKiller);
			}
		}
	}
}
