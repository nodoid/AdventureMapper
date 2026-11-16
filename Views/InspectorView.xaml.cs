using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using AdventureMapper.Services;
using AdventureMapper.Views.Popups;
using Mopups.Services;

namespace AdventureMapper.Views;

public partial class InspectorView : ContentView
{
	private static string AutoSaveLabel(int seconds) => seconds switch
	{
		0 => L.T("AutoSave_Off"),
		30 => L.T("AutoSave_30"),
		60 => L.T("AutoSave_60"),
		120 => L.T("AutoSave_120"),
		300 => L.T("AutoSave_300"),
		_ => L.T("AutoSave_600"),
	};

	private static readonly MapDirection[] DirectionOptions =
	{
		MapDirection.North,
		MapDirection.NorthEast,
		MapDirection.East,
		MapDirection.SouthEast,
		MapDirection.South,
		MapDirection.SouthWest,
		MapDirection.West,
		MapDirection.NorthWest,
		MapDirection.Up,
		MapDirection.Down,
	};

	private readonly MapEditorState _state;
	private readonly AutoSaveService _autoSave;
	private List<MapLayer> _layers = new();
	private bool _suppress;

	public InspectorView(MapEditorState state, AutoSaveService autoSave)
	{
		InitializeComponent();
		_state = state;
		_autoSave = autoSave;

		RoomKindPicker.ItemsSource = Enum.GetValues<RoomKind>().Select(kind => L.T($"RoomKind_{kind}")).ToList();
		ConnectionDirectionPicker.ItemsSource = DirectionOptions.Select(MapRules.DirectionLabel).ToList();
		ConnectionDirectionPicker.SelectedIndex = 0;
		AutoSavePicker.ItemsSource = AutoSaveService.IntervalOptions.Select(AutoSaveLabel).ToList();

		state.StructureChanged += (_, _) => Refresh();
		state.SelectionChanged += (_, _) => Refresh();
		state.ContentChanged += (_, _) => RefreshLive();
		state.PropertyChanged += OnStatePropertyChanged;
		autoSave.AutoSaved += (_, _) => UpdateAutoSaveStatus();

		Refresh();
	}

	private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(MapEditorState.CurrentLayer))
		{
			_suppress = true;
			try
			{
				RefreshLayerPicker();
				UpdateLayerSummary();
			}
			finally
			{
				_suppress = false;
			}
		}
	}

	private void Refresh()
	{
		_suppress = true;
		try
		{
			MapTitleEntry.Text = _state.Document.Title;
			MapAuthorEntry.Text = _state.Document.Author;
			RefreshLayerPicker();
			LayerNameEntry.Text = _state.CurrentLayer.Name;
			UpdateLayerSummary();
			RefreshRoomSection();
			RefreshWarnings();
			UpdateAutoSaveStatus();
		}
		finally
		{
			_suppress = false;
		}
	}

	/// <summary>Cheap refresh for edits that must not reset the focused text box.</summary>
	private void RefreshLive()
	{
		if (_state.SelectedRoom is not MapRoom room)
			return;

		RoomHeadingLabel.Text = DescribeRoom(room);
		DescriptionPreviewLabel.Text = room.Description.Length == 0 ? L.T("Inspector_NoDescription") : room.Description;
		RebuildConnectionRows(room);
		RebuildPuzzleRows(room);
		RefreshWarnings();
		UpdateLayerSummary();
	}

	private void RefreshLayerPicker()
	{
		_layers = _state.Document.Layers.OrderByDescending(l => l.Number).ToList();
		var selected = _layers.FindIndex(l => l.Number == _state.CurrentLayerNumber);
		LayerPicker.ItemsSource = _layers;
		LayerPicker.SelectedIndex = selected >= 0 ? selected : 0;
	}

	private void UpdateLayerSummary()
	{
		var layer = _state.CurrentLayer;
		var rooms = _state.Document.RoomsOn(layer.Number).Count();
		LayerSummaryLabel.Text = rooms == 1
			? L.T("Inspector_RoomsOne")
			: L.F("Inspector_RoomsMany", rooms);
	}

	private void RefreshRoomSection()
	{
		var room = _state.SelectedRoom;
		NoRoomLabel.IsVisible = room is null;
		RoomSection.IsVisible = room is not null;
		if (room is null)
			return;

		RoomHeadingLabel.Text = DescribeRoom(room);
		RoomNameEntry.Text = room.Name;
		DescriptionPreviewLabel.Text = room.Description.Length == 0 ? L.T("Inspector_NoDescription") : room.Description;
		RoomKindPicker.SelectedIndex = (int)room.Kind;
		var wasSuppressed = _suppress;
		_suppress = true;
		HiddenSwitch.IsToggled = room.IsHidden;
		_suppress = wasSuppressed;
		BridgeSection.IsVisible = room.Kind == RoomKind.Bridge;
		BridgeLayerEntry.Text = room.BridgeSpanLayer.ToString();
		RebuildBridgeRooms(room);
		RebuildConnectionRows(room);
		RebuildPuzzleRows(room);
	}

	private static string DescribeRoom(MapRoom room) =>
		L.F("Inspector_RoomHeading", room.Reference, room.Layer, L.T($"RoomKind_{room.Kind}"));

	private void RebuildConnectionRows(MapRoom room)
	{
		ConnectionsContainer.Children.Clear();

		foreach (var connection in room.Connections.ToList())
		{
			var row = new Grid
			{
				ColumnDefinitions = new ColumnDefinitionCollection(
					new ColumnDefinition(GridLength.Star),
					new ColumnDefinition(GridLength.Auto)),
				ColumnSpacing = 6,
			};

			row.Add(new Label
			{
				Text = $"{MapRules.DirectionLabel(connection.Direction)} → {connection.TargetReference}",
				FontSize = 13,
				TextColor = Color.FromArgb("#1C1F27"),
				VerticalOptions = LayoutOptions.Center,
			}, 0, 0);

			var remove = new Button
			{
				Text = "✕",
				FontSize = 12,
				Padding = new Thickness(8, 2),
				BackgroundColor = Colors.Transparent,
				TextColor = Color.FromArgb("#FF9A7A"),
				CommandParameter = connection,
			};
			remove.Clicked += OnRemoveConnectionClicked;
			row.Add(remove, 1, 0);

			ConnectionsContainer.Add(row);
		}

		if (room.Connections.Count == 0)
		{
			ConnectionsContainer.Add(new Label
			{
				Text = L.T("Inspector_NoConnections"),
				Style = (Style)Application.Current!.Resources["Caption"],
			});
		}
	}

	private void RebuildPuzzleRows(MapRoom room)
	{
		PuzzlesContainer.Children.Clear();

		foreach (var puzzle in room.Puzzles.ToList())
		{
			var row = new Grid
			{
				ColumnDefinitions = new ColumnDefinitionCollection(
					new ColumnDefinition(GridLength.Star),
					new ColumnDefinition(GridLength.Auto),
					new ColumnDefinition(GridLength.Auto)),
				ColumnSpacing = 6,
			};

			row.Add(new Label
			{
				Text = $"{puzzle.Title} · {L.T($"PuzzleKind_{puzzle.Kind}")}",
				FontSize = 13,
				TextColor = Color.FromArgb("#1C1F27"),
				VerticalOptions = LayoutOptions.Center,
			}, 0, 0);

			var edit = new Button
			{
				Text = L.T("Common_Edit"),
				FontSize = 12,
				Padding = new Thickness(10, 2),
				BackgroundColor = Color.FromArgb("#1E2633"),
				TextColor = Color.FromArgb("#1C1F27"),
				CommandParameter = puzzle,
			};
			edit.Clicked += OnEditPuzzleClicked;
			row.Add(edit, 1, 0);

			var remove = new Button
			{
				Text = "✕",
				FontSize = 12,
				Padding = new Thickness(8, 2),
				BackgroundColor = Colors.Transparent,
				TextColor = Color.FromArgb("#FF9A7A"),
				CommandParameter = puzzle,
			};
			remove.Clicked += OnRemovePuzzleClicked;
			row.Add(remove, 2, 0);

			PuzzlesContainer.Add(row);
		}

		if (room.Puzzles.Count == 0)
		{
			PuzzlesContainer.Add(new Label
			{
				Text = L.T("Inspector_NoPuzzles"),
				Style = (Style)Application.Current!.Resources["Caption"],
			});
		}
	}

	private void RefreshWarnings()
	{
		WarningsContainer.Children.Clear();
		var warnings = _state.Warnings;

		if (warnings.Count == 0)
		{
			WarningsContainer.Add(new Label
			{
				Text = L.T("Inspector_AllGood"),
				FontSize = 11.5,
				TextColor = Color.FromArgb("#8B95AB"),
			});
			return;
		}

		foreach (var warning in warnings.Take(12))
		{
			var color = warning.Severity switch
			{
				MapWarningSeverity.Error => "#FF8A76",
				MapWarningSeverity.Warning => "#F2B54C",
				_ => "#8B95AB",
			};

			WarningsContainer.Add(new Label
			{
				Text = warning.Message,
				FontSize = 11.5,
				TextColor = Color.FromArgb(color),
			});
		}
	}

	private void UpdateAutoSaveStatus()
	{
		var index = Array.IndexOf(AutoSaveService.IntervalOptions, _autoSave.IntervalSeconds);
		if (index >= 0 && index != AutoSavePicker.SelectedIndex)
		{
			_suppress = true;
			AutoSavePicker.SelectedIndex = index;
			_suppress = false;
		}

		if (_autoSave.IntervalSeconds <= 0)
		{
			AutoSaveStatusLabel.Text = L.T("Inspector_AutoSaveOff");
			return;
		}

		AutoSaveStatusLabel.Text = _autoSave.LastSavedUtc is DateTime last
			? L.F("Inspector_LastSave", last.ToLocalTime().ToString("HH:mm:ss"), DescribeInterval(_autoSave.IntervalSeconds))
			: L.F("Inspector_SavesEvery", DescribeInterval(_autoSave.IntervalSeconds));
	}

	private static string DescribeInterval(int seconds) => seconds switch
	{
		< 60 => L.F("Interval_Seconds", seconds),
		60 => L.T("Interval_Minute"),
		_ => L.F("Interval_Minutes", seconds / 60),
	};

	private void OnStateLayerChanged() => Refresh();

	private void OnMapTitleChanged(object? sender, TextChangedEventArgs e)
	{
		if (_suppress)
			return;
		_state.SetTitle(e.NewTextValue);
	}

	private void OnMapAuthorChanged(object? sender, TextChangedEventArgs e)
	{
		if (_suppress)
			return;
		_state.SetAuthor(e.NewTextValue ?? string.Empty);
	}

	private void OnLayerPicked(object? sender, EventArgs e)
	{
		if (_suppress || LayerPicker.SelectedIndex < 0 || LayerPicker.SelectedIndex >= _layers.Count)
			return;

		_state.CurrentLayerNumber = _layers[LayerPicker.SelectedIndex].Number;

		_suppress = true;
		LayerNameEntry.Text = _state.CurrentLayer.Name;
		_suppress = false;
		UpdateLayerSummary();
	}

	private void OnLayerNameChanged(object? sender, TextChangedEventArgs e)
	{
		if (_suppress)
			return;

		_state.RenameCurrentLayer(e.NewTextValue ?? string.Empty);
		_suppress = true;
		RefreshLayerPicker();
		_suppress = false;
	}

	private void OnFloorAboveClicked(object? sender, EventArgs e) => _state.AddLayerRelative(1);

	private void OnFloorBelowClicked(object? sender, EventArgs e) => _state.AddLayerRelative(-1);

	private void OnRoomNameChanged(object? sender, TextChangedEventArgs e)
	{
		if (_suppress || _state.SelectedRoom is not MapRoom room)
			return;

		room.Name = string.IsNullOrWhiteSpace(e.NewTextValue) ? room.Name : e.NewTextValue;
		_state.RoomEdited();
	}

	private async void OnEditDescriptionClicked(object? sender, EventArgs e)
	{
		if (_state.SelectedRoom is not MapRoom room)
			return;

		var popup = new DescriptionPopup(L.F("Inspector_DescriptionHeading", room.Name), room.Description);
		await MopupService.Instance.PushAsync(popup);
		var result = await popup.Result;
		if (result is null)
			return;

		room.Description = result;
		_state.RoomEdited(L.T("Status_DescriptionUpdated"));
		RefreshLive();
	}

	private void OnHiddenToggled(object? sender, ToggledEventArgs e)
	{
		if (_suppress || _state.SelectedRoom is not MapRoom room || room.IsHidden == e.Value)
			return;

		_state.SetRoomHidden(e.Value);
		RefreshLive();
	}

	private void OnRoomKindChanged(object? sender, EventArgs e)
	{
		if (_suppress || _state.SelectedRoom is not MapRoom room || RoomKindPicker.SelectedIndex < 0)
			return;

		room.Kind = (RoomKind)RoomKindPicker.SelectedIndex;
		BridgeSection.IsVisible = room.Kind == RoomKind.Bridge;

		if (room.Kind == RoomKind.Bridge)
		{
			if (room.BridgeSpanLayer != room.Layer)
				room.BridgeSpanLayer = room.Layer;

			if (room.BridgeSpanRoomId is null ||
				_state.Document.FindRoom(room.BridgeSpanLayer, room.BridgeSpanRoomId.Value) is null)
			{
				room.BridgeSpanRoomId = _state.Document.RoomsOn(room.BridgeSpanLayer)
					.FirstOrDefault(r => r.Id != room.Id)?.Id;
			}

			_suppress = true;
			BridgeLayerEntry.Text = room.BridgeSpanLayer.ToString();
			_suppress = false;
			RebuildBridgeRooms(room);
		}

		_state.RoomEdited(L.T("Status_RoomTypeUpdated"));
	}

	private void OnBridgeLayerChanged(object? sender, TextChangedEventArgs e)
	{
		if (_suppress || _state.SelectedRoom is not MapRoom room)
			return;

		if (!int.TryParse(e.NewTextValue, out var layer))
			return;

		room.BridgeSpanLayer = layer;
		RebuildBridgeRooms(room);
		_state.RoomEdited(L.T("Status_BridgeUpdated"));
	}

	private void RebuildBridgeRooms(MapRoom room)
	{
		var candidates = _state.Document.RoomsOn(room.BridgeSpanLayer)
			.Where(r => r.Id != room.Id)
			.OrderBy(r => r.Id)
			.ToList();

		_suppress = true;
		try
		{
			BridgeRoomPicker.ItemsSource = candidates;
			var index = room.BridgeSpanRoomId is int id ? candidates.FindIndex(r => r.Id == id) : -1;
			BridgeRoomPicker.SelectedIndex = index;
		}
		finally
		{
			_suppress = false;
		}
	}

	private void OnBridgeRoomPicked(object? sender, EventArgs e)
	{
		if (_suppress || _state.SelectedRoom is not MapRoom room)
			return;

		if (BridgeRoomPicker.SelectedItem is not MapRoom beneath)
			return;

		room.BridgeSpanRoomId = beneath.Id;
		_state.RoomEdited(L.T("Status_BridgeUpdated"));
	}

	private void OnAddConnectionClicked(object? sender, EventArgs e)
	{
		if (_state.SelectedRoom is null)
			return;

		if (!RoomReference.TryParse(ConnectionReferenceEntry.Text, out var target))
		{
			_state.Status = "Connections use layer-room, for example 1-4 or -1-2.";
			return;
		}

		var direction = ConnectionDirectionPicker.SelectedIndex >= 0 && ConnectionDirectionPicker.SelectedIndex < DirectionOptions.Length
			? DirectionOptions[ConnectionDirectionPicker.SelectedIndex]
			: MapDirection.North;

		if (_state.AddConnection(direction, target))
		{
			ConnectionReferenceEntry.Text = string.Empty;
			RefreshLive();
		}
	}

	private void OnRemoveConnectionClicked(object? sender, EventArgs e)
	{
		if ((sender as Button)?.CommandParameter is not MapConnection connection)
			return;

		_state.RemoveConnection(connection);
		RefreshLive();
	}

	private async void OnAddPuzzleClicked(object? sender, EventArgs e)
	{
		if (_state.SelectedRoom is not MapRoom room)
			return;

		var draft = new MapPuzzle { Title = L.F("Puzzle_DefaultName", room.Puzzles.Count + 1) };
		var popup = new PuzzlePopup(draft);
		await MopupService.Instance.PushAsync(popup);
		var result = await popup.Result;
		if (result is null)
			return;

		_state.AddPuzzle(result);
		Refresh();
	}

	private async void OnEditPuzzleClicked(object? sender, EventArgs e)
	{
		if ((sender as Button)?.CommandParameter is not MapPuzzle puzzle)
			return;

		var popup = new PuzzlePopup(puzzle);
		await MopupService.Instance.PushAsync(popup);
		var result = await popup.Result;
		if (result is null)
			return;

		_state.UpdatePuzzle(puzzle, result);
		Refresh();
	}

	private void OnRemovePuzzleClicked(object? sender, EventArgs e)
	{
		if ((sender as Button)?.CommandParameter is not MapPuzzle puzzle)
			return;

		_state.RemovePuzzle(puzzle);
		Refresh();
	}

	private async void OnDeleteRoomClicked(object? sender, EventArgs e)
	{
		var room = _state.SelectedRoom;
		if (room is null)
			return;

		var confirmed = await EditorActions.ConfirmAsync(
			L.T("Dialog_DeleteTitle"),
			L.F("Dialog_DeleteSimple", room.Name, room.Reference),
			L.T("Common_Delete"),
			L.T("Common_Cancel"));

		if (confirmed)
			_state.DeleteSelectedRoom();
	}

	private void OnAutoSaveIntervalChanged(object? sender, EventArgs e)
	{
		if (_suppress || AutoSavePicker.SelectedIndex < 0)
			return;

		_autoSave.IntervalSeconds = AutoSaveService.IntervalOptions[AutoSavePicker.SelectedIndex];
		UpdateAutoSaveStatus();
	}

	private async void OnSaveNowClicked(object? sender, EventArgs e)
	{
		await _autoSave.SaveAsync(userInitiated: true);
		UpdateAutoSaveStatus();
	}
}
