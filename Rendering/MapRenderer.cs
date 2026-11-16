using System.Text;
using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using SkiaSharp;

namespace AdventureMapper.Rendering;

/// <summary>
/// Draws layers and whole map sheets with SkiaSharp, so the editor canvas and the
/// JPEG/PDF exports share one visual language.
/// </summary>
public sealed class MapRenderer
{
	private static readonly SKTypeface BoldTypeface = CreateTypeface(SKFontStyle.Bold);
	private static readonly SKTypeface RegularTypeface = CreateTypeface(SKFontStyle.Normal);
	private static readonly SKColor OneWayLink = new(0x25, 0x63, 0xEB);

	public static SKTypeface TypefaceFor(SKFontStyle style) =>
		style.Weight >= (int)SKFontStyleWeight.SemiBold ? BoldTypeface : RegularTypeface;

	private static readonly MapDirection[] DragDirections =
	{
		MapDirection.North, MapDirection.NorthEast, MapDirection.East, MapDirection.SouthEast,
		MapDirection.South, MapDirection.SouthWest, MapDirection.West, MapDirection.NorthWest,
	};

	public void DrawLayer(
		SKCanvas canvas,
		AdventureMapDocument document,
		int layerNumber,
		MapLayout layout,
		MapRoom? selectedRoom,
		bool print,
		MapDirection? dragDirection = null,
		SKPoint? dragPoint = null)
	{
		var palette = print ? Palette.Print : Palette.Screen;

		canvas.Save();
		using (var background = Fill(palette.Canvas))
			canvas.DrawRect(SKRect.Create(0, 0, layout.Width, layout.Height), background);

		DrawGrid(canvas, layout, palette);

		using var titleFont = new SKFont(TypefaceFor(SKFontStyle.Bold), 15.5f);
		using var bodyFont = new SKFont(TypefaceFor(SKFontStyle.Normal), 12f);
		using var smallFont = new SKFont(TypefaceFor(SKFontStyle.Normal), 10.5f);
		using var chipFont = new SKFont(TypefaceFor(SKFontStyle.Bold), 10.5f);
		using var ink = Fill(palette.Text);
		using var muted = Fill(palette.Muted);
		using var accent = Fill(palette.Accent);
		using var measure = new SKPaint { IsAntialias = true };

		// Same-layer links first, so rooms sit on top of them.
		foreach (var room in layout.Rooms)
		{
			foreach (var connection in room.Connections)
			{
				if (connection.TargetLayer != layerNumber)
					continue;

				var target = document.FindRoom(connection.Target);
				if (target is null)
					continue;

				var mutual = target.Connections.Any(c => c.TargetLayer == room.Layer && c.TargetRoom == room.Id);
				if (room.Id < target.Id)
					DrawLink(canvas, layout.BoundsOf(room), layout.BoundsOf(target), palette, arrowAtTarget: true, arrowAtSource: mutual);
				else if (!mutual)
					DrawLink(canvas, layout.BoundsOf(room), layout.BoundsOf(target), palette, arrowAtTarget: true, arrowAtSource: false);
			}
		}

		foreach (var room in layout.Rooms)
		{
			var bounds = layout.BoundsOf(room);
			foreach (var connection in room.Connections)
			{
				if (connection.TargetLayer != layerNumber)
					DrawChip(canvas, bounds, connection, palette, chipFont, measure);
			}
		}

		foreach (var room in layout.Rooms)
			DrawRoom(canvas, room, layout, selectedRoom, palette, titleFont, bodyFont, smallFont, chipFont, ink, muted, accent, measure);

		if (!print && selectedRoom is not null)
			DrawDragHandles(canvas, layout, selectedRoom, palette, chipFont, dragDirection, dragPoint);

		if (layout.Rooms.Count == 0)
		{
			canvas.DrawText(
				L.T("Canvas_NoRooms"),
				layout.Width / 2,
				layout.Height / 2,
				SKTextAlign.Center,
				bodyFont,
				muted);
		}

		canvas.Restore();
	}

	public void DrawSheet(SKCanvas canvas, AdventureMapDocument document, MapSheet sheet, bool print)
	{
		var palette = print ? Palette.Print : Palette.Screen;

		using (var background = Fill(palette.Canvas))
			canvas.DrawRect(new SKRect(0, 0, sheet.Width, sheet.Height), background);

		using var titleFont = new SKFont(TypefaceFor(SKFontStyle.Bold), 30f);
		using var metaFont = new SKFont(TypefaceFor(SKFontStyle.Normal), 12.5f);
		using var headerFont = new SKFont(TypefaceFor(SKFontStyle.Bold), 18f);
		using var ink = Fill(palette.Text);
		using var muted = Fill(palette.Muted);

		canvas.DrawText(document.Title, MapSheet.OuterMargin, MapSheet.OuterMargin + 18, SKTextAlign.Left, titleFont, ink);

		var layers = sheet.Panels.Select(p => p.Layer.Number).ToList();
		var range = layers.Count == 0
			? L.T("Export_NoLayers")
			: layers.Min() == layers.Max()
				? L.F("Export_LayerOne", layers.Min())
				: L.F("Export_LayerRange", layers.Min(), layers.Max());
		var updated = document.ModifiedUtc.ToLocalTime().ToString("d MMM yyyy HH:mm");
		var meta = document.Rooms.Count == 1
			? L.F("Export_MetaOne", range, updated)
			: L.F("Export_Meta", document.Rooms.Count, range, updated);
		canvas.DrawText(meta, MapSheet.OuterMargin, MapSheet.OuterMargin + 42, SKTextAlign.Left, metaFont, muted);
		canvas.DrawText(
			L.T("Export_PanelHint"),
			MapSheet.OuterMargin,
			MapSheet.OuterMargin + 61,
			SKTextAlign.Left,
			metaFont,
			muted);

		foreach (var panel in sheet.Panels)
		{
			using var frame = new SKPath();
			frame.AddRoundRect(panel.Frame, 14, 14);
			using (var fill = new SKPaint { Color = print ? new SKColor(0xFB, 0xFC, 0xFE) : new SKColor(0x15, 0x19, 0x22), IsAntialias = true })
				canvas.DrawPath(frame, fill);
			using (var stroke = new SKPaint { Color = palette.Grid, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f })
				canvas.DrawPath(frame, stroke);

			canvas.DrawText(
				L.F("Export_LayerHeading", panel.Layer.Number, panel.Layer.Name),
				panel.Frame.Left + MapSheet.PanelPadding,
				panel.Frame.Top + 32,
				SKTextAlign.Left,
				headerFont,
				ink);

			canvas.DrawText(
				panel.Layout.Rooms.Count == 1 ? L.T("Export_RoomOne") : L.F("Export_Rooms", panel.Layout.Rooms.Count),
				panel.Frame.Right - MapSheet.PanelPadding,
				panel.Frame.Top + 32,
				SKTextAlign.Right,
				metaFont,
				muted);

			canvas.Save();
			canvas.Translate(panel.Frame.Left + MapSheet.PanelPadding, panel.Frame.Top + MapSheet.HeaderHeight);
			canvas.ClipRect(new SKRect(0, 0, panel.Layout.Width, panel.Layout.Height));
			DrawLayer(canvas, document, panel.Layer.Number, panel.Layout, null, print);
			canvas.Restore();
		}
	}

	/// <summary>
	/// The direction handles around the selected room: press one and drag it onto another
	/// room to link the two.
	/// </summary>
	private static void DrawDragHandles(
		SKCanvas canvas,
		MapLayout layout,
		MapRoom room,
		Palette palette,
		SKFont font,
		MapDirection? dragDirection,
		SKPoint? dragPoint)
	{
		var bounds = layout.BoundsOf(room);
		if (bounds.IsEmpty)
			return;

		using var wash = Fill(SKColors.White);
		using var label = Fill(palette.Text);
		using var outline = new SKPaint
		{
			Color = palette.RoomBorder,
			IsAntialias = true,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 1.4f,
		};

		foreach (var direction in DragDirections)
		{
			var point = MapLayout.HandlePosition(bounds, direction);
			canvas.DrawCircle(point, MapLayout.HandleRadius, wash);
			canvas.DrawCircle(point, MapLayout.HandleRadius, outline);
			canvas.DrawText(MapRules.DirectionShortLabel(direction), point.X, point.Y + 3.6f, SKTextAlign.Center, font, label);
		}

		if (dragDirection is not MapDirection dragging || dragPoint is not SKPoint pointer)
			return;

		var start = MapLayout.HandlePosition(bounds, dragging);
		using var preview = new SKPaint
		{
			Color = palette.Accent,
			IsAntialias = true,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 2.5f,
		};
		canvas.DrawLine(start, pointer, preview);
		canvas.DrawCircle(pointer, 7f, preview);
	}

	private static void DrawGrid(SKCanvas canvas, MapLayout layout, Palette palette)
	{
		using var paint = new SKPaint
		{
			Color = palette.Grid,
			IsAntialias = false,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 1f,
		};

		for (var x = MapLayout.CellWidth; x < layout.Width; x += MapLayout.CellWidth)
			canvas.DrawLine(x, 0, x, layout.Height, paint);
		for (var y = MapLayout.CellHeight; y < layout.Height; y += MapLayout.CellHeight)
			canvas.DrawLine(0, y, layout.Width, y, paint);
	}

	private static void DrawLink(
		SKCanvas canvas,
		SKRect from,
		SKRect to,
		Palette palette,
		bool arrowAtTarget,
		bool arrowAtSource)
	{
		if (from.IsEmpty || to.IsEmpty)
			return;

		var fromCenter = new SKPoint(from.MidX, from.MidY);
		var toCenter = new SKPoint(to.MidX, to.MidY);
		var start = EdgePoint(from, toCenter);
		var end = EdgePoint(to, fromCenter);
		// A two-way link has arrowheads at both ends; one-way links stand out in blue.
		var linkColor = arrowAtSource ? palette.Link : OneWayLink;

		using var paint = new SKPaint
		{
			Color = linkColor,
			IsAntialias = true,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 2.5f,
		};

		var straight = Math.Abs(fromCenter.X - toCenter.X) < 1f || Math.Abs(fromCenter.Y - toCenter.Y) < 1f;
		if (straight)
		{
			canvas.DrawLine(start, end, paint);
		}
		else
		{
			var corner = new SKPoint(end.X, start.Y);
			canvas.DrawLine(start, corner, paint);
			canvas.DrawLine(corner, end, paint);
		}

		if (arrowAtTarget)
			DrawArrowHead(canvas, straight ? start : new SKPoint(end.X, start.Y), end, linkColor);
		if (arrowAtSource)
			DrawArrowHead(canvas, straight ? end : new SKPoint(start.X, end.Y), start, linkColor);
	}

	private static void DrawArrowHead(SKCanvas canvas, SKPoint from, SKPoint to, SKColor color)
	{
		var angle = MathF.Atan2(to.Y - from.Y, to.X - from.X);
		const float size = 9f;

		using var path = new SKPath();
		path.MoveTo(to);
		path.LineTo(to.X - size * MathF.Cos(angle - 0.45f), to.Y - size * MathF.Sin(angle - 0.45f));
		path.LineTo(to.X - size * MathF.Cos(angle + 0.45f), to.Y - size * MathF.Sin(angle + 0.45f));
		path.Close();

		using var paint = Fill(color);
		canvas.DrawPath(path, paint);
	}

	private static void DrawChip(SKCanvas canvas, SKRect room, MapConnection connection, Palette palette, SKFont font, SKPaint measure)
	{
		if (room.IsEmpty)
			return;

		var text = $"{MapRules.DirectionShortLabel(connection.Direction)} {connection.TargetReference}";
		var width = font.MeasureText(text, measure) + 16f;
		const float height = 19f;

		var chip = connection.Direction switch
		{
			MapDirection.North or MapDirection.Up => new SKRect(
				room.MidX - width / 2, room.Top - height - 6, room.MidX + width / 2, room.Top - 6),
			MapDirection.South or MapDirection.Down => new SKRect(
				room.MidX - width / 2, room.Bottom + 6, room.MidX + width / 2, room.Bottom + 6 + height),
			MapDirection.East => new SKRect(
				room.Right - width, room.MidY - height / 2, room.Right, room.MidY + height / 2),
			MapDirection.NorthEast => new SKRect(
				room.Right - width + 6, room.Top - height - 6, room.Right + 6, room.Top - 6),
			MapDirection.SouthEast => new SKRect(
				room.Right - width + 6, room.Bottom + 6, room.Right + 6, room.Bottom + 6 + height),
			MapDirection.SouthWest => new SKRect(
				room.Left - 6, room.Bottom + 6, room.Left + width - 6, room.Bottom + 6 + height),
			MapDirection.NorthWest => new SKRect(
				room.Left - 6, room.Top - height - 6, room.Left + width - 6, room.Top - 6),
			_ => new SKRect(room.Left, room.MidY - height / 2, room.Left + width, room.MidY + height / 2),
		};

		using var path = new SKPath();
		path.AddRoundRect(chip, 6, 6);
		using (var fill = new SKPaint { Color = palette.Chip, IsAntialias = true })
			canvas.DrawPath(path, fill);
		using (var stroke = new SKPaint
		{
			Color = palette.Link,
			IsAntialias = true,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 1f,
			// Secret exits get a dashed outline, matching the dashed room border.
			PathEffect = connection.Hidden ? SKPathEffect.CreateDash(new[] { 5f, 4f }, 0f) : null,
		})
			canvas.DrawPath(path, stroke);

		using var ink = Fill(palette.Text);
		canvas.DrawText(text, chip.MidX, chip.MidY + 3.8f, SKTextAlign.Center, font, ink);
	}

	private static void DrawRoom(
		SKCanvas canvas,
		MapRoom room,
		MapLayout layout,
		MapRoom? selectedRoom,
		Palette palette,
		SKFont titleFont,
		SKFont bodyFont,
		SKFont smallFont,
		SKFont chipFont,
		SKPaint ink,
		SKPaint muted,
		SKPaint accent,
		SKPaint measure)
	{
		var bounds = layout.BoundsOf(room);
		if (bounds.IsEmpty)
			return;

		var fillColor = room.Kind switch
		{
			RoomKind.Bridge => palette.BridgeFill,
			RoomKind.Pit => palette.PitFill,
			_ => palette.RoomFill,
		};
		var borderColor = room.Kind switch
		{
			RoomKind.Bridge => palette.BridgeBorder,
			RoomKind.Pit => palette.PitBorder,
			_ => palette.RoomBorder,
		};
		var isSelected = selectedRoom is not null && selectedRoom.Id == room.Id && selectedRoom.Layer == room.Layer;

		using var path = new SKPath();
		path.AddRoundRect(bounds, 10, 10);
		using (var fill = new SKPaint { Color = fillColor, IsAntialias = true })
			canvas.DrawPath(path, fill);
		using (var border = new SKPaint
		{
			Color = borderColor,
			IsAntialias = true,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = isSelected ? 3.5f : 1.6f,
			// Secret rooms are dashed so they stand out from ordinary ones.
			PathEffect = room.IsHidden ? SKPathEffect.CreateDash(new[] { 7f, 5f }, 0f) : null,
		})
			canvas.DrawPath(path, border);

		if (isSelected)
		{
			using var ring = new SKPaint
			{
				Color = palette.Accent,
				IsAntialias = true,
				Style = SKPaintStyle.Stroke,
				StrokeWidth = 2f,
			};
			canvas.DrawRoundRect(SKRect.Inflate(bounds, 4, 4), 13, 13, ring);
		}

		var left = bounds.Left + 14f;
		var right = bounds.Right - 14f;

		// layer-room badge
		var referenceWidth = chipFont.MeasureText(room.Reference, measure) + 12f;
		var referenceRect = new SKRect(right - referenceWidth, bounds.Top + 9f, right, bounds.Top + 25f);
		using (var badge = new SKPaint { Color = palette.Chip, IsAntialias = true })
			canvas.DrawRoundRect(referenceRect, 5, 5, badge);
		canvas.DrawText(room.Reference, referenceRect.MidX, referenceRect.MidY + 3.6f, SKTextAlign.Center, chipFont, ink);

		var titleWidth = Math.Max(60f, right - left - referenceWidth - 10f);
		var title = Ellipsize(titleFont, measure, room.Name, titleWidth);
		canvas.DrawText(title, left, bounds.Top + 25f, SKTextAlign.Left, titleFont, ink);

		var descriptionLines = WrapText(bodyFont, measure, room.Description, bounds.Width - 28f, 4);
		var lineY = bounds.Top + 45f;
		foreach (var line in descriptionLines)
		{
			canvas.DrawText(line, left, lineY, SKTextAlign.Left, bodyFont, muted);
			lineY += 15.5f;
		}

		var footerY = bounds.Bottom - 14f;
		var kindText = room.Kind switch
		{
			RoomKind.Bridge => L.F("Canvas_BridgeSpan", room.BridgeSpanLayer),
			RoomKind.Pit => L.T("RoomKind_Pit"),
			_ => L.T("RoomKind_Normal"),
		};
		if (room.IsHidden)
			kindText += " · " + L.T("Canvas_Hidden");
		canvas.DrawText(kindText, left, footerY, SKTextAlign.Left, smallFont, muted);

		if (room.Puzzles.Count > 0)
		{
			var puzzleText = room.Puzzles.Count == 1 ? L.T("Canvas_PuzzleOne") : L.F("Canvas_Puzzles", room.Puzzles.Count);
			canvas.DrawText(puzzleText, right, footerY, SKTextAlign.Right, smallFont, accent);
		}
	}

	private static SKPoint EdgePoint(SKRect rect, SKPoint towards)
	{
		var centerX = rect.MidX;
		var centerY = rect.MidY;
		var dx = towards.X - centerX;
		var dy = towards.Y - centerY;

		if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
			return new SKPoint(centerX, centerY);

		var scaleX = Math.Abs(dx) < 0.001f ? float.MaxValue : (rect.Width / 2f) / Math.Abs(dx);
		var scaleY = Math.Abs(dy) < 0.001f ? float.MaxValue : (rect.Height / 2f) / Math.Abs(dy);
		var factor = Math.Min(scaleX, scaleY);
		return new SKPoint(centerX + dx * factor, centerY + dy * factor);
	}

	private static List<string> WrapText(SKFont font, SKPaint paint, string text, float maxWidth, int maxLines)
	{
		var lines = new List<string>();
		if (string.IsNullOrWhiteSpace(text) || maxLines <= 0)
			return lines;

		var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var index = 0;

		while (index < words.Length && lines.Count < maxLines)
		{
			var builder = new StringBuilder();
			while (index < words.Length)
			{
				var candidate = builder.Length == 0 ? words[index] : builder + " " + words[index];
				if (builder.Length > 0 && font.MeasureText(candidate, paint) > maxWidth)
					break;

				builder.Clear().Append(candidate);
				index++;
			}

			if (builder.Length == 0)
				break;

			lines.Add(builder.ToString());
		}

		if (index < words.Length && lines.Count > 0)
			lines[^1] = Ellipsize(font, paint, lines[^1] + "…", maxWidth);

		return lines;
	}

	private static string Ellipsize(SKFont font, SKPaint paint, string text, float maxWidth)
	{
		if (font.MeasureText(text, paint) <= maxWidth)
			return text;

		var trimmed = text;
		while (trimmed.Length > 1 && font.MeasureText(trimmed + "…", paint) > maxWidth)
			trimmed = trimmed[..^1];

		return trimmed + "…";
	}

	private static SKPaint Fill(SKColor color) => new()
	{
		Color = color,
		IsAntialias = true,
		Style = SKPaintStyle.Fill,
	};

	private static SKTypeface CreateTypeface(SKFontStyle style)
	{
		foreach (var family in new[] { "Helvetica Neue", "Helvetica", "Arial", "Segoe UI", "Roboto", "sans-serif" })
		{
			var face = SKTypeface.FromFamilyName(family, style);
			if (face is not null)
				return face;
		}

		return SKTypeface.Default;
	}

	private readonly record struct Palette(
		SKColor Canvas,
		SKColor Grid,
		SKColor RoomFill,
		SKColor RoomBorder,
		SKColor BridgeFill,
		SKColor BridgeBorder,
		SKColor PitFill,
		SKColor PitBorder,
		SKColor Text,
		SKColor Muted,
		SKColor Accent,
		SKColor Link,
		SKColor Chip)
	{
		public static Palette Screen => new(
			new SKColor(0xF4, 0xF6, 0xFA),
			new SKColor(0xE2, 0xE8, 0xF1),
			new SKColor(0xFF, 0xFF, 0xFF),
			new SKColor(0x3B, 0x5A, 0x8C),
			new SKColor(0xE4, 0xF4, 0xF6),
			new SKColor(0x1C, 0x8A, 0x93),
			new SKColor(0xFB, 0xEA, 0xE4),
			new SKColor(0xC0, 0x4A, 0x1E),
			new SKColor(0x1C, 0x1F, 0x27),
			new SKColor(0x5A, 0x64, 0x78),
			new SKColor(0xB4, 0x7A, 0x0F),
			new SKColor(0x3B, 0x5A, 0x8C),
			new SKColor(0xED, 0xF1, 0xF7));

		public static Palette Print => new(
			SKColors.White,
			new SKColor(0xE6, 0xE9, 0xEE),
			new SKColor(0xF7, 0xF8, 0xFA),
			new SKColor(0x3B, 0x5A, 0x8C),
			new SKColor(0xE4, 0xF4, 0xF6),
			new SKColor(0x1C, 0x8A, 0x93),
			new SKColor(0xFB, 0xEA, 0xE4),
			new SKColor(0xC0, 0x4A, 0x1E),
			new SKColor(0x1A, 0x1D, 0x24),
			new SKColor(0x6B, 0x72, 0x80),
			new SKColor(0xB4, 0x7A, 0x0F),
			new SKColor(0x5B, 0x74, 0xA6),
			new SKColor(0xEE, 0xF1, 0xF6));
	}
}
