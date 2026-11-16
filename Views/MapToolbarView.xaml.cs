using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using AdventureMapper.Services;

namespace AdventureMapper.Views;

public partial class MapToolbarView : ContentView
{
	private readonly MapEditorState _state;
	private readonly EditorActions _actions;

	public MapToolbarView(MapEditorState state, EditorActions actions)
	{
		InitializeComponent();
		_state = state;
		_actions = actions;

		_state.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName is nameof(MapEditorState.CurrentLayerNumber)
				or nameof(MapEditorState.Status)
				or nameof(MapEditorState.Document))
			{
				UpdateFloorLabel();
			}

			if (e.PropertyName is nameof(MapEditorState.SnapToGrid))
				UpdateSnapLabel();

			if (e.PropertyName is nameof(MapEditorState.CanUndo) or nameof(MapEditorState.CanRedo))
				UpdateHistoryButtons();
		};
		_state.StructureChanged += (_, _) => UpdateFloorLabel();

		UpdateFloorLabel();
		UpdateSnapLabel();
		UpdateHistoryButtons();
	}

	private void UpdateSnapLabel() =>
		SnapButton.Text = L.T(_state.SnapToGrid ? "Toolbar_SnapOn" : "Toolbar_SnapOff");

	private void OnSnapClicked(object? sender, EventArgs e) => _state.SnapToGrid = !_state.SnapToGrid;

	private void UpdateHistoryButtons()
	{
		UndoButton.IsEnabled = _state.CanUndo;
		RedoButton.IsEnabled = _state.CanRedo;
	}

	private void OnUndoClicked(object? sender, EventArgs e) => _state.Undo();

	private void OnRedoClicked(object? sender, EventArgs e) => _state.Redo();

	private void UpdateFloorLabel()
	{
		var layer = _state.CurrentLayer;
		var count = _state.Document.RoomsOn(layer.Number).Count();
		FloorLabel.Text = count == 1
			? L.F("Toolbar_FloorSummaryOne", layer.Number, layer.Name)
			: L.F("Toolbar_FloorSummary", layer.Number, layer.Name, count);
	}

	private async void OnNewClicked(object? sender, EventArgs e) => await _actions.NewMapAsync();

	private async void OnOpenClicked(object? sender, EventArgs e) => await _actions.OpenAsync();

	private async void OnSaveClicked(object? sender, EventArgs e) => await _actions.SaveAsync(saveAs: false);

	private async void OnExportClicked(object? sender, EventArgs e) => await _actions.ExportWithPromptAsync();

	private async void OnPrintClicked(object? sender, EventArgs e) => await _actions.PrintAsync();

	private void OnAddPlainRoomClicked(object? sender, EventArgs e) => _state.AddDetachedRoom();

	private void OnAddNorthClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.North);

	private void OnAddNorthEastClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.NorthEast);

	private void OnAddEastClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.East);

	private void OnAddSouthEastClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.SouthEast);

	private void OnAddSouthClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.South);

	private void OnAddSouthWestClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.SouthWest);

	private void OnAddWestClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.West);

	private void OnAddNorthWestClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.NorthWest);

	private void OnAddUpClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.Up);

	private void OnAddDownClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.Down);

	private void OnPreviousFloorClicked(object? sender, EventArgs e) => ChangeFloor(-1);

	private void OnNextFloorClicked(object? sender, EventArgs e) => ChangeFloor(1);

	private void ChangeFloor(int delta)
	{
		var numbers = _state.Document.Layers.Select(l => l.Number).OrderBy(n => n).ToList();
		var index = numbers.IndexOf(_state.CurrentLayerNumber);
		var next = index + delta;
		if (next >= 0 && next < numbers.Count)
			_state.CurrentLayerNumber = numbers[next];
	}
}
