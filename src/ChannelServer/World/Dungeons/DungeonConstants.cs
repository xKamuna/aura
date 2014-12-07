using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aura.Channel.World.Dungeons
{
	/**
	 * 
	 * Things to consider adding:
	 *    Chest positions for rooms (0-8, also four corners)
	 *
	 **/
	public enum DungeonKey
	{
		None = 0,
		Chest = 70028,
		Room = 70029,
		Boss = 70030
	}

	public enum Door
	{
		None = 0x000, //Not really needed by anything but ok

		//Normal Doors
		North = 0x001,
		East = 0x002,
		South = 0x004,
		West = 0x008,

		//Locked Doors
		NorthLocked = 0x010,
		EastLocked = 0x020,
		SouthLocked = 0x040,
		WestLocked = 0x080,

		//Dungeon Entrance/Exit positions
		NorthExit = 0x100,
		EastExit = 0x100,
		SouthExit = 0x400,
		WestExit = 0x800
	}

	public enum Puzzle
	{
		None = 0,
		//Implimented
		SpawnChest,
		Switches,

		//Special
		Reward,
		Boss,
		FloorUp,
		FloorDown,

		//Unimlipmented
		AutoSpawn,
		Treasure,
		HerbGarden
	}

	public enum DoorProp : int
	{
		Normal = 10100,
		Unk1 = 10101,
		Locked = 10102,
		Unk2 = 10103,
		Boss = 10104,
		Reward = 10105
	}

	public enum DungeonLevel : int
	{
		Beginner = 0,
		Normal,
		Basic,
		Intermediate,
		Advanced,
		Boss
	}

	public enum DungeonLobby : int
	{
		Beta = 0,

		//Tir Chonaill
		Alby = 13,
/*		AlbyHardMode,
		Ciar,
		CiarHardmode,
		//Tir Na Nog
		Albey,
		//Dunbarton
		Math,
		Rabbie,
		//Gairech Hill
		Fiodh,
		//Bangor
		Barri,
		//Bangor (Another World)
		Baol,
		//Sen Mag
		Peaca,
		//Emain Macha
		Rundal,
		RundalHardMode,
		Coill,
		//Misc
		AbbNeaghCastle,
		DugaldCastle,
		SenMagCastle,
		SliabCuilinCastle,

		//Iria
		Longa,
		Maiz,
		Par
 */
	}
}
