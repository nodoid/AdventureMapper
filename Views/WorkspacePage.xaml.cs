using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using AdventureMapper.Services;

namespace AdventureMapper.Views;

/// <summary>Desktop (macOS/Windows) layout: menu bar, canvas, inspector and status bar.</summary>
public partial class WorkspacePage : ContentPage
{
	private readonly MapEditorState _state;
	private readonly EditorActions _actions;
	private readonly AutoSaveService _autoSave;

	public WorkspacePage(
		MapEditorState state,
		EditorActions actions,
		AutoSaveService autoSave,
		MapToolbarView toolbar,
		MapCanvasView canvas,
		InspectorView inspector)
	{
		InitializeComponent();
		_state = state;
		_actions = actions;
		_autoSave = autoSave;
#if MACCATALYST
		MacEditMenu.Attach(state);
#endif

		ToolbarHost.Content = toolbar;
		CanvasHost.Content = canvas;
		InspectorHost.Content = inspector;

		state.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName is nameof(MapEditorState.Status)
				or nameof(MapEditorState.IsDirty)
				or nameof(MapEditorState.FilePath)
				or nameof(MapEditorState.FileLabel))
			{
				UpdateStatusBar();
			}

			if (e.PropertyName is nameof(MapEditorState.CanUndo)
				or nameof(MapEditorState.CanRedo)
				or nameof(MapEditorState.SnapToGrid))
			{
				UpdateEditMenu();
			}
		};
		autoSave.AutoSaved += (_, _) => UpdateStatusBar();

		UpdateStatusBar();
		UpdateEditMenu();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		PendingFileRouter.FileReady += OnFileReady;
		_ = ConsumePendingFileAsync();
	}

	protected override void OnDisappearing()
	{
		PendingFileRouter.FileReady -= OnFileReady;
		base.OnDisappearing();
	}

	private void OnFileReady(object? sender, string path) =>
		MainThread.BeginInvokeOnMainThread(() => _ = _actions.OpenPathAsync(path));

	private async Task ConsumePendingFileAsync()
	{
		var path = PendingFileRouter.Dequeue();
		if (path is not null)
			await _actions.OpenPathAsync(path);
	}

	private void UpdateStatusBar()
	{
		StatusLabel.Text = _state.Status;
		var autoSaved = _autoSave.LastSavedUtc is DateTime last
			? L.F("Status_AutoSavedSuffix", last.ToLocalTime().ToString("HH:mm:ss"))
			: string.Empty;
		SaveStatusLabel.Text = $"{_state.FileLabel}{(_state.IsDirty ? L.T("Status_UnsavedSuffix") : string.Empty)}{autoSaved}";
	}

	private void UpdateEditMenu()
	{
		UndoMenuItem.IsEnabled = _state.CanUndo;
		RedoMenuItem.IsEnabled = _state.CanRedo;
		SnapMenuItem.Text = L.T(_state.SnapToGrid ? "Menu_SnapToGridOn" : "Menu_SnapToGridOff");
	}

	private void OnUndoMenuClicked(object? sender, EventArgs e) => _state.Undo();

	private void OnRedoMenuClicked(object? sender, EventArgs e) => _state.Redo();

	private void OnSnapToGridMenuClicked(object? sender, EventArgs e) => _state.SnapToGrid = !_state.SnapToGrid;

	private async void OnNewMenuClicked(object? sender, EventArgs e) => await _actions.NewMapAsync();

	private async void OnOpenMenuClicked(object? sender, EventArgs e) => await _actions.OpenAsync();

	private async void OnSaveMenuClicked(object? sender, EventArgs e) => await _actions.SaveAsync(saveAs: false);

	private async void OnSaveAsMenuClicked(object? sender, EventArgs e) => await _actions.SaveAsync(saveAs: true);

	private async void OnPrintMenuClicked(object? sender, EventArgs e) => await _actions.PrintAsync();

	private async void OnExportJpegMenuClicked(object? sender, EventArgs e) => await _actions.ExportAsync(MapExportFormat.Jpeg);

	private async void OnExportPdfMenuClicked(object? sender, EventArgs e) => await _actions.ExportAsync(MapExportFormat.Pdf);

	private async void OnExportJsonMenuClicked(object? sender, EventArgs e) => await _actions.ExportAsync(MapExportFormat.Json);

	private void OnAddPlainRoomMenuClicked(object? sender, EventArgs e) => _state.AddDetachedRoom();

	private void OnAddNorthMenuClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.North);

	private void OnAddNorthEastMenuClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.NorthEast);

	private void OnAddEastMenuClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.East);

	private void OnAddSouthEastMenuClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.SouthEast);

	private void OnAddSouthMenuClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.South);

	private void OnAddSouthWestMenuClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.SouthWest);

	private void OnAddWestMenuClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.West);

	private void OnAddNorthWestMenuClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.NorthWest);

	private void OnAddUpMenuClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.Up);

	private void OnAddDownMenuClicked(object? sender, EventArgs e) => _state.AddRoom(MapDirection.Down);

	private void OnResetLayoutMenuClicked(object? sender, EventArgs e) => _state.ResetLayout();

	private async void OnDeleteRoomMenuClicked(object? sender, EventArgs e) => await _actions.DeleteSelectedRoomAsync();

	private void OnFloorAboveMenuClicked(object? sender, EventArgs e) => _state.AddLayerRelative(1);

	private void OnFloorBelowMenuClicked(object? sender, EventArgs e) => _state.AddLayerRelative(-1);

	private async void OnAboutMenuClicked(object? sender, EventArgs e) => await _actions.ShowAboutAsync();

	private async void OnMapFormatMenuClicked(object? sender, EventArgs e)
	{
		await EditorActions.AlertAsync(
			L.T("MapFormat_Title"),
			L.T("MapFormat_Body"));
	}
}
