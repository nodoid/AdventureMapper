using AdventureMapper.Models;
using AdventureMapper.Rendering;
using AdventureMapper.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace AdventureMapper.Views;

public partial class MapCanvasView : ContentView
{
	private static readonly MapDirection[] DragDirections =
	{
		MapDirection.North, MapDirection.NorthEast, MapDirection.East, MapDirection.SouthEast,
		MapDirection.South, MapDirection.SouthWest, MapDirection.West, MapDirection.NorthWest,
	};

	private const float HandleHitRadius = 14f;

	private readonly MapEditorState _state;
	private readonly MapRenderer _renderer = new();
	private MapLayout _layout;
	private bool _dragging;
	private MapDirection? _dragDirection;
	private SKPoint? _dragPoint;
	private bool _suppressTap;
	private MapRoom? _movingRoom;
	private SKPoint _moveStart;
	private SKPoint _moveOrigin;
	private bool _moved;

	public MapCanvasView(MapEditorState state)
	{
		InitializeComponent();
		_state = state;
		_layout = MapLayout.Build(state.Document, state.CurrentLayerNumber);
		ApplySize();

		state.StructureChanged += OnStructureChanged;
		state.ContentChanged += OnContentChanged;
		state.SelectionChanged += OnSelectionChanged;
		state.RoomAdded += OnRoomAdded;
	}

	private void OnStructureChanged(object? sender, EventArgs e) => Rebuild();

	private void OnContentChanged(object? sender, EventArgs e) => CanvasSurface.InvalidateSurface();

	private void OnSelectionChanged(object? sender, EventArgs e) => CanvasSurface.InvalidateSurface();

	private void Rebuild()
	{
		_layout = MapLayout.Build(_state.Document, _state.CurrentLayerNumber);
		ApplySize();
		CanvasSurface.InvalidateSurface();
	}

	private void ApplySize()
	{
		CanvasSurface.WidthRequest = _layout.Width;
		CanvasSurface.HeightRequest = _layout.Height;
	}

	private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
	{
		var canvas = e.Surface.Canvas;
		canvas.Clear(SKColor.Parse("#F3F5F9"));
		_renderer.DrawLayer(
			canvas,
			_state.Document,
			_state.CurrentLayerNumber,
			_layout,
			_state.SelectedRoom,
			print: false,
			_dragDirection,
			_dragPoint);
	}

	private void OnCanvasTapped(object? sender, TappedEventArgs e)
	{
		// A drag that started on a direction handle also ends with a tap; ignore that one.
		if (_suppressTap)
		{
			_suppressTap = false;
			return;
		}

		var position = e.GetPosition(CanvasSurface);
		if (position is null)
			return;

		var point = new SKPoint((float)position.Value.X, (float)position.Value.Y);
		var room = _layout.HitTest(point);
		if (room is not null)
		{
			_state.SelectRoom(room);
			return;
		}

		// Tapping a link switches it between two-way and one-way.
		if (_layout.HitTestLink(_state.Document, point) is { } link)
			_state.ToggleLink(link.From, link.To);
	}

	private void OnPointerPressed(object? sender, PointerEventArgs e)
	{
		var position = e.GetPosition(CanvasSurface);
		_suppressTap = false;
		if (position is null)
			return;

		var point = new SKPoint((float)position.Value.X, (float)position.Value.Y);

		if (_state.SelectedRoom is MapRoom selected)
		{
			var bounds = _layout.BoundsOf(selected);
			if (!bounds.IsEmpty)
			{
				foreach (var direction in DragDirections)
				{
					if (SKPoint.Distance(MapLayout.HandlePosition(bounds, direction), point) > HandleHitRadius)
						continue;

					_dragging = true;
					_dragDirection = direction;
					_dragPoint = point;
					CanvasSurface.InvalidateSurface();
					return;
				}
			}
		}

		// Pressing inside any room starts moving it; its links stay attached to its centre.
		var room = _layout.HitTest(point);
		if (room is null)
			return;

		_state.SelectRoom(room);
		_movingRoom = room;
		_moveStart = point;
		_moveOrigin = _layout.BoundsOf(room).Location;
		_moved = false;
	}

	private void OnPointerMoved(object? sender, PointerEventArgs e)
	{
		if (e.GetPosition(CanvasSurface) is not { } position)
			return;

		var point = new SKPoint((float)position.X, (float)position.Y);

		if (_dragging)
		{
			_dragPoint = point;
			CanvasSurface.InvalidateSurface();
			return;
		}

		if (_movingRoom is not { } room)
			return;

		if (!_moved && SKPoint.Distance(point, _moveStart) < 3f)
			return;

		var firstMove = !_moved;
		_moved = true;
		_suppressTap = true;

		var current = _layout.BoundsOf(room);
		var x = Math.Max(0, _moveOrigin.X + (point.X - _moveStart.X));
		var y = Math.Max(0, _moveOrigin.Y + (point.Y - _moveStart.Y));
		// One history entry per drag, recorded on its first movement.
		_state.MoveRoom(room, x - current.Left, y - current.Top, recordHistory: firstMove);

		var moved = _layout.BoundsOf(room);
		CanvasSurface.WidthRequest = Math.Max(CanvasSurface.WidthRequest, moved.Right + MapLayout.Margin);
		CanvasSurface.HeightRequest = Math.Max(CanvasSurface.HeightRequest, moved.Bottom + MapLayout.Margin);
	}

	private void OnPointerReleased(object? sender, PointerEventArgs e)
	{
		if (!_dragging)
		{
			if (_movingRoom is null)
				return;

			var movedRoom = _movingRoom;
			var wasMoved = _moved;
			_movingRoom = null;
			_moved = false;
			if (!wasMoved)
				return;

			_suppressTap = true;
			if (movedRoom is not null)
				_state.SnapRoomToGrid(movedRoom, recordHistory: false);
			Rebuild();
			return;
		}

		var direction = _dragDirection;
		var selected = _state.SelectedRoom;
		var position = e.GetPosition(CanvasSurface);

		_dragging = false;
		_dragDirection = null;
		_dragPoint = null;
		_suppressTap = true;
		CanvasSurface.InvalidateSurface();

		if (position is null || direction is null || selected is null)
			return;

		var target = _layout.HitTest(new SKPoint((float)position.Value.X, (float)position.Value.Y));
		if (target is not null)
			_state.ConnectRooms(selected, direction.Value, target);
	}

	private void OnRoomAdded(object? sender, MapRoom room)
	{
		// Let the ScrollView measure the resized canvas before scrolling to the new room.
		Dispatcher.Dispatch(async () =>
		{
			await Task.Delay(40);
			await ScrollToRoomAsync(room);
		});
	}

	private async Task ScrollToRoomAsync(MapRoom room)
	{
		var bounds = _layout.BoundsOf(room);
		if (bounds.IsEmpty)
			return;

		var viewportWidth = Scroller.Width;
		var viewportHeight = Scroller.Height;
		if (viewportWidth <= 0 || viewportHeight <= 0)
			return;

		var x = Math.Max(0, bounds.MidX - viewportWidth / 2);
		var y = Math.Max(0, bounds.MidY - viewportHeight / 2);
		await Scroller.ScrollToAsync(x, y, animated: true);
	}
}
