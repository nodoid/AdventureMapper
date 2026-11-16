using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;

namespace AdventureMapper.Tests.Models;

public class MapRoomTests
{
	[Fact]
	public void Description_IsTruncatedToTheLimit()
	{
		var room = new MapRoom { Description = new string('a', MapConstants.MaxDescriptionLength + 40) };

		Assert.Equal(MapConstants.MaxDescriptionLength, room.Description.Length);
		Assert.Equal(new string('a', MapConstants.MaxDescriptionLength), room.Description);
	}

	[Fact]
	public void Description_AcceptsExactlyTheLimit()
	{
		var text = new string('b', MapConstants.MaxDescriptionLength);
		Assert.Equal(text, new MapRoom { Description = text }.Description);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Description_TreatsMissingTextAsEmpty(string? text)
	{
		Assert.Equal(string.Empty, new MapRoom { Description = text! }.Description);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Truncate_TreatsMissingTextAsEmpty(string? text)
	{
		Assert.Equal(string.Empty, MapRoom.Truncate(text));
	}

	[Fact]
	public void Truncate_LeavesShortTextAlone()
	{
		Assert.Equal("A dusty hall.", MapRoom.Truncate("A dusty hall."));
	}

	[Theory]
	[InlineData(RoomKind.Normal, 0, 0)]
	[InlineData(RoomKind.Normal, 2, 2)]
	[InlineData(RoomKind.Normal, -1, -1)]
	[InlineData(RoomKind.Pit, 1, 1)]
	[InlineData(RoomKind.Pit, -2, -2)]
	[InlineData(RoomKind.Bridge, 0, 1)]
	[InlineData(RoomKind.Bridge, 3, 4)]
	[InlineData(RoomKind.Bridge, -1, 0)]
	public void WalkwayLayer_RaisesBridgesOneLayer(RoomKind kind, int layer, int expected)
	{
		var room = new MapRoom { Kind = kind, Layer = layer };

		Assert.Equal(expected, room.WalkwayLayer);
	}

	[Fact]
	public void Reference_DisplayAndToString_UseLayerRoomNotation()
	{
		var room = new MapRoom { Id = 7, Layer = -1, Name = "Cave" };

		Assert.Equal("-1-7", room.Reference);
		Assert.Equal("-1-7 Cave", room.Display);
		Assert.Equal("-1-7 Cave", room.ToString());
	}

	[Fact]
	public void FindConnection_MatchesTheDirection()
	{
		var room = new MapRoom();
		var east = new MapConnection { Direction = MapDirection.East, TargetLayer = 0, TargetRoom = 2 };
		room.Connections.Add(east);

		Assert.Same(east, room.FindConnection(MapDirection.East));
		Assert.Null(room.FindConnection(MapDirection.West));
	}

	[Fact]
	public void Connection_ExposesItsTarget()
	{
		var connection = new MapConnection { Direction = MapDirection.SouthWest, TargetLayer = 2, TargetRoom = 9 };

		Assert.Equal(new RoomReference(2, 9), connection.Target);
		Assert.Equal("2-9", connection.TargetReference);
		Assert.Contains("2-9", connection.Display);
		Assert.Contains(MapRules.DirectionLabel(MapDirection.SouthWest), connection.Display);
	}

	[Fact]
	public void HiddenFlags_AreOffByDefault()
	{
		Assert.False(new MapRoom().IsHidden);
		Assert.False(new MapConnection().Hidden);
	}

	[Fact]
	public void Puzzle_HasAUsefulDefaultTitle()
	{
		var puzzle = new MapPuzzle();

		Assert.False(string.IsNullOrWhiteSpace(puzzle.Title));
		Assert.Equal(PuzzleKind.Lock, puzzle.Kind);
		Assert.Empty(puzzle.Prompt);
		Assert.Empty(puzzle.Solution);
	}

	[Fact]
	public void Layer_DisplayIncludesItsNumberAndName()
	{
		var layer = new MapLayer { Number = 2, Name = "Rooftops" };

		Assert.Contains("2", layer.Display);
		Assert.Contains("Rooftops", layer.Display);
	}
}
