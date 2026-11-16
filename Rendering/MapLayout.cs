using AdventureMapper.Models;
using SkiaSharp;

namespace AdventureMapper.Rendering;

/// <summary>Grid placement of the rooms on a single layer, in canvas coordinates.</summary>
public sealed class MapLayout
{
	public const float RoomWidth = 250f;
	public const float RoomHeight = 150f;
	public const float ColumnGap = 34f;
	public const float RowGap = 30f;
	public const float Margin = 70f;
	public const float CellWidth = (float)MapConstants.GridCellWidth;
	public const float CellHeight = (float)MapConstants.GridCellHeight;

	private readonly Dictionary<int, SKRect> _bounds = new();

	public IReadOnlyList<MapRoom> Rooms { get; private init; } = Array.Empty<MapRoom>();

	public float Width { get; private set; }

	public float Height { get; private set; }

	public static MapLayout Build(AdventureMapDocument document, int layerNumber, float minWidth = 1400f, float minHeight = 900f)
	{
		var layout = new MapLayout
		{
			Rooms = document.RoomsOn(layerNumber).OrderBy(r => r.Row).ThenBy(r => r.Column).ToList(),
		};

		if (layout.Rooms.Count == 0)
		{
			layout.Width = minWidth;
			layout.Height = minHeight;
			return layout;
		}

		var minColumn = layout.Rooms.Min(r => r.Column);
		var minRow = layout.Rooms.Min(r => r.Row);

		foreach (var room in layout.Rooms)
		{
			var x = Margin + (room.Column - minColumn) * CellWidth;
			var y = Margin + (room.Row - minRow) * CellHeight;
			layout._bounds[room.Id] = SKRect.Create(x, y, RoomWidth, RoomHeight);
		}

		// A dragged room may sit past its grid cell, so the canvas grows to include it.
		var right = layout.Rooms.Max(room => layout.BoundsOf(room).Right);
		var bottom = layout.Rooms.Max(room => layout.BoundsOf(room).Bottom);
		layout.Width = Math.Max(minWidth, right + Margin);
		layout.Height = Math.Max(minHeight, bottom + Margin);
		return layout;
	}

	/// <summary>The room's rectangle: its grid cell plus any free placement made by dragging.</summary>
	public SKRect BoundsOf(MapRoom room) =>
		_bounds.TryGetValue(room.Id, out var bounds)
			? SKRect.Create(bounds.Left + (float)room.OffsetX, bounds.Top + (float)room.OffsetY, bounds.Width, bounds.Height)
			: SKRect.Empty;

	public MapRoom? HitTest(SKPoint point) =>
		Rooms.FirstOrDefault(r => BoundsOf(r) is var bounds && !bounds.IsEmpty && bounds.Contains(point));

	public const float HandleRadius = 10f;

	/// <summary>
	/// Where the drag handle for a direction sits, just outside the room. The offset plus
	/// the radius stays inside the gap between neighbouring rooms, so handles never cover
	/// the room next door.
	/// </summary>
	public static SKPoint HandlePosition(SKRect room, MapDirection direction)
	{
		const float offset = 14f;
		var (column, row) = MapRules.Offset(direction);
		return new SKPoint(
			room.MidX + column * (room.Width / 2f + offset),
			room.MidY + row * (room.Height / 2f + offset));
	}

	/// <summary>
	/// Finds the link nearest to a point, so tapping a line can toggle it. Only links
	/// between rooms on this layer are considered.
	/// </summary>
	public (MapRoom From, MapRoom To)? HitTestLink(AdventureMapDocument document, SKPoint point, float tolerance = 9f)
	{
		(MapRoom From, MapRoom To)? best = null;
		var bestDistance = tolerance;

		foreach (var room in Rooms)
		{
			var from = BoundsOf(room);
			if (from.IsEmpty)
				continue;

			foreach (var connection in room.Connections)
			{
				var target = Rooms.FirstOrDefault(candidate => candidate.Id == connection.TargetRoom);
				if (target is null || ReferenceEquals(target, room))
					continue;

				var distance = DistanceToSegment(point, new SKPoint(from.MidX, from.MidY), new SKPoint(BoundsOf(target).MidX, BoundsOf(target).MidY));
				if (distance > bestDistance)
					continue;

				bestDistance = distance;
				best = (room, target);
			}
		}

		_ = document;
		return best;
	}

	private static float DistanceToSegment(SKPoint point, SKPoint start, SKPoint end)
	{
		var lengthSquared = (end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y);
		if (lengthSquared < 0.0001f)
			return SKPoint.Distance(point, start);

		var t = ((point.X - start.X) * (end.X - start.X) + (point.Y - start.Y) * (end.Y - start.Y)) / lengthSquared;
		t = Math.Clamp(t, 0f, 1f);
		var nearest = new SKPoint(start.X + t * (end.X - start.X), start.Y + t * (end.Y - start.Y));
		return SKPoint.Distance(point, nearest);
	}
}
