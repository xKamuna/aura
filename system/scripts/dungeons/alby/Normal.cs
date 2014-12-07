using Aura.Channel.World.Dungeons;

public class Alby_Normal_782630562 : DungeonScript
{
	public override void OnLoad()
	{
		SetLobby(DungeonLobby.Alby);
		SetLevel(DungeonLevel.Normal);
		SetItemDropped(51011);
		SetDesign("tircho_alby_dungeon");
		SetFloorplan(0);
		SetSeed(782630562);

		AddDrop(2000, 100, 132, 634); //Gold
		AddDrop(51102, 35, 1, 5); //Mana Herbs
		AddDrop(71017, 10, 1, 3); //White Spider Fomor Scroll
		AddDrop(71019, 10, 1, 1); //Red Spider Fomor Scroll
	}

	public override void Build()
	{
		DungeonFloor floor = AddFloor(4266547036, 1227, 10361);
		SetEntrance(floor, 0, 4, 0x0005, Door.SouthExit, Door.East);

		var firstRoom = AddRoom(floor, 0, 1, 0x0003, Puzzle.SpawnChest, Door.North, Door.South);
		AddSpawnGroup(firstRoom, DungeonKey.None, Spawn(50002, 2));
		AddSpawnGroup(firstRoom, DungeonKey.Room, Spawn(30001, 2));

		AddRoom(floor, 1, 1, 0x0007, Puzzle.None, Door.South, Door.EastLocked);
	
		var secondRoom = AddRoom(floor, 2, 1, 0x000B, Puzzle.SpawnChest, Door.West, Door.East);
		AddSpawnGroup(secondRoom, DungeonKey.Boss, Spawn(50003, 1));
		
		AddRoom(floor, 3, 1, 0x000C, Puzzle.None, Door.East);

		SetBossroom(floor, 3, 2, 0x000D);
	}

	public override void OnBossOpen()
	{
		SpawnBoss(Spawn(50003, 2));
	}
}