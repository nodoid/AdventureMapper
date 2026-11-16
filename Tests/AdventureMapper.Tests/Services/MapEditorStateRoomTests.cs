using AdventureMapper.Models;
using AdventureMapper.Services;
using AdventureMapper.Tests.Support;

namespace AdventureMapper.Tests.Services;

public class MapEditorStateRoomTests
{
	private static (MapEditorState State, MapRoom Entrance) NewState()
	{
		var state = new MapEditorState();
		var entrance = state.Document.Rooms[0];
		state.SelectRoom(entrance);
		return (state, entrance);
	}

	[Theory]
	[InlineData(MapDirection.North, 0, -1)]
	[InlineData(MapDirection.NorthEast, 1, -1)]
	[InlineData(MapDirection.East, 1, 0)]
	[InlineData(MapDirection.SouthEast, 1, 1)]
	[InlineData(MapDirection.South, 0, 1)]
	[InlineData(MapDirection.SouthWest, -1, 1)]
	[InlineData(MapDirection.West, -1, 0)]
	[InlineData(MapDirection.NorthWest, -1, -1)]
	public void AddRoom_CompassDirectionPlacesAndConnectsBothWays(MapDirection direction, int column, int row)
	{
		var (state, entrance) = NewState();

		var room = state.AddRoom(direction);

		Assert.Equal(0, room.Layer);
		Assert.Equal(column, room.Column);
		Assert.Equal(row, room.Row);
		Assert.Contains(room, state.Document.Rooms);
		Assert.Same(room, state.SelectedRoom);
		Assert.True(state.IsDirty);
		Assert.Equal(state.CurrentLayerNumber, room.Layer);

		var forward = entrance.FindConnection(direction);
		Assert.NotNull(forward);
		Assert.Equal(room.Id, forward.TargetRoom);
		Assert.Equal(0, forward.TargetLayer);

		var backward = room.FindConnection(MapRules.Opposite(direction));
		Assert.NotNull(backward);
		Assert.Equal(entrance.Id, backward.TargetRoom);
	}

	[Fact]
	public void AddRoom_UpRaisesTheFloorInTheSameCell()
	{
		var (state, entrance) = NewState();

		var room = state.AddRoom(MapDirection.Up);

		Assert.Equal(1, room.Layer);
		Assert.Equal(entrance.Column, room.Column);
		Assert.Equal(entrance.Row, room.Row);
		Assert.Equal(1, state.CurrentLayerNumber);
		Assert.Contains(state.Document.Layers, layer => layer.Number == 1);
		Assert.Equal(room.Id, entrance.FindConnection(MapDirection.Up)!.TargetRoom);
		Assert.Equal(entrance.Id, room.FindConnection(MapDirection.Down)!.TargetRoom);
	}

	[Fact]
	public void AddRoom_DownDropsOneFloor()
	{
		var (state, entrance) = NewState();

		var room = state.AddRoom(MapDirection.Down);

		Assert.Equal(-1, room.Layer);
		Assert.Equal(entrance.Column, room.Column);
		Assert.Equal(entrance.Row, room.Row);
		Assert.Equal(-1, state.CurrentLayerNumber);
		Assert.Equal(room.Id, entrance.FindConnection(MapDirection.Down)!.TargetRoom);
	}

	[Fact]
	public void AddRoom_DownFromABridgeReturnsToTheSpannedLayer()
	{
		var state = new MapEditorState();
		var bridge = state.Document.AddBridge(2, 3, 4, spanLayer: 0);
		state.SelectRoom(bridge);

		var room = state.AddRoom(MapDirection.Down);

		Assert.Equal(0, room.Layer);
		Assert.Equal(3, room.Column);
		Assert.Equal(4, room.Row);
		Assert.Equal(0, state.CurrentLayerNumber);
	}

	[Fact]
	public void AddRoom_DownFromTheGroundReachesTheCellar()
	{
		var (state, _) = NewState();

		var room = state.AddRoom(MapDirection.Down);

		Assert.Equal(-1, room.Layer);
		Assert.Contains(state.Document.Layers, layer => layer.Number == -1);
	}

	[Fact]
	public void AddRoom_WithoutSelectionUsesTheCurrentLayerAndAFreeCell()
	{
		var state = new MapEditorState();
		state.CurrentLayerNumber = 2;
		var structureChanged = 0;
		var selectionChanged = 0;
		MapRoom? announced = null;
		state.StructureChanged += (_, _) => structureChanged++;
		state.SelectionChanged += (_, _) => selectionChanged++;
		state.RoomAdded += (_, room) => announced = room;

		var room = state.AddRoom(MapDirection.North);

		Assert.Equal(2, room.Layer);
		Assert.Single(state.Document.Rooms, other =>
			other.Layer == room.Layer && other.Column == room.Column && other.Row == room.Row);
		Assert.Same(room, announced);
		Assert.Same(room, state.SelectedRoom);
		Assert.True(state.IsDirty);
		Assert.Contains(room.Reference, state.Status);
		Assert.Equal(1, structureChanged);
		Assert.Equal(1, selectionChanged);
		Assert.Empty(room.Connections);
		Assert.Empty(state.Document.Rooms[0].Connections);
	}

	[Fact]
	public void AddRoom_SkipsCellsThatAreAlreadyTaken()
	{
		var (state, entrance) = NewState();
		state.Document.AddRoom(0, 1, 0, "Blocker");

		var room = state.AddRoom(MapDirection.East);

		Assert.Equal(2, room.Column);
		Assert.Equal(0, room.Row);
		Assert.Equal(room.Id, entrance.FindConnection(MapDirection.East)!.TargetRoom);
	}

	[Fact]
	public void AddRoom_KeepsWorkingFromARoomOnAnotherLayer()
	{
		var (state, _) = NewState();
		var upstairs = state.Document.AddRoom(2, 0, 0, "Upstairs");
		state.SelectRoom(upstairs);

		var room = state.AddRoom(MapDirection.South);

		Assert.Equal(2, room.Layer);
		Assert.Equal(1, room.Row);
		Assert.Equal(2, state.CurrentLayerNumber);
	}

	[Fact]
	public void DeleteSelectedRoom_RemovesTheRoomAndItsLinks()
	{
		var (state, entrance) = NewState();
		var hall = state.AddRoom(MapDirection.East);
		var structureChanged = 0;
		var selectionChanged = 0;
		state.StructureChanged += (_, _) => structureChanged++;
		state.SelectionChanged += (_, _) => selectionChanged++;

		state.DeleteSelectedRoom();

		Assert.DoesNotContain(hall, state.Document.Rooms);
		Assert.Empty(entrance.Connections);
		Assert.Null(state.SelectedRoom);
		Assert.True(state.IsDirty);
		Assert.False(string.IsNullOrWhiteSpace(state.Status));
		Assert.Equal(1, structureChanged);
		Assert.Equal(1, selectionChanged);
	}

	[Fact]
	public void DeleteSelectedRoom_WithoutSelectionDoesNothing()
	{
		var state = new MapEditorState();
		var rooms = state.Document.Rooms.Count;

		state.DeleteSelectedRoom();

		Assert.Equal(rooms, state.Document.Rooms.Count);
		Assert.False(state.IsDirty);
	}

	[Fact]
	public void DeleteSelectedRoom_FallsBackToARemainingLayer()
	{
		var state = new MapEditorState();
		var attic = state.Document.AddRoom(1, 0, 0, "Attic");
		state.SelectRoom(attic);
		state.CurrentLayerNumber = 1;

		state.DeleteSelectedRoom();

		Assert.DoesNotContain(state.Document.Layers, layer => layer.Number == 1);
		Assert.Equal(0, state.CurrentLayerNumber);
	}

	[Fact]
	public void AddDetachedRoom_CreatesAHiddenRoomWithNoLinks()
	{
		var state = new MapEditorState();
		var structureChanged = 0;
		var selectionChanged = 0;
		MapRoom? announced = null;
		state.StructureChanged += (_, _) => structureChanged++;
		state.SelectionChanged += (_, _) => selectionChanged++;
		state.RoomAdded += (_, room) => announced = room;

		var room = state.AddDetachedRoom();

		Assert.True(room.IsHidden);
		Assert.Empty(room.Connections);
		Assert.Equal(0, room.Layer);
		Assert.Equal(1, room.Row);
		Assert.Single(state.Document.Rooms, other =>
			other.Layer == room.Layer && other.Column == room.Column && other.Row == room.Row);
		Assert.Same(room, state.SelectedRoom);
		Assert.Same(room, announced);
		Assert.True(state.IsDirty);
		Assert.Contains(room.Reference, state.Status);
		Assert.Equal(1, structureChanged);
		Assert.Equal(1, selectionChanged);
		Assert.DoesNotContain(state.Document.Rooms.SelectMany(other => other.Connections), connection =>
			connection.TargetLayer == room.Layer && connection.TargetRoom == room.Id);
	}

	[Fact]
	public void AddDetachedRoom_StacksBelowExistingRooms()
	{
		var state = new MapEditorState();

		var first = state.AddDetachedRoom();
		var second = state.AddDetachedRoom();

		Assert.Equal(1, first.Row);
		Assert.Equal(2, second.Row);
		Assert.Equal(0, second.Column);
		Assert.NotEqual(first.Id, second.Id);
	}

	[Fact]
	public void AddDetachedRoom_UsesTheCurrentFloor()
	{
		var state = new MapEditorState();
		state.CurrentLayerNumber = 2;

		var room = state.AddDetachedRoom();

		Assert.Equal(2, room.Layer);
		Assert.Equal(2, state.CurrentLayerNumber);
		Assert.Contains(state.Document.Layers, layer => layer.Number == 2);
	}

	[Fact]
	public void MoveRoom_OffsetsTheRoomAndKeepsItsLinks()
	{
		var (state, entrance) = NewState();
		var hall = state.AddRoom(MapDirection.East);
		state.MarkSaved();
		var redraws = 0;
		state.ContentChanged += (_, _) => redraws++;

		state.MoveRoom(hall, 24, -12);
		state.MoveRoom(hall, 6, 2);

		Assert.Equal(30, hall.OffsetX, 3);
		Assert.Equal(-10, hall.OffsetY, 3);
		Assert.True(state.IsDirty);
		Assert.Equal(2, redraws);
		Assert.NotNull(entrance.FindConnection(MapDirection.East));
		Assert.Equal(1, hall.Column);
	}

	[Fact]
	public void ResetLayout_SnapsEveryRoomBackToItsCell()
	{
		var (state, entrance) = NewState();
		var hall = state.AddRoom(MapDirection.East);
		state.MoveRoom(hall, 40, 25);
		state.MoveRoom(entrance, -12, 8);
		state.MarkSaved();
		var structureChanged = 0;
		state.StructureChanged += (_, _) => structureChanged++;

		state.ResetLayout();

		Assert.Equal(0, entrance.OffsetX);
		Assert.Equal(0, entrance.OffsetY);
		Assert.Equal(0, hall.OffsetX);
		Assert.Equal(0, hall.OffsetY);
		Assert.True(state.IsDirty);
		Assert.Equal(1, structureChanged);
		Assert.Contains("reset", state.Status, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void ResetLayout_WithoutMovedRoomsChangesNothing()
	{
		var (state, _) = NewState();
		state.MarkSaved();

		state.ResetLayout();

		Assert.False(state.IsDirty);
	}

	[Fact]
	public void MoveRoom_IgnoresZeroDeltasAndUnknownRooms()
	{
		var (state, entrance) = NewState();
		state.MarkSaved();

		state.MoveRoom(entrance, 0, 0);
		state.MoveRoom(new MapRoom { Id = 99 }, 10, 10);

		Assert.False(state.IsDirty);
		Assert.Equal(0, entrance.OffsetX);
	}

	[Fact]
	public void Undo_RestoresThePositionsBeforeAMove()
	{
		var (state, entrance) = NewState();
		var hall = state.AddRoom(MapDirection.East);
		state.MarkSaved();
		state.MoveRoom(hall, 30, -20);
		Assert.True(state.CanUndo);

		Assert.True(state.Undo());

		Assert.Equal(0, hall.OffsetX);
		Assert.Equal(0, hall.OffsetY);
		Assert.False(state.IsDirty);
		Assert.True(state.CanRedo);
		Assert.Equal(1, hall.Column);
		Assert.NotNull(entrance.FindConnection(MapDirection.East));
		Assert.False(string.IsNullOrWhiteSpace(state.Status));
	}

	[Fact]
	public void Redo_ReappliesTheMove()
	{
		var (state, _) = NewState();
		var entrance = state.Document.Rooms[0];
		state.MoveRoom(entrance, 30, -20);
		state.Undo();

		Assert.True(state.Redo());

		Assert.Equal(30, entrance.OffsetX, 3);
		Assert.Equal(-20, entrance.OffsetY, 3);
		Assert.False(state.CanRedo);
		Assert.True(state.IsDirty);
	}

	[Fact]
	public void Undo_RestoresAResetLayout()
	{
		var (state, entrance) = NewState();
		var hall = state.AddRoom(MapDirection.East);
		state.MoveRoom(hall, 40, 25);
		state.ResetLayout();
		Assert.Equal(0, hall.OffsetX);

		state.Undo();

		Assert.Equal(40, hall.OffsetX, 3);
		Assert.Equal(25, hall.OffsetY, 3);
		Assert.Equal(0, entrance.OffsetX);
	}

	[Fact]
	public void Undo_AndRedo_DoNothingWithoutHistory()
	{
		var state = new MapEditorState();
		var redraws = 0;
		state.StructureChanged += (_, _) => redraws++;

		Assert.False(state.Undo());
		Assert.False(state.Redo());
		Assert.Equal(0, redraws);
	}

	[Fact]
	public void Undo_ClearsWhenANewMapIsLoaded()
	{
		var (state, entrance) = NewState();
		state.MoveRoom(entrance, 10, 10);
		Assert.True(state.CanUndo);

		state.NewDocument();

		Assert.False(state.CanUndo);
		Assert.False(state.CanRedo);
	}

	[Fact]
	public void SnapRoomToGrid_RoundsToTheNearestCellWhenOn()
	{
		var (state, _) = NewState();
		var entrance = state.Document.Rooms[0];
		state.SnapToGrid = true;
		state.MoveRoom(entrance, 150, 100);

		state.SnapRoomToGrid(entrance);

		Assert.Equal(MapConstants.GridCellWidth, entrance.OffsetX, 3);
		Assert.Equal(MapConstants.GridCellHeight, entrance.OffsetY, 3);
		Assert.True(state.CanUndo);
	}

	[Fact]
	public void SnapRoomToGrid_LeavesTheRoomAloneWhenOff()
	{
		var (state, _) = NewState();
		var entrance = state.Document.Rooms[0];
		state.MoveRoom(entrance, 150, 100);

		state.SnapRoomToGrid(entrance);

		Assert.Equal(150, entrance.OffsetX, 3);
		Assert.Equal(100, entrance.OffsetY, 3);
	}

	[Fact]
	public void SetRoomHidden_MarksTheRoomAndTheMapDirty()
	{
		var (state, entrance) = NewState();
		state.MarkSaved();

		state.SetRoomHidden(true);

		Assert.True(entrance.IsHidden);
		Assert.True(state.IsDirty);
		Assert.False(string.IsNullOrWhiteSpace(state.Status));

		state.SetRoomHidden(false);
		Assert.False(entrance.IsHidden);
	}

	[Fact]
	public void SetRoomHidden_WithoutSelectionDoesNothing()
	{
		var state = new MapEditorState();

		state.SetRoomHidden(true);

		Assert.False(state.IsDirty);
		Assert.All(state.Document.Rooms, room => Assert.False(room.IsHidden));
	}

	[Fact]
	public void AddConnection_LinksTheSelectedRoom()
	{
		var (state, entrance) = NewState();
		var hall = state.Document.AddRoom(0, 4, 4, "Hall");

		var added = state.AddConnection(MapDirection.East, new RoomReference(0, hall.Id));

		Assert.True(added);
		Assert.True(state.IsDirty);
		Assert.Equal(hall.Id, entrance.FindConnection(MapDirection.East)!.TargetRoom);
		Assert.Contains(hall.Reference, state.Status);
	}

	[Fact]
	public void AddConnection_ToAMissingRoomReportsAndChangesNothing()
	{
		var (state, entrance) = NewState();

		var added = state.AddConnection(MapDirection.North, new RoomReference(3, 99));

		Assert.False(added);
		Assert.False(state.IsDirty);
		Assert.Empty(entrance.Connections);
		Assert.Contains("3-99", state.Status);
	}

	[Fact]
	public void AddConnection_WithoutSelectionDoesNothing()
	{
		var state = new MapEditorState();

		Assert.False(state.AddConnection(MapDirection.North, new RoomReference(0, 1)));
	}

	[Fact]
	public void ConnectRooms_LinksBothWays()
	{
		var (state, entrance) = NewState();
		var hall = state.Document.AddRoom(0, 3, 0, "Hall");
		state.MarkSaved();

		var linked = state.ConnectRooms(entrance, MapDirection.East, hall);

		Assert.True(linked);
		Assert.Equal(hall.Id, entrance.FindConnection(MapDirection.East)!.TargetRoom);
		Assert.Equal(entrance.Id, hall.FindConnection(MapDirection.West)!.TargetRoom);
		Assert.True(state.IsDirty);
		Assert.Contains(entrance.Reference, state.Status);
	}

	[Fact]
	public void ConnectRooms_CanMakeAOneWayRoute()
	{
		var (state, entrance) = NewState();
		var hall = state.Document.AddRoom(0, 3, 0, "Hall");

		state.ConnectRooms(entrance, MapDirection.East, hall, bidirectional: false);

		Assert.NotNull(entrance.FindConnection(MapDirection.East));
		Assert.Null(hall.FindConnection(MapDirection.West));
	}

	[Fact]
	public void ConnectRooms_RejectsSelfLinksAndUnknownRooms()
	{
		var (state, entrance) = NewState();
		var stranger = new MapRoom { Id = 99, Layer = 0, Name = "Stranger" };

		Assert.False(state.ConnectRooms(entrance, MapDirection.East, entrance));
		Assert.False(state.ConnectRooms(entrance, MapDirection.East, stranger));
		Assert.False(state.ConnectRooms(entrance, MapDirection.Up, state.Document.Rooms[0]));
		Assert.Empty(entrance.Connections);
	}

	[Fact]
	public void ToggleLink_RemovesTheReturnLegOfATwoWayLink()
	{
		var (state, entrance) = NewState();
		var hall = state.Document.AddRoom(0, 3, 0, "Hall");
		state.ConnectRooms(entrance, MapDirection.East, hall);

		var toggled = state.ToggleLink(entrance, hall);

		Assert.True(toggled);
		Assert.NotNull(entrance.FindConnection(MapDirection.East));
		Assert.Null(hall.FindConnection(MapDirection.West));
		Assert.True(state.IsDirty);
		Assert.Contains("→", state.Status);
	}

	[Fact]
	public void ToggleLink_RestoresTheReturnLegOfAOneWayLink()
	{
		var (state, entrance) = NewState();
		var hall = state.Document.AddRoom(0, 3, 0, "Hall");
		state.ConnectRooms(entrance, MapDirection.East, hall, bidirectional: false);

		state.ToggleLink(entrance, hall);

		Assert.NotNull(hall.FindConnection(MapDirection.West));
		Assert.Contains("↔", state.Status);
	}

	[Fact]
	public void ToggleLink_AddsTheForwardLegWhenOnlyTheReturnExists()
	{
		var (state, entrance) = NewState();
		var hall = state.Document.AddRoom(0, 3, 0, "Hall");
		state.Document.Connect(hall, MapDirection.West, entrance);

		state.ToggleLink(entrance, hall);

		Assert.NotNull(entrance.FindConnection(MapDirection.East));
	}

	[Fact]
	public void ToggleLink_WithoutALinkDoesNothing()
	{
		var (state, entrance) = NewState();
		var hall = state.Document.AddRoom(0, 3, 0, "Hall");
		state.MarkSaved();

		Assert.False(state.ToggleLink(entrance, hall));
		Assert.False(state.IsDirty);
		Assert.False(state.ToggleLink(entrance, entrance));
	}

	[Fact]
	public void RemoveConnection_UnlinksTheConnection()
	{
		var (state, entrance) = NewState();
		state.AddConnection(MapDirection.North, new RoomReference(0, entrance.Id));
		var connection = entrance.FindConnection(MapDirection.North)!;
		state.MarkSaved();
		Assert.False(state.IsDirty);

		state.RemoveConnection(connection);

		Assert.Empty(entrance.Connections);
		Assert.True(state.IsDirty);
	}

	[Fact]
	public void AddPuzzle_RequiresASelectedRoom()
	{
		var state = new MapEditorState();

		Assert.Throws<InvalidOperationException>(() => state.AddPuzzle());
	}

	[Fact]
	public void AddPuzzle_NumbersPuzzlesPerRoom()
	{
		var (state, entrance) = NewState();

		var first = state.AddPuzzle();
		var second = state.AddPuzzle(new MapPuzzle { Kind = PuzzleKind.Trap });

		Assert.Equal(1, first.Id);
		Assert.Equal(2, second.Id);
		Assert.Same(second, state.SelectedPuzzle);
		Assert.Equal(new[] { first, second }, entrance.Puzzles);
		Assert.True(state.IsDirty);
		Assert.False(string.IsNullOrWhiteSpace(first.Title));
		Assert.Equal(PuzzleKind.Trap, second.Kind);
	}

	[Fact]
	public void AddPuzzle_KeepsTheFieldsTypedInTheDialog()
	{
		var (state, entrance) = NewState();
		var puzzle = new MapPuzzle
		{
			Title = "The iron door",
			Kind = PuzzleKind.Lock,
			Prompt = "Turn the handle",
			Solution = "Push",
			Reward = "A key",
		};

		state.AddPuzzle(puzzle);

		var stored = Assert.Single(entrance.Puzzles);
		Assert.Equal("The iron door", stored.Title);
		Assert.Equal(PuzzleKind.Lock, stored.Kind);
		Assert.Equal("Turn the handle", stored.Prompt);
		Assert.Equal("Push", stored.Solution);
		Assert.Equal("A key", stored.Reward);
	}

	[Fact]
	public void UpdatePuzzle_CopiesTheEditedValues()
	{
		var (state, entrance) = NewState();
		var stored = state.AddPuzzle();
		state.SelectPuzzle(null);

		state.UpdatePuzzle(stored, new MapPuzzle
		{
			Title = "  New title  ",
			Kind = PuzzleKind.Riddle,
			Prompt = "Ask",
			Solution = "Answer",
			Reward = "Gold",
		});

		Assert.Equal("New title", stored.Title);
		Assert.Equal(PuzzleKind.Riddle, stored.Kind);
		Assert.Equal("Ask", stored.Prompt);
		Assert.Equal("Answer", stored.Solution);
		Assert.Equal("Gold", stored.Reward);
		Assert.Same(stored, state.SelectedPuzzle);
		Assert.True(state.IsDirty);
		Assert.Single(entrance.Puzzles);
	}

	[Fact]
	public void UpdatePuzzle_KeepsTheOldTitleWhenCleared()
	{
		var (state, _) = NewState();
		var stored = state.AddPuzzle(new MapPuzzle { Title = "Keep me" });

		state.UpdatePuzzle(stored, new MapPuzzle { Title = "   " });

		Assert.Equal("Keep me", stored.Title);
	}

	[Fact]
	public void RemovePuzzle_SelectsTheNextRemainingPuzzle()
	{
		var (state, entrance) = NewState();
		var first = state.AddPuzzle();
		var second = state.AddPuzzle();
		state.SelectPuzzle(second);

		state.RemovePuzzle(second);

		Assert.Single(entrance.Puzzles);
		Assert.Same(first, state.SelectedPuzzle);
		Assert.True(state.IsDirty);
	}

	[Fact]
	public void RemovePuzzle_WithoutSelectionDoesNothing()
	{
		var state = new MapEditorState();

		state.RemovePuzzle(new MapPuzzle());

		Assert.False(state.IsDirty);
	}

	[Fact]
	public void NotifyPuzzleEdited_MarksTheMapDirty()
	{
		var (state, _) = NewState();
		state.MarkSaved();
		Assert.False(state.IsDirty);

		state.NotifyPuzzleEdited();

		Assert.True(state.IsDirty);
		Assert.False(string.IsNullOrWhiteSpace(state.Status));
	}

	[Fact]
	public void AddRoom_ThenDelete_LeavesTheDocumentConsistent()
	{
		var (state, entrance) = NewState();

		var compass = state.AddRoom(MapDirection.NorthEast);
		state.SelectRoom(entrance);
		var below = state.AddRoom(MapDirection.Down);

		state.SelectRoom(below);
		state.DeleteSelectedRoom();

		Assert.DoesNotContain(below, state.Document.Rooms);
		Assert.Null(entrance.FindConnection(MapDirection.Down));
		Assert.NotNull(entrance.FindConnection(MapDirection.NorthEast));
		Assert.Contains(compass, state.Document.Rooms);
	}
}
