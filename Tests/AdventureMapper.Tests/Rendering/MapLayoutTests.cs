using AdventureMapper.Models;
using AdventureMapper.Rendering;
using SkiaSharp;

namespace AdventureMapper.Tests.Rendering;

public class MapLayoutTests
{
	[Fact]
	public void Build_EmptyLayer_UsesTheMinimumCanvasSize()
	{
		var layout = MapLayout.Build(new AdventureMapDocument(), 0, minWidth: 800f, minHeight: 600f);

		Assert.Empty(layout.Rooms);
		Assert.Equal(800f, layout.Width);
		Assert.Equal(600f, layout.Height);
		Assert.Null(layout.HitTest(new SKPoint(100, 100)));
	}

	[Fact]
	public void Build_SingleRoom_SitsAtTheMargin()
	{
		var document = new AdventureMapDocument();
		var room = document.AddRoom(0, 0, 0, "Only");

		var layout = MapLayout.Build(document, 0);

		var bounds = layout.BoundsOf(room);
		Assert.Equal(MapLayout.Margin, bounds.Left);
		Assert.Equal(MapLayout.Margin, bounds.Top);
		Assert.Equal(MapLayout.RoomWidth, bounds.Width);
		Assert.Equal(MapLayout.RoomHeight, bounds.Height);
	}

	[Fact]
	public void Build_OffsetsRoomsByTheirGridPosition()
	{
		var document = new AdventureMapDocument();
		var origin = document.AddRoom(0, 0, 0, "Origin");
		var east = document.AddRoom(0, 2, 0, "East");
		var south = document.AddRoom(0, 0, 3, "South");

		var layout = MapLayout.Build(document, 0);

		Assert.Equal(MapLayout.Margin + 2 * MapLayout.CellWidth, layout.BoundsOf(east).Left);
		Assert.Equal(MapLayout.Margin + 3 * MapLayout.CellHeight, layout.BoundsOf(south).Top);
		Assert.Equal(MapLayout.Margin, layout.BoundsOf(origin).Left);
	}

	[Fact]
	public void Build_NormalisesNegativeCoordinatesToTheMargin()
	{
		var document = new AdventureMapDocument();
		var far = document.AddRoom(0, -2, -1, "Far");
		var origin = document.AddRoom(0, 0, 0, "Origin");

		var layout = MapLayout.Build(document, 0);

		Assert.Equal(MapLayout.Margin, layout.BoundsOf(far).Left);
		Assert.Equal(MapLayout.Margin, layout.BoundsOf(far).Top);
		Assert.Equal(MapLayout.Margin + 2 * MapLayout.CellWidth, layout.BoundsOf(origin).Left);
		Assert.Equal(MapLayout.Margin + 1 * MapLayout.CellHeight, layout.BoundsOf(origin).Top);
	}

	[Fact]
	public void Build_GrowsToFitTheFurthestRoom()
	{
		var document = new AdventureMapDocument();
		document.AddRoom(0, 0, 0, "First");
		document.AddRoom(0, 5, 4, "Last");

		var layout = MapLayout.Build(document, 0, minWidth: 100f, minHeight: 100f);

		Assert.Equal(
			MapLayout.Margin * 2 + 5 * MapLayout.CellWidth + MapLayout.RoomWidth,
			layout.Width);
		Assert.Equal(
			MapLayout.Margin * 2 + 4 * MapLayout.CellHeight + MapLayout.RoomHeight,
			layout.Height);
	}

	[Fact]
	public void Build_KeepsTheMinimumSizeForSmallMaps()
	{
		var document = new AdventureMapDocument();
		document.AddRoom(0, 0, 0, "Only");

		var layout = MapLayout.Build(document, 0, minWidth: 5000f, minHeight: 4000f);

		Assert.Equal(5000f, layout.Width);
		Assert.Equal(4000f, layout.Height);
	}

	[Fact]
	public void Build_OnlyLaysOutTheRequestedLayer()
	{
		var document = new AdventureMapDocument();
		document.AddRoom(0, 0, 0, "Ground");
		var attic = document.AddRoom(1, 0, 0, "Attic");

		var layout = MapLayout.Build(document, 1);

		Assert.Equal(new[] { attic }, layout.Rooms);
		Assert.Equal(SKRect.Empty, layout.BoundsOf(document.Rooms[0]));
	}

	[Fact]
	public void Rooms_AreOrderedByRowThenColumn()
	{
		var document = new AdventureMapDocument();
		var southEast = document.AddRoom(0, 1, 1, "South east");
		var northEast = document.AddRoom(0, 1, 0, "North east");
		var northWest = document.AddRoom(0, 0, 0, "North west");

		var layout = MapLayout.Build(document, 0);

		Assert.Equal(new[] { northWest, northEast, southEast }, layout.Rooms);
	}

	[Fact]
	public void HitTest_FindsRoomsAndIgnoresTheGapsBetweenThem()
	{
		var document = new AdventureMapDocument();
		var room = document.AddRoom(0, 0, 0, "Room");

		var layout = MapLayout.Build(document, 0);
		var bounds = layout.BoundsOf(room);

		Assert.Same(room, layout.HitTest(new SKPoint(bounds.MidX, bounds.MidY)));
		Assert.Same(room, layout.HitTest(new SKPoint(bounds.Left + 1, bounds.Top + 1)));
		Assert.Null(layout.HitTest(new SKPoint(bounds.Left - 1, bounds.Top)));
		Assert.Null(layout.HitTest(new SKPoint(bounds.Right + MapLayout.ColumnGap / 2, bounds.MidY)));
	}

	[Fact]
	public void HandlePosition_PlacesHandlesAroundTheRoom()
	{
		var bounds = SKRect.Create(100, 100, MapLayout.RoomWidth, MapLayout.RoomHeight);

		var north = MapLayout.HandlePosition(bounds, MapDirection.North);
		var east = MapLayout.HandlePosition(bounds, MapDirection.East);
		var southWest = MapLayout.HandlePosition(bounds, MapDirection.SouthWest);

		Assert.Equal(bounds.MidX, north.X);
		Assert.True(north.Y < bounds.Top);
		Assert.True(east.X > bounds.Right);
		Assert.Equal(bounds.MidY, east.Y);
		Assert.True(southWest.X < bounds.Left);
		Assert.True(southWest.Y > bounds.Bottom);

		var handles = Enum.GetValues<MapDirection>()
			.Where(MapRules.IsCompass)
			.Select(direction => MapLayout.HandlePosition(bounds, direction))
			.ToList();
		Assert.Equal(8, handles.Distinct().Count());
	}

	[Fact]
	public void HandlePosition_StaysClearOfTheNeighbouringRoom()
	{
		var document = new AdventureMapDocument();
		var west = document.AddRoom(0, 0, 0, "West");
		var east = document.AddRoom(0, 1, 0, "East");
		var layout = MapLayout.Build(document, 0);
		var westBounds = layout.BoundsOf(west);
		var eastBounds = layout.BoundsOf(east);

		var eastHandle = MapLayout.HandlePosition(westBounds, MapDirection.East);
		var westHandle = MapLayout.HandlePosition(eastBounds, MapDirection.West);

		Assert.True(eastHandle.X + MapLayout.HandleRadius < eastBounds.Left);
		Assert.True(westHandle.X - MapLayout.HandleRadius > westBounds.Right);
	}

	[Fact]
	public void HitTestLink_FindsTheLineBetweenTwoRooms()
	{
		var document = new AdventureMapDocument();
		var entrance = document.AddRoom(0, 0, 0, "Entrance");
		var hall = document.AddRoom(0, 1, 0, "Hall");
		document.Connect(entrance, MapDirection.East, hall);
		document.Connect(hall, MapDirection.West, entrance);
		var layout = MapLayout.Build(document, 0);

		var from = layout.BoundsOf(entrance);
		var to = layout.BoundsOf(hall);
		var midpoint = new SKPoint((from.MidX + to.MidX) / 2f, from.MidY);

		var hit = layout.HitTestLink(document, midpoint);
		Assert.NotNull(hit);
		Assert.Contains((hit.Value.From, hit.Value.To), new[] { (entrance, hall), (hall, entrance) });
		Assert.Null(layout.HitTestLink(document, new SKPoint(5, 5)));
	}

	[Fact]
	public void HitTestLink_IgnoresRoomsWithoutLinks()
	{
		var document = new AdventureMapDocument();
		document.AddRoom(0, 0, 0, "Alone");
		var layout = MapLayout.Build(document, 0);

		Assert.Null(layout.HitTestLink(document, new SKPoint(200, 200)));
	}

	[Fact]
	public void Build_AppliesTheFreePlacementOffsetOfARoom()
	{
		var document = new AdventureMapDocument();
		var room = document.AddRoom(0, 0, 0, "Moved");
		room.OffsetX = 60;
		room.OffsetY = -25;

		var layout = MapLayout.Build(document, 0);
		var bounds = layout.BoundsOf(room);

		Assert.Equal(MapLayout.Margin + 60f, bounds.Left, 3);
		Assert.Equal(MapLayout.Margin - 25f, bounds.Top, 3);
		Assert.Equal(MapLayout.RoomWidth, bounds.Width, 3);
	}

	[Fact]
	public void Build_GrowsToFitAMovedRoom()
	{
		var document = new AdventureMapDocument();
		var room = document.AddRoom(0, 0, 0, "Far");
		room.OffsetX = 500;

		var layout = MapLayout.Build(document, 0, minWidth: 100f, minHeight: 100f);

		Assert.Equal(MapLayout.Margin + 500f + MapLayout.RoomWidth + MapLayout.Margin, layout.Width, 3);
	}

	[Fact]
	public void HitTest_FollowsAMovedRoom()
	{
		var document = new AdventureMapDocument();
		var room = document.AddRoom(0, 0, 0, "Moved");
		room.OffsetX = 300;
		room.OffsetY = 200;

		var layout = MapLayout.Build(document, 0);
		var bounds = layout.BoundsOf(room);

		Assert.Same(room, layout.HitTest(new SKPoint(bounds.MidX, bounds.MidY)));
		Assert.Null(layout.HitTest(new SKPoint(MapLayout.Margin + 5, MapLayout.Margin + 5)));
	}

	[Fact]
	public void HitTestLink_FollowsAMovedRoom()
	{
		var document = new AdventureMapDocument();
		var entrance = document.AddRoom(0, 0, 0, "Entrance");
		var hall = document.AddRoom(0, 1, 0, "Hall");
		document.Connect(entrance, MapDirection.East, hall);
		document.Connect(hall, MapDirection.West, entrance);
		hall.OffsetX = 140;
		hall.OffsetY = 90;

		var layout = MapLayout.Build(document, 0);
		var from = layout.BoundsOf(entrance);
		var to = layout.BoundsOf(hall);
		var midpoint = new SKPoint((from.MidX + to.MidX) / 2f, (from.MidY + to.MidY) / 2f);

		var hit = layout.HitTestLink(document, midpoint);

		Assert.NotNull(hit);
		Assert.Contains((hit.Value.From, hit.Value.To), new[] { (entrance, hall), (hall, entrance) });
	}

	[Fact]
	public void BoundsOf_UnknownRoom_IsEmpty()
	{
		var layout = MapLayout.Build(new AdventureMapDocument(), 0);

		Assert.Equal(SKRect.Empty, layout.BoundsOf(new MapRoom { Id = 99 }));
	}
}
