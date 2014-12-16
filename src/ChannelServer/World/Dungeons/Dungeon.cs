using Aura.Channel.Network.Sending;
using Aura.Channel.Scripting.Scripts;
using Aura.Channel.World.Entities;
using Aura.Shared.Mabi.Const;
using Aura.Shared.Network;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aura.Channel.World.Dungeons
{
	public class Dungeon
	{

		private Dictionary<Creature, Dictionary<string, string>> _vars = new Dictionary<Creature, Dictionary<string, string>>();
		private DungeonScript _script;
		private bool _active = false;

		public DungeonScript Script { get { return _script; } }
		//Instance ID Is Region ID Plus random number until exec gets them working <3
		public long InstanceID { get; set; }
		public string Design { get { return _script.Design; } }
		public int ItemDropped { get { return _script.ItemClass; } }
		public uint Seed { get { return _script.Seed; } }
		public int Floorplan { get { return _script.Floorplan; } }

		public DungeonLobby Lobby { get { return _script.Lobby; } }
		public Region EntryRegion { get; set; }

		public List<DungeonFloor> Floors = new List<DungeonFloor>();

		public bool EnableStatues = true;

		public List<Creature> Players = new List<Creature>();
		public List<Creature> Creators = new List<Creature>();

		public Dungeon(Creature pCreator, int pRegionStart, DungeonScript pScript, out int nextAvailableRegion)
		{
			//TODO: Party support when parties are once again added
			Creators.Add(pCreator);

			if (!ChannelServer.Instance.World.HasRegion(pRegionStart))
				ChannelServer.Instance.World.AddRegion(pRegionStart);

			this.EntryRegion = ChannelServer.Instance.World.GetRegion(pRegionStart);

			_script = Activator.CreateInstance(pScript.GetType()) as DungeonScript;
			_script.OnLoad();
			_script.Dungeon = this;
			_script.RegionIndex = ++pRegionStart;

			this.InstanceID = ChannelServer.Instance.World.DungeonManager.NewInstance();

			this.Build();

			Log.Info("Region Index: {0}", _script.RegionIndex);

			//Set up props
			long entryPropIndex = 0x00A0000000000000 + ((long)this.EntryRegion.Id << 32) + ((long)0x0001 << 16);

			var leaveStatue = new Prop(entryPropIndex + 2, "", "", 0, this.EntryRegion.Id, 3250, 3250, Direction.North);
			leaveStatue.Behavior = new PropFunc(
				(Creature pCreature, Prop pProp) =>
				{
					this.RemovePlayer(pCreature);
				});

			var moveDownProp = new Prop(entryPropIndex + 3,"", "",0, this.EntryRegion.Id, 3250, 4500, Direction.North);
			moveDownProp.Behavior = new PropFunc(
				(Creature pCreature, Prop pProp) =>
				{
					var ePos = Floors[0].EntrancePosition;
					pCreature.Warp(Floors[0].Region.Id, ePos.X, ePos.Y);

					Send.DungeonWarp(pCreature);
					Send.WarpRegion(pCreature as PlayerCreature);
				});

			this.EntryRegion.AddProp(leaveStatue);
			this.EntryRegion.AddProp(moveDownProp);

			//Warp player in
			this.AddPlayer(pCreator);

			nextAvailableRegion = _script.RegionIndex;
			Log.Info("Next Available Region {0}", nextAvailableRegion);
		}

		public void AddPlayer(Creature pPlayer, bool warp = true)
		{
			this.Players.Add(pPlayer);
			_vars.Add(pPlayer, new Dictionary<string, string>());

			if (_active && warp)
				this.WarpPlayerIn(pPlayer, warp);
		}

		public void RemovePlayer(Creature pPlayer, bool pWarp = true)
		{
			if (!this.Players.Contains(pPlayer))
				return;

			// Remove from player variables
			_vars.Remove(pPlayer);

			//Remove Keys...
			List<Item> toRemove = new List<Item>();
			foreach (var item in pPlayer.Inventory.Items)
			{
				if (Enum.IsDefined(typeof(DungeonKey), item.Info.Id))
				{
					toRemove.Add(item);
				}
			}

			foreach (var item in toRemove)
				pPlayer.Inventory.Remove(item);

			this.Players.Remove(pPlayer);

			this.WarpPlayerOut(pPlayer, pWarp);

			if (this.Players.Count <= 0)
				this.Dispose();
		}

		public void Start()
		{
			if (_active)
				return;

			foreach (var player in this.Players)
				this.WarpPlayerIn(player);

			_active = true;
		}

		public void WarpPlayerIn(Creature pPlayer, bool pSendWarp = true)
		{
			var pos = pPlayer.GetPosition();
			Log.Info("Sending dungeon information...");
			Send.CharacterLock(pPlayer, Locks.Default);
			if (!pSendWarp)
				return;

			pPlayer.SetLocation(this.EntryRegion.Id, pos.X, pos.Y);
			pPlayer.Warping = true;
			Send.DungeonInfo(pPlayer, this);
		}

		public void FinishCustomWarp(Creature pPlayer, int regionId, int x, int y)
		{
			pPlayer.SetLocation(regionId, x, y);
			pPlayer.Warping = true;
			Send.DungeonInfo(pPlayer, this);
		}

		public void WarpPlayerOut(Creature pPlayer, bool pSendWarp = true)
		{
			var exitPos = this.ExitPosition;

			pPlayer.SetLocation(exitPos.Item1, exitPos.Item2, exitPos.Item3);

			if (!pSendWarp)
				return;

			pPlayer.Warping = true;
			Send.CharacterLock(pPlayer, Locks.Default);
			Send.EnterRegion(pPlayer as PlayerCreature);
		}

		public void Broadcast(Packet p)
		{
			foreach (var player in this.Players)
			{
				player.Client.Send(p);
			}
		}

		public void Build()
		{
			_script.Build();

			foreach (var floor in Floors)
				floor.Build();
		}

		public Tuple<int, int, int> ExitPosition
		{
			get
			{
				//Defaults to Tir Cho Town Square~
				var region = 1;
				var x = 12800;
				var y = 38300;

				switch (this.Lobby)
				{
					case DungeonLobby.Alby:
						region = 13;
						x = 2500;
						y = 2500;
						break;
					default:
						Log.Info("Unknown Dungeon Lobby Info {0}", this.Lobby.ToString("G"));
						break;
				}

				return Tuple.Create(region, x, y);
			}
		}

		public DungeonFloor GetFloorByRegion(int pRegionId)
		{
			return this.Floors.FirstOrDefault(a => a.Region.Id == pRegionId);
		}

		public void SetPlayerVariable(Creature pCreature, string pKey, string pValue)
		{
			if(!_vars.ContainsKey(pCreature)){
				return;
			}

			var dictionary = _vars[pCreature];
			if (dictionary.ContainsKey(pKey))
			{
				dictionary[pKey] = pValue;
			}
			else
			{
				dictionary.Add(pKey, pValue);
			}
		}

		public string GetPlayerVariable(Creature pCreature, string pKey)
		{
			if (!_vars.ContainsKey(pCreature))
			{
				return null;
			}

			var dictionary = _vars[pCreature];

			if (dictionary.ContainsKey(pKey))
			{
				return dictionary[pKey];
			}

			return null;
		}

		public void Cleared()
		{
			//yea
			foreach (var player in Players)
			{
				var key = new Item((int)DungeonKey.Chest);
				player.Inventory.Add(key, true);
				Send.Effect(player, Effect.PickUpKey, key.Info.Id, key.Info.Color1, key.Info.Color2, key.Info.Color3);
				var bossFloor = this.Floors[this.Floors.Count - 1];
				bossFloor.BossRoom.OpenAllDoors();
				bossFloor.Exit.OpenAllDoors();
			}

			var exit = this.Floors[this.Floors.Count - 1].Exit;			
			// Exit Statue
			Send.PropInteraction(exit.ExitStatue, "directed_ask(" + exit.X + ", " + exit.Y + ");", "message:s:Do you really want to leave the dungeon?", this);
			// Reward Chests
			foreach (var chest in exit.Chests){
				Send.PropInteraction(chest, "directed_ask(" + exit.X + ", " + exit.Y + ");", "message:s:Do you want to open this chest?", this);
			}
		}

		public void Dispose()
		{
			//TODO: Free up region
		}
	}
}
