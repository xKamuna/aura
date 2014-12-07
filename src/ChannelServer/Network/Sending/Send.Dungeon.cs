// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aura.Channel.World.Entities;
using Aura.Shared.Network;
using Aura.Channel.World;
using Aura.Shared.Util;
using Aura.Shared.Mabi.Const;
using Aura.Channel.World.Dungeons;

namespace Aura.Channel.Network.Sending
{
	public static partial class Send
	{
		/// <summary>
		/// Sends the initial dungeon information to the given creature
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="d"></param>
		public static void DungeonInfo(Creature creature, Dungeon d)
		{
			var packet = new Packet(Op.DungeonInfo, MabiId.Broadcast);

			packet.PutLong(creature.EntityId);
			packet.PutLong(d.InstanceID);

			packet.PutByte(1);
			packet.PutString(d.Design);

			packet.PutInt(d.ItemDropped);
			packet.PutUInt(d.Seed);
			packet.PutInt(d.Floorplan);

			packet.PutInt(d.Floors.Count + 1); //Floor Count + Entry
			packet.PutInt(d.EntryRegion.Id);
			foreach(var floor in d.Floors)
				packet.PutInt(floor.Region.Id);

			packet.PutString("<option/>");

			packet.PutInt(d.Floors.Count); //Floor Count?
			
			foreach(var floor in d.Floors)
			{
				packet.PutInt(floor.Rooms.Values.Count);
				foreach(var room in floor.Rooms.Values)
				{
					packet.PutByte(room.X);
					packet.PutByte(room.Y);
				}
			}

			packet.PutInt(0); //? look at ciar info

			packet.PutInt(d.Floors.Count); //Floor Count
			foreach(var floor in d.Floors)
			{
				packet.PutUInt(0); //Floor seed or 0 apparently
				packet.PutInt(0); //Somethin.
			}

			Log.Info("Trying to send dungeon packet...");

			creature.Client.Send(packet);
		}

		public static void DungeonWarp(Creature creature)
		{
			Position pos = creature.GetPosition();

			var packet = new Packet(Op.DungeonWarp, creature.EntityId);

			packet.PutInt(creature.Region.Id);
			packet.PutInt(0);

			creature.Client.Send(packet);
		}
	}
}
