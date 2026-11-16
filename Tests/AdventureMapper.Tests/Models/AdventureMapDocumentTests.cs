using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using AdventureMapper.Tests.Support;

namespace AdventureMapper.Tests.Models;

public class AdventureMapDocumentTests
{
	[Fact]
	public void CreateNew_StartsWithAGroundLayerAndAnEntrance()
	{
		var document = AdventureMapDocument.CreateNew();

		var layer = Assert.Single(document.Layers);
		Assert.Equal(0, layer.Number);
		Assert.False(string.IsNullOrWhiteSpace(layer.Name));

		var room = Assert.Single(document.Rooms);
		Assert.Equal(1, room.Id);
		Assert.Equal(0, room.Layer);
		Assert.Equal(0, room.Column);
		Assert.Equal(0, room.Row);
		Assert.False(string.IsNullOrWhiteSpace(room.Name));
		Assert.False(string.IsNullOrWhiteSpace(room.Description));
		Assert.Equal(RoomKind.Normal, room.Kind);
		Assert.Equal(2, document.NextRoomId);
		Assert.False(string.IsNullOrWhiteSpace(document.Title));
	}

	[Fact]
	public void CreateNew_UsesTheRequestedTitle()
	{
		Assert.Equal("The Sunken Vault", AdventureMapDocument.CreateNew("The Sunken Vault").Title);
	}

	[Fact]
	public void AddRoom_AssignsSequentialIdsAndKeepsGivenNames()
	{
		var document = AdventureMapDocument.CreateNew();

		var second = document.AddRoom(0, 1, 0, "Hallway");
		var third = document.AddRoom(0, 2, 0);

		Assert.Equal(2, second.Id);
		Assert.Equal(3, third.Id);
		Assert.Equal("Hallway", second.Name);
		Assert.Contains("3", third.Name);
		Assert.Equal(4, document.NextRoomId);
	}

	[Fact]
	public void AddRoom_CreatesTheLayerItNeeds()
	{
		var document = TestDocuments.NewBlank();

		document.AddRoom(3, 0, 0, "Attic");

		var layer = Assert.Single(document.Layers);
		Assert.Equal(3, layer.Number);
		Assert.False(string.IsNullOrWhiteSpace(layer.Name));
	}

	[Fact]
	public void GetOrCreateLayer_ReturnsExistingLayersAndKeepsThemSorted()
	{
		var document = TestDocuments.NewBlank();

		var upper = document.GetOrCreateLayer(3);
		var lower = document.GetOrCreateLayer(-1);
		var ground = document.GetOrCreateLayer(0);

		Assert.Same(upper, document.GetOrCreateLayer(3));
		Assert.Equal(new[] { -1, 0, 3 }, document.Layers.Select(layer => layer.Number));
		Assert.Same(lower, document.GetOrCreateLayer(-1, "Cellars"));
		Assert.Equal(MapRules.DefaultLayerName(-1), lower.Name);
		Assert.Equal(0, ground.Number);
	}

	[Fact]
	public void GetOrCreateLayer_NamesNewLayersAfterTheirFloor()
	{
		var document = TestDocuments.NewBlank();

		var layer = document.GetOrCreateLayer(2);

		Assert.Equal(MapRules.DefaultLayerName(2), layer.Name);
		Assert.False(string.IsNullOrWhiteSpace(layer.Name));
	}

	[Fact]
	public void FindRoom_MatchesLayerAndId()
	{
		var document = AdventureMapDocument.CreateNew();
		var room = document.Rooms[0];

		Assert.Same(room, document.FindRoom(0, room.Id));
		Assert.Same(room, document.FindRoom(new RoomReference(0, room.Id)));
		Assert.Null(document.FindRoom(1, room.Id));
		Assert.Null(document.FindRoom(0, room.Id + 1));
	}

	[Fact]
	public void RoomsOn_OnlyReturnsTheRequestedLayer()
	{
		var document = AdventureMapDocument.CreateNew();
		document.AddRoom(1, 0, 0, "Upstairs");

		Assert.Single(document.RoomsOn(1));
		Assert.Single(document.RoomsOn(0));
		Assert.Empty(document.RoomsOn(2));
	}

	[Fact]
	public void SortedRooms_OrdersByLayerRowAndColumn()
	{
		var document = TestDocuments.NewBlank();
		var later = document.AddRoom(1, 0, 0, "Later");
		var south = document.AddRoom(0, 1, 2, "South");
		var north = document.AddRoom(0, 1, 0, "North");
		var west = document.AddRoom(0, 0, 0, "West");

		Assert.Equal(new[] { west, north, south, later }, document.SortedRooms);
	}

	[Fact]
	public void IsCellFree_DetectsOccupiedCells()
	{
		var document = AdventureMapDocument.CreateNew();

		Assert.False(document.IsCellFree(0, 0, 0));
		Assert.True(document.IsCellFree(0, 1, 0));
		Assert.True(document.IsCellFree(1, 0, 0));
	}

	[Fact]
	public void Connect_StoresTheTargetAsLayerRoom()
	{
		var document = AdventureMapDocument.CreateNew();
		var from = document.Rooms[0];
		var to = document.AddRoom(2, 4, -1, "Tower top");

		var connection = document.Connect(from, MapDirection.Up, to);

		Assert.Equal(MapDirection.Up, connection.Direction);
		Assert.Equal(2, connection.TargetLayer);
		Assert.Equal(to.Id, connection.TargetRoom);
		Assert.Equal($"{to.Layer}-{to.Id}", connection.TargetReference);
		Assert.Same(connection, from.FindConnection(MapDirection.Up));
	}

	[Fact]
	public void Connect_ReplacesTheSameDirectionByDefault()
	{
		var document = AdventureMapDocument.CreateNew();
		var from = document.Rooms[0];
		var first = document.AddRoom(0, 1, 0, "First");
		var second = document.AddRoom(0, 1, 1, "Second");

		document.Connect(from, MapDirection.East, first);
		document.Connect(from, MapDirection.East, second);

		var connection = Assert.Single(from.Connections);
		Assert.Equal(second.Id, connection.TargetRoom);
	}

	[Fact]
	public void Connect_CanKeepExistingConnections()
	{
		var document = AdventureMapDocument.CreateNew();
		var from = document.Rooms[0];
		var target = document.AddRoom(0, 1, 0, "Target");

		document.Connect(from, MapDirection.East, target, replaceExisting: false);
		document.Connect(from, MapDirection.East, target, replaceExisting: false);

		Assert.Equal(2, from.Connections.Count);
	}

	[Fact]
	public void RemoveRoom_ClearsConnectionsThatPointedAtIt()
	{
		var document = AdventureMapDocument.CreateNew();
		var entrance = document.Rooms[0];
		var hall = document.AddRoom(0, 1, 0, "Hall");
		document.Connect(entrance, MapDirection.East, hall);
		document.Connect(hall, MapDirection.West, entrance);

		document.RemoveRoom(hall);

		Assert.DoesNotContain(hall, document.Rooms);
		Assert.Empty(entrance.Connections);
	}

	[Fact]
	public void RemoveRoom_PrunesLayersLeftEmpty()
	{
		var document = AdventureMapDocument.CreateNew();
		var attic = document.AddRoom(2, 0, 0, "Attic");

		document.RemoveRoom(attic);

		Assert.DoesNotContain(document.Layers, layer => layer.Number == 2);
		Assert.Contains(document.Layers, layer => layer.Number == 0);
	}

	[Fact]
	public void PruneEmptyLayers_AlwaysKeepsTheGroundFloor()
	{
		var document = AdventureMapDocument.CreateNew();
		document.AddRoom(1, 0, 0, "Upstairs");
		document.Rooms.Clear();

		document.PruneEmptyLayers();

		var layer = Assert.Single(document.Layers);
		Assert.Equal(0, layer.Number);
	}

	[Fact]
	public void Touch_UpdatesTheModifiedTime()
	{
		var document = new AdventureMapDocument { ModifiedUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

		document.Touch();

		Assert.True(document.ModifiedUtc > new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
	}

	[Fact]
	public void NewDocument_UsesLocalisedDefaults()
	{
		var document = AdventureMapDocument.CreateNew();

		Assert.Equal(L.T("Default_Untitled"), document.Title);
		Assert.Equal(L.T("Default_Entrance"), document.Rooms[0].Name);
		Assert.Equal(L.T("Default_EntranceDescription"), document.Rooms[0].Description);
	}
}
