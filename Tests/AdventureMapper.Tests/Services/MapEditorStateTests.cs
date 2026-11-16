using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using AdventureMapper.Services;

namespace AdventureMapper.Tests.Services;

public class MapEditorStateTests
{
	[Fact]
	public void Constructor_StartsWithAnUnsavedNewMap()
	{
		var state = new MapEditorState();

		Assert.NotNull(state.Document);
		Assert.Single(state.Document.Rooms);
		Assert.Equal(0, state.CurrentLayerNumber);
		Assert.Equal(0, state.CurrentLayer.Number);
		Assert.False(state.IsDirty);
		Assert.False(state.HasFile);
		Assert.Null(state.SelectedRoom);
		Assert.False(state.HasSelection);
		Assert.Null(state.SelectedPuzzle);
		Assert.Equal(L.T("Status_NotSaved"), state.FileLabel);
		Assert.False(string.IsNullOrWhiteSpace(state.Status));
	}

	[Fact]
	public void NewDocument_ReplacesTheMapAndClearsTheDirtyFlag()
	{
		var state = new MapEditorState();
		state.AddRoom(MapDirection.East);
		state.SetFilePath("/tmp/elsewhere.ama");
		Assert.True(state.IsDirty);

		var structureChanged = 0;
		var selectionChanged = 0;
		state.StructureChanged += (_, _) => structureChanged++;
		state.SelectionChanged += (_, _) => selectionChanged++;

		state.NewDocument();

		Assert.Single(state.Document.Rooms);
		Assert.Null(state.SelectedRoom);
		Assert.Null(state.FilePath);
		Assert.False(state.HasFile);
		Assert.False(state.IsDirty);
		Assert.Equal(0, state.CurrentLayerNumber);
		Assert.Equal(L.T("Status_NotSaved"), state.FileLabel);
		Assert.Equal(1, structureChanged);
		Assert.Equal(1, selectionChanged);
	}

	[Fact]
	public void Load_SelectsTheFirstRoomAndReportsTheFileName()
	{
		var state = new MapEditorState();
		var document = AdventureMapDocument.CreateNew("Loaded map");
		document.AddRoom(0, 1, 0, "Hall");

		var structureChanged = 0;
		var selectionChanged = 0;
		state.StructureChanged += (_, _) => structureChanged++;
		state.SelectionChanged += (_, _) => selectionChanged++;

		state.Load(document, "/maps/The Vault.ama");

		Assert.Same(document, state.Document);
		Assert.Same(document.Rooms[0], state.SelectedRoom);
		Assert.True(state.HasSelection);
		Assert.Equal("The Vault.ama", state.FileLabel);
		Assert.True(state.HasFile);
		Assert.False(state.IsDirty);
		Assert.Equal(0, state.CurrentLayerNumber);
		Assert.Contains("The Vault.ama", state.Status);
		Assert.Equal(1, structureChanged);
		Assert.Equal(1, selectionChanged);
	}

	[Fact]
	public void Load_WithoutAPathKeepsTheNotSavedLabel()
	{
		var state = new MapEditorState();

		state.Load(AdventureMapDocument.CreateNew("Memory only"), null);

		Assert.False(state.HasFile);
		Assert.Equal(L.T("Status_NotSaved"), state.FileLabel);
		Assert.Equal(L.T("Status_MapLoaded"), state.Status);
	}

	[Fact]
	public void Load_UsesTheLowestLayerWhenThereIsNoGroundFloor()
	{
		var state = new MapEditorState();
		var document = new AdventureMapDocument();
		document.AddRoom(4, 0, 0, "High");
		document.AddRoom(5, 0, 0, "Higher");

		state.Load(document, null);

		Assert.Equal(4, state.CurrentLayerNumber);
	}

	[Fact]
	public void SetFilePath_RaisesTheDerivedProperties()
	{
		var state = new MapEditorState();
		var names = new List<string>();
		state.PropertyChanged += (_, e) => names.Add(e.PropertyName!);

		state.SetFilePath("/maps/one.ama");

		Assert.Contains(nameof(MapEditorState.FilePath), names);
		Assert.Contains(nameof(MapEditorState.HasFile), names);
		Assert.Contains(nameof(MapEditorState.FileLabel), names);
		Assert.Equal("one.ama", state.FileLabel);
	}

	[Fact]
	public void SelectRoom_RaisesSelectionChangedAndClearsThePuzzle()
	{
		var state = new MapEditorState();
		var entrance = state.Document.Rooms[0];
		var hall = state.Document.AddRoom(0, 1, 0, "Hall");
		state.SelectRoom(entrance);
		var puzzle = state.AddPuzzle();

		var changes = 0;
		state.SelectionChanged += (_, _) => changes++;

		state.SelectRoom(hall);

		Assert.Same(hall, state.SelectedRoom);
		Assert.Null(state.SelectedPuzzle);
		Assert.NotSame(puzzle, state.SelectedPuzzle);
		Assert.Equal(1, changes);

		state.SelectRoom(hall);
		Assert.Equal(1, changes);
	}

	[Fact]
	public void SelectPuzzle_RaisesSelectionChanged()
	{
		var state = new MapEditorState();
		state.SelectRoom(state.Document.Rooms[0]);
		var puzzle = state.AddPuzzle();
		var changes = 0;
		state.SelectionChanged += (_, _) => changes++;

		state.SelectPuzzle(null);

		Assert.Null(state.SelectedPuzzle);
		Assert.Equal(1, changes);
	}

	[Fact]
	public void RoomEdited_NeedsASelectedRoom()
	{
		var state = new MapEditorState();
		var contentChanged = 0;
		state.ContentChanged += (_, _) => contentChanged++;

		state.RoomEdited("ignored");

		Assert.False(state.IsDirty);
		Assert.Equal(0, contentChanged);
	}

	[Fact]
	public void RoomEdited_MarksTheMapDirtyAndRaisesContentChanged()
	{
		var state = new MapEditorState();
		state.SelectRoom(state.Document.Rooms[0]);
		var contentChanged = 0;
		state.ContentChanged += (_, _) => contentChanged++;
		var before = state.Document.ModifiedUtc;

		state.RoomEdited("Edited");

		Assert.True(state.IsDirty);
		Assert.Equal("Edited", state.Status);
		Assert.Equal(1, contentChanged);
		Assert.True(state.Document.ModifiedUtc >= before);
	}

	[Fact]
	public void MarkSaved_ClearsTheDirtyFlag()
	{
		var state = new MapEditorState();
		state.SelectRoom(state.Document.Rooms[0]);
		state.RoomEdited();
		Assert.True(state.IsDirty);

		state.MarkSaved("Saved");

		Assert.False(state.IsDirty);
		Assert.Equal("Saved", state.Status);
	}

	[Fact]
	public void SetTitle_TrimsAndMarksTheMapDirty()
	{
		var state = new MapEditorState();
		var contentChanged = 0;
		state.ContentChanged += (_, _) => contentChanged++;

		state.SetTitle("  The Vault  ");

		Assert.Equal("The Vault", state.Document.Title);
		Assert.True(state.IsDirty);
		Assert.Equal(1, contentChanged);
	}

	[Fact]
	public void SetTitle_RejectsBlankTitles()
	{
		var state = new MapEditorState();
		state.SetTitle("Something");

		state.SetTitle("   ");

		Assert.False(string.IsNullOrWhiteSpace(state.Document.Title));
		Assert.Equal("Untitled adventure", state.Document.Title);
	}

	[Fact]
	public void SetAuthor_StoresTheAuthor()
	{
		var state = new MapEditorState();

		state.SetAuthor("Ada");

		Assert.Equal("Ada", state.Document.Author);
		Assert.True(state.IsDirty);
	}

	[Fact]
	public void RenameCurrentLayer_TrimsOrFallsBackToTheDefaultName()
	{
		var state = new MapEditorState();
		state.CurrentLayerNumber = 2;

		state.RenameCurrentLayer("  Rooftops  ");
		Assert.Equal("Rooftops", state.CurrentLayer.Name);
		Assert.True(state.IsDirty);

		state.RenameCurrentLayer("   ");
		Assert.Equal(MapRules.DefaultLayerName(2), state.CurrentLayer.Name);
	}

	[Fact]
	public void CurrentLayerNumber_CreatesTheLayerAndRaisesStructureChanged()
	{
		var state = new MapEditorState();
		var changes = 0;
		state.StructureChanged += (_, _) => changes++;

		state.CurrentLayerNumber = 3;

		Assert.Equal(3, state.CurrentLayerNumber);
		Assert.Equal(3, state.CurrentLayer.Number);
		Assert.Contains(state.Document.Layers, layer => layer.Number == 3);
		Assert.Equal(1, changes);

		state.CurrentLayerNumber = 3;
		Assert.Equal(1, changes);
	}

	[Fact]
	public void AddLayerRelative_MovesUpAndDownThroughTheFloors()
	{
		var state = new MapEditorState();

		var above = state.AddLayerRelative(1);

		Assert.Equal(1, above.Number);
		Assert.Equal(1, state.CurrentLayerNumber);
		Assert.Contains("1", state.Status);

		var ground = state.AddLayerRelative(-1);

		Assert.Equal(0, ground.Number);
		Assert.Equal(0, state.CurrentLayerNumber);
	}

	[Fact]
	public void Warnings_ReflectTheCurrentDocument()
	{
		var state = new MapEditorState();
		state.Load(new AdventureMapDocument(), null);

		var warnings = state.Warnings;

		Assert.NotEmpty(warnings);
		Assert.All(warnings, warning => Assert.Equal(MapWarningSeverity.Info, warning.Severity));
	}
}
