using System.ComponentModel;
using System.Runtime.CompilerServices;
using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;

namespace AdventureMapper.Services;

/// <summary>
/// Shared editing state used by both the desktop workspace and the tablet tabs.
/// </summary>
public sealed class MapEditorState : INotifyPropertyChanged
{
	private AdventureMapDocument _document;
	private MapRoom? _selectedRoom;
	private MapPuzzle? _selectedPuzzle;
	private int _currentLayer;
	private string? _filePath;
	private bool _isDirty;
	private string _status = L.T("Status_Ready");
	private bool _snapToGrid;
	private readonly Stack<LayoutSnapshot> _undo = new();
	private readonly Stack<LayoutSnapshot> _redo = new();

	private readonly record struct RoomPlacement(int Layer, int Id, double OffsetX, double OffsetY);

	private sealed record LayoutSnapshot(IReadOnlyList<RoomPlacement> Rooms, bool WasDirty);

	public MapEditorState()
	{
		_document = AdventureMapDocument.CreateNew();
		_status = L.T("Status_NewMap");
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Raised when rooms, layers or connections are added or removed.</summary>
	public event EventHandler? StructureChanged;

	/// <summary>Raised when the contents of the current room/layer change (redraw only).</summary>
	public event EventHandler? ContentChanged;

	/// <summary>Raised when the selected room or puzzle changes.</summary>
	public event EventHandler? SelectionChanged;

	/// <summary>Raised after a room has been created so the canvas can scroll to it.</summary>
	public event EventHandler<MapRoom>? RoomAdded;

	public AdventureMapDocument Document => _document;

	public string? FilePath
	{
		get => _filePath;
		private set
		{
			if (_filePath == value)
				return;
			_filePath = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(HasFile));
			OnPropertyChanged(nameof(FileLabel));
		}
	}

	public bool HasFile => !string.IsNullOrEmpty(_filePath);

	public string FileLabel => string.IsNullOrEmpty(_filePath) ? L.T("Status_NotSaved") : Path.GetFileName(_filePath);

	public bool IsDirty
	{
		get => _isDirty;
		private set
		{
			if (_isDirty == value)
				return;
			_isDirty = value;
			OnPropertyChanged();
		}
	}

	public string Status
	{
		get => _status;
		set
		{
			if (_status == value)
				return;
			_status = value;
			OnPropertyChanged();
		}
	}

	public int CurrentLayerNumber
	{
		get => _currentLayer;
		set
		{
			var layer = _document.GetOrCreateLayer(value);
			if (_currentLayer == layer.Number)
				return;

			_currentLayer = layer.Number;
			OnPropertyChanged();
			OnPropertyChanged(nameof(CurrentLayer));
			StructureChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public MapLayer CurrentLayer => _document.GetOrCreateLayer(_currentLayer);

	public MapRoom? SelectedRoom
	{
		get => _selectedRoom;
		private set
		{
			if (ReferenceEquals(_selectedRoom, value))
				return;

			_selectedRoom = value;
			_selectedPuzzle = null;
			OnPropertyChanged();
			OnPropertyChanged(nameof(HasSelection));
			SelectionChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public MapPuzzle? SelectedPuzzle
	{
		get => _selectedPuzzle;
		set
		{
			if (ReferenceEquals(_selectedPuzzle, value))
				return;

			_selectedPuzzle = value;
			OnPropertyChanged();
			SelectionChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public bool HasSelection => _selectedRoom is not null;

	/// <summary>True when there is a room move, snap or layout reset to undo.</summary>
	public bool CanUndo => _undo.Count > 0;

	/// <summary>True when an undone change can be reapplied.</summary>
	public bool CanRedo => _redo.Count > 0;

	/// <summary>When on, a dragged room clicks into the nearest grid cell on release.</summary>
	public bool SnapToGrid
	{
		get => _snapToGrid;
		set
		{
			if (_snapToGrid == value)
				return;

			_snapToGrid = value;
			OnPropertyChanged();
			Status = L.T(value ? "Status_SnapOn" : "Status_SnapOff");
		}
	}

	public IReadOnlyList<MapWarning> Warnings => MapRules.Validate(_document);

	public void NewDocument()
	{
		_document = AdventureMapDocument.CreateNew();
		_filePath = null;
		_currentLayer = 0;
		_selectedRoom = null;
		_selectedPuzzle = null;
		IsDirty = false;
		Status = L.T("Status_NewMap");
		RaiseDocumentReplaced();
	}

	public void Load(AdventureMapDocument document, string? path)
	{
		_document = document;
		_filePath = path;
		_currentLayer = document.Layers.Any(l => l.Number == 0)
			? 0
			: document.Layers.Count > 0
				? document.Layers.Min(l => l.Number)
				: 0;
		_selectedRoom = document.Rooms.FirstOrDefault();
		_selectedPuzzle = null;
		IsDirty = false;
		Status = string.IsNullOrEmpty(path)
			? L.T("Status_MapLoaded")
			: L.F("Status_Opened", Path.GetFileName(path));
		RaiseDocumentReplaced();
	}

	public void SetFilePath(string? path)
	{
		FilePath = path;
	}

	public void SelectRoom(MapRoom? room) => SelectedRoom = room;

	public void SelectPuzzle(MapPuzzle? puzzle) => SelectedPuzzle = puzzle;

	/// <summary>Marks the document as changed after an edit that does not alter its structure.</summary>
	public void RoomEdited(string? status = null)
	{
		if (_selectedRoom is null)
			return;

		_document.Touch();
		IsDirty = true;
		if (status is not null)
			Status = status;
		ContentChanged?.Invoke(this, EventArgs.Empty);
	}

	public void MarkSaved(string? status = null)
	{
		_document.Touch();
		IsDirty = false;
		if (status is not null)
			Status = status;
	}

	public void SetTitle(string title)
	{
		_document.Title = string.IsNullOrWhiteSpace(title) ? "Untitled adventure" : title.Trim();
		IsDirty = true;
		ContentChanged?.Invoke(this, EventArgs.Empty);
	}

	public void SetAuthor(string author)
	{
		_document.Author = author;
		IsDirty = true;
		ContentChanged?.Invoke(this, EventArgs.Empty);
	}

	public void RenameCurrentLayer(string name)
	{
		var layer = CurrentLayer;
		layer.Name = string.IsNullOrWhiteSpace(name) ? MapRules.DefaultLayerName(layer.Number) : name.Trim();
		IsDirty = true;
		OnPropertyChanged(nameof(CurrentLayer));
		ContentChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Adds a room with no links at all: a secret room, or one that is only reached by
	/// solving a puzzle. It goes in the first free cell below the rooms on this floor and
	/// starts hidden, so it can be linked later with a hidden exit or a puzzle reward.
	/// </summary>
	public MapRoom AddDetachedRoom()
	{
		var layer = _currentLayer;
		var row = _document.RoomsOn(layer).Select(room => room.Row).DefaultIfEmpty(-1).Max() + 1;
		var column = 0;
		while (!_document.IsCellFree(layer, column, row))
			column++;

		var room = _document.AddRoom(layer, column, row);
		room.IsHidden = true;

		_currentLayer = room.Layer;
		_selectedRoom = room;
		_selectedPuzzle = null;
		IsDirty = true;
		Status = L.F("Status_AddedHiddenRoom", room.Reference);

		OnPropertyChanged(nameof(CurrentLayerNumber));
		OnPropertyChanged(nameof(CurrentLayer));
		OnPropertyChanged(nameof(SelectedRoom));
		OnPropertyChanged(nameof(HasSelection));
		StructureChanged?.Invoke(this, EventArgs.Empty);
		SelectionChanged?.Invoke(this, EventArgs.Empty);
		RoomAdded?.Invoke(this, room);
		return room;
	}

	/// <summary>
	/// Moves a room freely on the canvas. The grid cell is unchanged, so connections keep
	/// their direction; the links follow the room because they are drawn between centres.
	/// </summary>
	public void MoveRoom(MapRoom room, double deltaX, double deltaY, bool recordHistory = true)
	{
		if ((deltaX == 0 && deltaY == 0) || !_document.Rooms.Contains(room))
			return;

		if (recordHistory)
			PushLayoutHistory();

		room.OffsetX += deltaX;
		room.OffsetY += deltaY;
		_document.Touch();
		IsDirty = true;
		ContentChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Snaps every room back onto its grid cell, discarding any dragged offsets.</summary>
	public void ResetLayout()
	{
		var moved = _document.Rooms.Where(room => room.OffsetX != 0 || room.OffsetY != 0).ToList();
		if (moved.Count == 0)
			return;

		PushLayoutHistory();
		foreach (var room in moved)
		{
			room.OffsetX = 0;
			room.OffsetY = 0;
		}

		_document.Touch();
		IsDirty = true;
		Status = L.T("Status_LayoutReset");
		StructureChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Snaps a dragged room to the nearest grid cell, when snapping is switched on.</summary>
	public void SnapRoomToGrid(MapRoom room, bool recordHistory = true)
	{
		if (!_snapToGrid || !_document.Rooms.Contains(room))
			return;

		var x = Math.Round(room.OffsetX / MapConstants.GridCellWidth, MidpointRounding.AwayFromZero) * MapConstants.GridCellWidth;
		var y = Math.Round(room.OffsetY / MapConstants.GridCellHeight, MidpointRounding.AwayFromZero) * MapConstants.GridCellHeight;
		MoveRoom(room, x - room.OffsetX, y - room.OffsetY, recordHistory);
	}

	/// <summary>Undoes the last room move, snap or layout reset.</summary>
	public bool Undo()
	{
		if (_undo.Count == 0)
			return false;

		var snapshot = _undo.Pop();
		_redo.Push(CaptureLayout());
		ApplyLayout(snapshot);
		Status = L.T("Status_Undone");
		return true;
	}

	/// <summary>Reapplies the last undone layout change.</summary>
	public bool Redo()
	{
		if (_redo.Count == 0)
			return false;

		var snapshot = _redo.Pop();
		_undo.Push(CaptureLayout());
		ApplyLayout(snapshot);
		Status = L.T("Status_Redone");
		return true;
	}

	/// <summary>Marks the selected room as hidden (secret) or visible again.</summary>
	public void SetRoomHidden(bool hidden)
	{
		if (_selectedRoom is null)
			return;

		_selectedRoom.IsHidden = hidden;
		RoomEdited(L.T("Status_HiddenUpdated"));
	}

	public MapLayer AddLayerRelative(int offset)
	{
		var layer = _document.GetOrCreateLayer(_currentLayer + offset);
		CurrentLayerNumber = layer.Number;
		Status = L.F("Status_Layer", layer.Number, layer.Name);
		return layer;
	}

	/// <summary>Adds a room next to the selection (or in a free cell when nothing is selected).</summary>
	public MapRoom AddRoom(MapDirection direction)
	{
		var selected = _selectedRoom;
		var layer = _currentLayer;
		var (dc, dr) = MapRules.Offset(direction);
		int column;
		int row;

		if (selected is null)
		{
			column = 0;
			row = 0;
		}
		else
		{
			layer = selected.Layer;
			column = selected.Column + dc;
			row = selected.Row + dr;

			if (direction is MapDirection.Up or MapDirection.Down)
			{
				layer = direction == MapDirection.Up
					? selected.Layer + 1
					: MapRules.DownTargetLayer(selected);
				column = selected.Column;
				row = selected.Row;
			}
		}

		var (freeColumn, freeRow) = FindFreeCell(layer, column, row, dc, dr);
		var room = _document.AddRoom(layer, freeColumn, freeRow);

		if (selected is not null)
		{
			_document.Connect(selected, direction, room);
			_document.Connect(room, MapRules.Opposite(direction), selected);
		}

		_currentLayer = layer;
		_selectedRoom = room;
		_selectedPuzzle = null;
		IsDirty = true;
		Status = L.F("Status_AddedRoom", room.Name, room.Reference);

		OnPropertyChanged(nameof(CurrentLayerNumber));
		OnPropertyChanged(nameof(CurrentLayer));
		OnPropertyChanged(nameof(SelectedRoom));
		OnPropertyChanged(nameof(HasSelection));
		StructureChanged?.Invoke(this, EventArgs.Empty);
		SelectionChanged?.Invoke(this, EventArgs.Empty);
		RoomAdded?.Invoke(this, room);
		return room;
	}

	public void DeleteSelectedRoom()
	{
		if (_selectedRoom is null)
			return;

		var room = _selectedRoom;
		var label = $"{room.Name} ({room.Reference})";
		_document.RemoveRoom(room);

		_selectedRoom = null;
		_selectedPuzzle = null;
		IsDirty = true;
		Status = L.F("Status_Deleted", label);

		if (!_document.Layers.Any(l => l.Number == _currentLayer))
			_currentLayer = _document.Layers.Min(l => l.Number);

		OnPropertyChanged(nameof(CurrentLayerNumber));
		OnPropertyChanged(nameof(CurrentLayer));
		OnPropertyChanged(nameof(SelectedRoom));
		OnPropertyChanged(nameof(HasSelection));
		StructureChanged?.Invoke(this, EventArgs.Empty);
		SelectionChanged?.Invoke(this, EventArgs.Empty);
	}

	public bool AddConnection(MapDirection direction, RoomReference target)
	{
		if (_selectedRoom is null)
			return false;

		var targetRoom = _document.FindRoom(target);
		if (targetRoom is null)
		{
			Status = L.F("Status_NoRoom", target);
			return false;
		}

		_document.Connect(_selectedRoom, direction, targetRoom);
		RoomEdited(L.F("Status_Linked", _selectedRoom.Reference, MapRules.DirectionLabel(direction), targetRoom.Reference));
		return true;
	}

	/// <summary>
	/// Connects two rooms on the map, adding the return link unless this is a one-way route.
	/// Used by dragging a direction handle from one room onto another.
	/// </summary>
	public bool ConnectRooms(MapRoom from, MapDirection direction, MapRoom to, bool bidirectional = true)
	{
		if (ReferenceEquals(from, to) || !_document.Rooms.Contains(from) || !_document.Rooms.Contains(to))
			return false;

		if (MapRules.LayerStep(direction) != 0)
			return false;

		_document.Connect(from, direction, to);
		if (bidirectional)
			_document.Connect(to, MapRules.Opposite(direction), from);

		_document.Touch();
		IsDirty = true;
		Status = L.F("Status_Linked", from.Reference, MapRules.DirectionLabel(direction), to.Reference);
		ContentChanged?.Invoke(this, EventArgs.Empty);
		return true;
	}

	/// <summary>
	/// Switches a link between two-way and one-way: a two-way link keeps only the leg from
	/// <paramref name="from"/> to <paramref name="to"/>, a one-way link gains its return leg.
	/// </summary>
	public bool ToggleLink(MapRoom from, MapRoom to)
	{
		if (ReferenceEquals(from, to))
			return false;

		var forward = from.Connections.FirstOrDefault(c => c.TargetLayer == to.Layer && c.TargetRoom == to.Id);
		var backward = to.Connections.FirstOrDefault(c => c.TargetLayer == from.Layer && c.TargetRoom == from.Id);
		if (forward is null && backward is null)
			return false;

		if (forward is not null && backward is not null)
		{
			to.Connections.Remove(backward);
			Status = L.F("Status_LinkOneWay", from.Reference, MapRules.DirectionLabel(forward.Direction), to.Reference);
		}
		else if (forward is not null)
		{
			_document.Connect(to, MapRules.Opposite(forward.Direction), from);
			Status = L.F("Status_LinkTwoWay", from.Reference, to.Reference);
		}
		else
		{
			_document.Connect(from, MapRules.Opposite(backward!.Direction), to);
			Status = L.F("Status_LinkTwoWay", from.Reference, to.Reference);
		}

		_document.Touch();
		IsDirty = true;
		ContentChanged?.Invoke(this, EventArgs.Empty);
		return true;
	}

	public void RemoveConnection(MapConnection connection)
	{
		if (_selectedRoom is null)
			return;

		_selectedRoom.Connections.Remove(connection);
		RoomEdited(L.T("Status_ConnectionRemoved"));
	}

	public MapPuzzle AddPuzzle() => AddPuzzle(new MapPuzzle());

	/// <summary>Adds a puzzle described by a Mopups dialog to the selected room.</summary>
	public MapPuzzle AddPuzzle(MapPuzzle puzzle)
	{
		if (_selectedRoom is null)
			throw new InvalidOperationException("Select a room before adding a puzzle.");

		var nextId = _selectedRoom.Puzzles.Count == 0 ? 1 : _selectedRoom.Puzzles.Max(p => p.Id) + 1;
		puzzle.Id = nextId;
		if (string.IsNullOrWhiteSpace(puzzle.Title))
			puzzle.Title = L.F("Puzzle_DefaultName", nextId);

		_selectedRoom.Puzzles.Add(puzzle);
		SelectedPuzzle = puzzle;
		RoomEdited(L.T("Status_PuzzleAdded"));
		return puzzle;
	}

	/// <summary>Copies an edited puzzle back onto the stored puzzle.</summary>
	public void UpdatePuzzle(MapPuzzle target, MapPuzzle updated)
	{
		target.Title = string.IsNullOrWhiteSpace(updated.Title) ? target.Title : updated.Title.Trim();
		target.Kind = updated.Kind;
		target.Prompt = updated.Prompt;
		target.Solution = updated.Solution;
		target.Reward = updated.Reward;
		SelectedPuzzle = target;
		RoomEdited(L.T("Status_PuzzleUpdated"));
	}

	public void RemovePuzzle(MapPuzzle puzzle)
	{
		if (_selectedRoom is null)
			return;

		_selectedRoom.Puzzles.Remove(puzzle);
		if (ReferenceEquals(_selectedPuzzle, puzzle))
			SelectedPuzzle = _selectedRoom.Puzzles.FirstOrDefault();
		RoomEdited(L.T("Status_PuzzleRemoved"));
	}

	public void NotifyPuzzleEdited(string? status = null)
	{
		_isDirty = true;
		RoomEdited(status ?? L.T("Status_PuzzleUpdated"));
	}

	private void PushLayoutHistory()
	{
		_undo.Push(CaptureLayout());
		_redo.Clear();
		OnPropertyChanged(nameof(CanUndo));
		OnPropertyChanged(nameof(CanRedo));
	}

	private LayoutSnapshot CaptureLayout() =>
		new(_document.Rooms.Select(room => new RoomPlacement(room.Layer, room.Id, room.OffsetX, room.OffsetY)).ToList(), IsDirty);

	private void ApplyLayout(LayoutSnapshot snapshot)
	{
		foreach (var placement in snapshot.Rooms)
		{
			if (_document.FindRoom(placement.Layer, placement.Id) is not { } room)
				continue;

			room.OffsetX = placement.OffsetX;
			room.OffsetY = placement.OffsetY;
		}

		IsDirty = snapshot.WasDirty;
		_document.Touch();
		OnPropertyChanged(nameof(CanUndo));
		OnPropertyChanged(nameof(CanRedo));
		StructureChanged?.Invoke(this, EventArgs.Empty);
	}

	private (int Column, int Row) FindFreeCell(int layer, int column, int row, int dc, int dr)
	{
		if (_document.IsCellFree(layer, column, row))
			return (column, row);

		if (dc == 0 && dr == 0)
		{
			dc = 1;
			dr = 0;
		}

		var candidateColumn = column;
		var candidateRow = row;
		for (var attempt = 0; attempt < 200; attempt++)
		{
			candidateColumn += dc;
			candidateRow += dr;
			if (_document.IsCellFree(layer, candidateColumn, candidateRow))
				return (candidateColumn, candidateRow);
		}

		return (column, row);
	}

	private void RaiseDocumentReplaced()
	{
		_undo.Clear();
		_redo.Clear();
		OnPropertyChanged(nameof(CanUndo));
		OnPropertyChanged(nameof(CanRedo));
		OnPropertyChanged(nameof(Document));
		OnPropertyChanged(nameof(FilePath));
		OnPropertyChanged(nameof(HasFile));
		OnPropertyChanged(nameof(FileLabel));
		OnPropertyChanged(nameof(CurrentLayerNumber));
		OnPropertyChanged(nameof(CurrentLayer));
		OnPropertyChanged(nameof(SelectedRoom));
		OnPropertyChanged(nameof(HasSelection));
		StructureChanged?.Invoke(this, EventArgs.Empty);
		SelectionChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
