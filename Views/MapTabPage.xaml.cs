using AdventureMapper.Resources.Strings;
using AdventureMapper.Services;

namespace AdventureMapper.Views;

/// <summary>Tablet tab: toolbar, map canvas and status bar.</summary>
public partial class MapTabPage : ContentPage
{
	private readonly MapEditorState _state;
	private readonly EditorActions _actions;
	private readonly AutoSaveService _autoSave;

	public MapTabPage(
		MapEditorState state,
		EditorActions actions,
		AutoSaveService autoSave,
		MapToolbarView toolbar,
		MapCanvasView canvas)
	{
		InitializeComponent();
		_state = state;
		_actions = actions;
		_autoSave = autoSave;

		ToolbarHost.Content = toolbar;
		CanvasHost.Content = canvas;

		state.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName is nameof(MapEditorState.Status)
				or nameof(MapEditorState.IsDirty)
				or nameof(MapEditorState.FilePath)
				or nameof(MapEditorState.FileLabel))
			{
				UpdateStatusBar();
			}
		};
		autoSave.AutoSaved += (_, _) => UpdateStatusBar();

		UpdateStatusBar();
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
}
