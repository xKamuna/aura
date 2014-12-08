using Aura.Channel.Network.Sending;
using Aura.Channel.World.Entities;
using Aura.Data;
using Aura.Data.Database;
using Aura.Shared.Mabi.Const;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Aura.Channel.World.Dungeons
{
	public class DungeonRoom
	{
		//Positional Things
		public DungeonFloor Floor;
		public byte X { get; set; }
		public byte Y { get; set; }
		public short Area { get; set; }

		public Position Center
		{
			get
			{
				if (!this.IsBossRoom)
					return new Position((X * 2400) + 1200, (Y * 2400) + 1200);
				else
					return new Position((X * 2400) + 1200, (Y + 1) * 2400);
			}
		}

		//Utils
		public bool IsBossRoom { get { return this.Puzzle == Puzzle.Boss; } }
		public bool Cleared = false;

		//Objects
		private List<Entity> _entities = new List<Entity>();
		private Dictionary<Door, Prop> _doors = new Dictionary<Door, Prop>();
		public Puzzle Puzzle = Puzzle.None;
		public SpawnPattern Spawns { get; set; }
		public Door Doors = Door.None;

		private long _propIndex = 0x00A1000000000000;

		//Entity Accessors
		public List<Prop> Props
		{
			get
			{
				return _entities.Where(a => a.DataType == DataType.Prop).Cast<Prop>().ToList();
			}
		}

		public List<NPC> NPCs
		{
			get
			{
				return _entities.Where(a => a.DataType == DataType.Creature).Cast<NPC>().ToList();
			}
		}

		/// <summary>
		/// Creates a Dungeon Room
		/// </summary>
		/// <param name="pFloor"></param>
		/// <param name="pX"></param>
		/// <param name="pY"></param>
		/// <param name="pArea"></param>
		/// <param name="pPuzzle"></param>
		/// <param name="pDoors"></param>
		public DungeonRoom(DungeonFloor pFloor, byte pX, byte pY, short pArea, Puzzle pPuzzle = Puzzle.None, params Door[] pDoors)
		{
			this.Floor = pFloor;
			this.X = pX;
			this.Y = pY;
			this.Area = pArea;
			this.Puzzle = pPuzzle;

			foreach (var door in pDoors)
				this.Doors |= door;

			_propIndex = _propIndex + ((long)Floor.Region.Id << 32) + ((long)Area << 16);
			var testRegion = (ulong)Floor.Region.Id;
			var testArea = (ulong)Area;

			//TODO: Spawn Patterns inherited from floor.
			this.Spawns = new SpawnPattern(this);

			if (this.Puzzle != Puzzle.Boss && this.Puzzle != Puzzle.FloorUp && this.Puzzle != Puzzle.FloorDown && this.Puzzle != Puzzle.Reward)
				this.Floor.Rooms[Tuple.Create(this.X, this.Y)] = this;
		}

		/// <summary>
		/// A Room on the dungeon grid
		/// </summary>
		/// <param name="pX"></param>
		/// <param name="pY"></param>
		/// <param name="pArea"></param>
		public DungeonRoom(byte pX, byte pY, byte pArea)
		{
			this.X = pX;
			this.Y = pY;

			this.Area = pArea;
		}

		/// <summary>
		/// Builds the room based on props, puzzles and creatures
		/// </summary>
		public void Build()
		{
			//Seed the collission tree with the dungeon room borders
			{
				//TODO: Move room points to properties?
				var p1 = new Point(this.X * 2400 + 300, this.Y * 2400 + 2100);
				var p2 = new Point(this.X * 2400 + 2100, this.Y * 2400 + 2100);
				var p3 = new Point(this.X * 2400 + 2100, this.Y * 2400 + 300);
				var p4 = new Point(this.X * 2400 + 300, this.Y * 2400 + 300);

				this.Floor.Region.Collisions.AddRect(p1, p2, p3, p4);
			}

			//Add Doors
			{
				//Normal Doors
				if ((this.Doors & Door.North) != 0)
					this.AddDoor(Door.North, false);
				if ((this.Doors & Door.East) != 0)
					this.AddDoor((Door.East), false);
				if ((this.Doors & Door.South) != 0)
					this.AddDoor((Door.South), false);
				if ((this.Doors & Door.West) != 0)
					this.AddDoor((Door.West), false);

				//Locked Doors
				if ((this.Doors & Door.NorthLocked) != 0)
					this.AddDoor((Door.North), true);
				if ((this.Doors & Door.EastLocked) != 0)
					this.AddDoor((Door.East), true);
				if ((this.Doors & Door.SouthLocked) != 0)
					this.AddDoor((Door.South), true);
				if ((this.Doors & Door.WestLocked) != 0)
					this.AddDoor((Door.West), true);
			}

			//Time to do puzzles~
			switch (this.Puzzle)
			{
				case Puzzle.FloorUp:
					this.AddFloorUpProps();
					break;
				case Puzzle.FloorDown:
					this.AddFloorDownProps();
					break;
				case Puzzle.Boss:
					this.AddBossProps();
					break;
				case Puzzle.Reward:
					this.AddRewardProps();
					break;
				case Puzzle.SpawnChest:
					this.AddSpawnChest();
					break;
				case Puzzle.Switches:
					this.AddSwitches();
					break;
				case Puzzle.None:
					break;
				default:
					Log.Unimplemented("Unimplimented puzzle type `{0}` in Dungeon `{1}` on Floor `{2}`", this.Puzzle.ToString("g"), this.Floor.Parent.Script.Design, this.Floor.Region); //TODO: Floor Index
					break;
			}
		}

		public List<Creature> SpawnCreature(int pClass, int pAmount, DungeonKey pDropKey = DungeonKey.None)
		{
			var rnd = RandomProvider.Get();
			List<Creature> spawns = new List<Creature>();
			var keyDropped = false;

			for (var i = 0; i < pAmount; i++)
			{
				int x = (int)rnd.Next(this.Center.X - 900, this.Center.X + 900);
				int y = (int)rnd.Next(this.Center.Y - 900, this.Center.Y + 900);
				var creature = ChannelServer.Instance.ScriptManager.Spawn(pClass, this.Floor.Region.Id, x, y, -1, true, true);
				if (pDropKey != DungeonKey.None && !keyDropped)
				{
					creature.Drops.Add((int)pDropKey, 100f);
					keyDropped = true;
				}
				var cPos = creature.GetPosition();
				Send.SpawnEffect(SpawnEffect.Monster, this.Floor.Region.Id, cPos.X, cPos.Y, creature);
				spawns.Add(creature);
			}

			return spawns;
		}

		public void AddDoor(Door pDoor)
		{
			Doors |= pDoor;
		}

		public void KillNotify(Creature mob, Creature killer)
		{
			this.Spawns.KillNotify(mob, killer);
		}

		public void OpenDoor(Door pDoor)
		{
			if ((this.Doors & pDoor) == 0 || !_doors.ContainsKey(pDoor))
				return;

			var door = _doors[pDoor];
			door.State = "open";
			Send.PropUpdate(door);
		}

		public void CloseDoor(Door pDoor)
		{
			if ((this.Doors & pDoor) == 0 || !_doors.ContainsKey(pDoor))
				return;

			var door = _doors[pDoor];
			door.State = "closed";
			Send.PropUpdate(door);
		}

		public void OpenAllDoors()
		{
			foreach (var door in _doors.Values)
			{
				door.State = "open";
				Send.PropUpdate(door);
			}
		}

		public void CloseAllDoors()
		{
			//TODO: Close adjacent doors in other rooms
			foreach (var door in _doors.Values)
			{
				door.State = "closed";
				Send.PropUpdate(door);
			}
		}

		public void OpenBossDoor()
		{
			if (!this.IsBossRoom)
				return;

			var door = _doors.Values.FirstOrDefault(a => a.Info.Id == (int)DoorProp.Boss);
			door.State = "open";
			Send.PropUpdate(door);
		}

		public void OpenRewardsDoor()
		{
			if (!this.IsBossRoom)
				return;

			var rewardRoom = Floor.GetRoomOrDefault(this.X, (byte)(this.Y + 2)); //two to the north~
			rewardRoom.OpenDoor(Door.South);
		}

		/// <summary>
		/// Adds a prop to the room and sends the entity packet
		/// </summary>
		/// <remarks>
		/// To Whomever is maintaining this code:
		///   I realize that this is rather sloppy and there are
		///   probably hundreds of better ways to do this.
		///   However, I have just spent the last three hours
		///   trying to fix a bug that originated from my
		///   optimizations to other code, so I'm in no
		///   mood to try my luck anywhere else. It is
		///   now your responsibility to clean up after
		///   me.
		///   Good Luck.
		///       -Lios
		/// </remarks>
		private Prop AddProp(int pClass, string pName, string pTitle, string pExtra, string pState, int pX, int pY, float pDirection, int pColor1 = -1, int pColor2 = -1, int pColor3 = -1, int pColor4 = -1, int pColor5 = -1, int pColor6 = -1, int pColor7 = -1, int pColor8 = -1, int pColor9 = -1)
		{
			Prop newProp = new Prop(++_propIndex, pName, pTitle, pClass, this.Floor.Region.Id, pX, pY, pDirection);

			//I hate my life i hate my fucking life i spent a half hour figuring out this bug i hate my life
			newProp.State = pState;

			if (pColor1 > 0)
				newProp.Info.Color1 = (uint)pColor1;
			if (pColor2 > 0)
				newProp.Info.Color2 = (uint)pColor2;
			if (pColor3 > 0)
				newProp.Info.Color3 = (uint)pColor3;
			if (pColor4 > 0)
				newProp.Info.Color4 = (uint)pColor4;
			if (pColor5 > 0)
				newProp.Info.Color5 = (uint)pColor5;
			if (pColor6 > 0)
				newProp.Info.Color6 = (uint)pColor6;
			if (pColor7 > 0)
				newProp.Info.Color7 = (uint)pColor7;
			if (pColor8 > 0)
				newProp.Info.Color7 = (uint)pColor8;
			if (pColor9 > 0)
				newProp.Info.Color8 = (uint)pColor9;

			this.Floor.Region.AddProp(newProp);
			_entities.Add(newProp);
			return newProp;
		}

		private void AddDoor(Door pDoor, bool pLocked)
		{
			int propClass = 10100;

			float pRotation = this.parseDoorPosition(pDoor);

			if (pLocked)
				propClass = 10102;

			string state = "open";
			int color3 = 0xFFFFFF;

			if (pLocked)
			{
				state = "closed";
				color3 = this.Floor.GenerateKey();
			}

			Prop door = this.AddProp(propClass, "", "", "", state, this.Center.X, this.Center.Y, pRotation, 0xFFFFFF, 0xFFFFFF, color3);

			if (pLocked)
			{
				door.Behavior = new PropFunc(
					(Creature pCreature, Prop pProp) =>
					{
						foreach (var item in pCreature.Inventory.Items)
						{
							if (item.Info.Id == (int)DungeonKey.Room)
							{
								if (item.Info.Color1 == pProp.Info.Color3)
								{
									pProp.State = "open";
									Send.PropUpdate(pProp);

									Send.Notice(pCreature, NoticeType.MiddleSystem, "You have opened the door with the key.");
									pCreature.Inventory.Remove(item);
									return;
								}
							}
						}

						Send.Notice(pCreature, NoticeType.MiddleSystem, "There is no matching key.");
					});
			}

			_doors[pDoor] = door;
		}

		//Puzzle Methods
		public void AddFloorUpProps()
		{
			float rotation = this.parseDoorPosition(Door.North);
			float oppositeRotation = this.parseDoorPosition(Door.South);

			if ((this.Doors & Door.EastExit) != 0)
			{
				rotation = this.parseDoorPosition(Door.East);
				oppositeRotation = this.parseDoorPosition(Door.West);
			}
			else if ((this.Doors & Door.SouthExit) != 0)
			{
				rotation = this.parseDoorPosition(Door.South);
				oppositeRotation = this.parseDoorPosition(Door.North);
			}
			else if ((this.Doors & Door.WestExit) != 0)
			{
				rotation = this.parseDoorPosition(Door.West);
				oppositeRotation = this.parseDoorPosition(Door.East);
			}

			if (this.Floor.Parent.EnableStatues)
			{
				Prop statue = this.AddProp(10035, "", "", "", "single", Center.X, Center.Y, oppositeRotation, 0xFFFFFF, 0xFFFFFF, 0xFFFFFF);
				//TODO: Save position.
			}

			Prop stairProp = this.AddProp(10024, "", "", "", "single", Center.X, Center.Y, rotation);

			Prop warpProp = this.AddProp(10039, "_upstairs", "<mini>TO</mini>Upstairs", "", "single", Center.X, Center.Y, rotation);

			warpProp.Behavior = new PropFunc(
				(Creature pCreature, Prop pProp) =>
				{
					Send.CharacterLock(pCreature, Locks.Default);

					pCreature.Warping = true;

					if ((this.Floor.Index - 1) >= 0)
					{
						var previousFloor = this.Floor.Parent.Floors[this.Floor.Index - 1];
						var exitPos = previousFloor.ExitPosition;
						pCreature.SetLocation(previousFloor.Region.Id, exitPos.X, exitPos.Y);
					}
					else
						pCreature.SetLocation(this.Floor.Parent.EntryRegion.Id, 3250, 4250);

					Send.EnterRegion(pCreature as PlayerCreature);
				});
		}

		public void AddFloorDownProps()
		{
			float rotation = this.parseDoorPosition(Door.North);

			if ((this.Doors & Door.EastExit) != 0)
				rotation = this.parseDoorPosition(Door.East);
			else if ((this.Doors & Door.SouthExit) != 0)
				rotation = this.parseDoorPosition(Door.South);
			else if ((this.Doors & Door.WestExit) != 0)
				rotation = this.parseDoorPosition(Door.South);

			Prop stairProp = this.AddProp(10025, "", "", "", "single", Center.X, Center.Y, rotation);

			Prop warpProp = this.AddProp(10039, "_downstairs", "<mini>TO</mini>Downstairs", "", "single", Center.X, Center.Y, rotation);

			warpProp.Behavior = new PropFunc(
				(Creature pCreature, Prop pProp) =>
				{
					Send.CharacterLock(pCreature, Locks.Default);

					pCreature.Warping = true;

					if ((this.Floor.Index + 1) < this.Floor.Parent.Floors.Count)
					{
						var previousFloor = this.Floor.Parent.Floors[this.Floor.Index + 1];
						var exitPos = previousFloor.ExitPosition;
						pCreature.SetLocation(previousFloor.Region.Id, exitPos.X, exitPos.Y);
						Send.EnterRegion(pCreature as PlayerCreature);
					}
					else
					{
						Send.Notice(pCreature, NoticeType.MiddleSystem, "A strange force is preventing you from moving any further.");
						Send.CharacterUnlock(pCreature, Locks.Default);
						pCreature.Warping = false;
						Log.Warning("Player {0} is attempting to move downstairs when no floor is found!");
					}
				});
		}

		public void AddBossProps()
		{
			//Boss Door
			{
				float rotation = this.parseDoorPosition(Door.South);
				int color3 = Floor.GenerateKey(0xFF0000);

				Prop door = this.AddProp(10104, "", "", "", "closed", Center.X, Center.Y, rotation, 0xFFFFFF, 0xFFFFFF, color3);

				door.Behavior = new PropFunc(
					(Creature pCreature, Prop pProp) =>
					{
						//TODO: Go through keys and all that...
						foreach (Item item in pCreature.Inventory.Items)
						{
							if (item.Info.Id == (int)DungeonKey.Boss)
							{
								if (item.Info.Color1 == pProp.Info.Color3)
								{
									//Right key
									pProp.State = "open";
									Send.PropUpdate(pProp);
									this.Floor.Parent.Script.OnBossOpen();
									pCreature.Inventory.Remove(item);
									return;
								}
							}
						}
						Send.Notice(pCreature, NoticeType.MiddleSystem, "There is no matching key.");
					}
					);


				_doors[Door.South] = door;
			}
			//Reward Door
			{
				float rotation = this.parseDoorPosition(Door.North);

				Prop door = this.AddProp(10105, "", "", "", "closed", Center.X, Center.Y, rotation, 0xFFFFFF, 0xFFFFFF);

				door.Behavior = new PropFunc(
					(Creature pCreature, Prop pProp) =>
					{
						Send.Notice(pCreature, NoticeType.MiddleSystem, "You must defeat all enemies before leaving!");
					}
					);

				_doors[Door.North] = door;
			}
		}

		public void AddRewardProps()
		{
			//Chest
			{
				//TODO: Compass Directions
				Prop rewardChest = this.AddProp(10201, "", "", "", "closed_identified", Center.X, Center.Y + 540, 1.570796f);
				rewardChest.Behavior = new PropFunc(
					(Creature pCreature, Prop pProp) =>
					{
						if (pCreature.Inventory.Count((int)DungeonKey.Chest) < 0)
						{
							Send.Notice(pCreature, NoticeType.MiddleSystem, "There is no matching key.");
							return;
						}
						pCreature.Inventory.Remove((int)DungeonKey.Chest, 1);
						//TODO: Set player variable to prevent reward keys from being used for other chests.
						pProp.State = "open";
						Send.PropUpdate(pProp);

						// Play Sound
						Send.PlaySound(pProp, "data/sound/chest_open.wav");

						var pos = pProp.GetPosition();

						var rnd = RandomProvider.Get();
						//Drop rewards
						foreach (var drop in Floor.Parent.Script.GetRewards())
						{
							if (rnd.Next(100) <= drop.Chance)
							{
								Item item = drop;
								var amount = rnd.Next(drop.Minimum, drop.Maximum);
								item.Info.Amount = (ushort)amount;
								item.Info.X = (pos.X + rnd.Next(-50, 51));
								item.Info.Y = (pos.Y + rnd.Next(-50, 51));
								this.Floor.Region.DropItem(item, item.Info.X, item.Info.Y);
							}
						}
					});
			}
			//Exit Statue
			{
				Prop statue = this.AddProp(10035, "", "", "<xml dungeon_name=\"" + this.Floor.Parent.Design + "\" dungeon_id=\"" + this.Floor.Parent.InstanceID + "\"/>", "single", Center.X, Center.Y, 4.712389f);

				statue.Behavior = new PropFunc(
					(Creature pCreature, Prop pProp) =>
					{
						this.Floor.Parent.RemovePlayer(pCreature);
					}
					);
			}
		}

		public void AddSpawnChest()
		{
			Prop chest = this.AddProp(10200, "", "", "", "closed", Center.X + 247, Center.Y + 101, -2.907847f);

			chest.Behavior = new PropFunc(
				(Creature pCreature, Prop pProp) =>
				{
					this.CloseAllDoors();
					pProp.State = "open";
					Send.PropUpdate(pProp);

					var pos = pProp.GetPosition();

					//Drop gold
					var gold = new Item(2000);
					var rnd = RandomProvider.Get();
					gold.Info.Amount = (ushort)rnd.Next(25, 75);
					this.Floor.Region.DropItem(gold, pos.X, pos.Y);

					this.Spawns.OnClearFunc = new SpawnPattern.OnClear(() =>
					{
						this.OpenAllDoors();
						this.Cleared = true;
					});

					this.Spawns.Start();
				});
		}

		public void AddSwitches()
		{

		}

		//Utility Methods
		private float parseDoorPosition(Door pDoor)
		{
			var rotation = Direction.North; //Default: North

			switch (pDoor)
			{
				case Door.East:
				case Door.EastExit:
				case Door.EastLocked:
					rotation = Direction.East;
					break;
				case Door.South:
				case Door.SouthExit:
				case Door.SouthLocked:
					rotation = Direction.South;
					break;
				case Door.West:
				case Door.WestExit:
				case Door.WestLocked:
					rotation = Direction.West;
					break;
			}

			return rotation;
		}
	}
}
