using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using AdventureMapper.Rendering;
using SkiaSharp;

namespace AdventureMapper.Services;

/// <summary>
/// The generator behind the JPEG and PDF exports: both formats are produced from the
/// same SkiaSharp rendering used on screen.
/// </summary>
public sealed class ExportService
{
	private const float PageWidth = 1190.55f;
	private const float PageHeight = 841.89f;
	private const float PageMargin = 42f;

	private readonly MapRenderer _renderer = new();

	public byte[] RenderJpeg(AdventureMapDocument document, int quality = 92)
	{
		var sheet = MapSheet.Build(document);
		sheet.FitTo(9000f, 9000f);

		var width = Math.Max(1, (int)MathF.Ceiling(sheet.Width * sheet.Scale));
		var height = Math.Max(1, (int)MathF.Ceiling(sheet.Height * sheet.Scale));

		using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
		if (surface is null)
			throw new InvalidOperationException("Could not create the image surface for the export.");

		var canvas = surface.Canvas;
		canvas.Clear(SKColors.White);
		canvas.Scale(sheet.Scale);
		_renderer.DrawSheet(canvas, document, sheet, print: true);

		using var image = surface.Snapshot();
		using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality)
			?? throw new InvalidOperationException("Could not encode the JPEG image.");
		return data.ToArray();
	}

	public byte[] RenderPdf(AdventureMapDocument document)
	{
		using var stream = new MemoryStream();
		var now = DateTime.Now;
		var metadata = new SKDocumentPdfMetadata
		{
			Title = document.Title,
			Author = string.IsNullOrWhiteSpace(document.Author) ? "Adventure Mapper" : document.Author,
			Creator = "Adventure Mapper",
			Producer = "Adventure Mapper",
			Subject = "Adventure map",
			Creation = now,
			Modified = now,
		};

		using var pdf = SKDocument.CreatePdf(stream, metadata)
			?? throw new InvalidOperationException("Could not create the PDF document.");

		DrawMapPage(pdf, document);
		WriteCataloguePages(pdf, document);
		pdf.Close();
		return stream.ToArray();
	}

	private void DrawMapPage(SKDocument pdf, AdventureMapDocument document)
	{
		var sheet = MapSheet.Build(document);
		var contentWidth = PageWidth - PageMargin * 2;
		var contentHeight = PageHeight - PageMargin * 2;
		sheet.FitTo(contentWidth, contentHeight);

		var canvas = pdf.BeginPage(PageWidth, PageHeight);
		canvas.Clear(SKColors.White);
		canvas.Save();
		canvas.Translate(
			PageMargin + (contentWidth - sheet.Width * sheet.Scale) / 2f,
			PageMargin + (contentHeight - sheet.Height * sheet.Scale) / 2f);
		canvas.Scale(sheet.Scale);
		_renderer.DrawSheet(canvas, document, sheet, print: true);
		canvas.Restore();
		pdf.EndPage();
	}

	private void WriteCataloguePages(SKDocument pdf, AdventureMapDocument document)
	{
		using var headingFont = new SKFont(MapRenderer.TypefaceFor(SKFontStyle.Bold), 16f);
		using var bodyFont = new SKFont(MapRenderer.TypefaceFor(SKFontStyle.Normal), 10.5f);
		using var boldFont = new SKFont(MapRenderer.TypefaceFor(SKFontStyle.Bold), 11.5f);
		using var ink = new SKPaint { Color = new SKColor(0x1C, 0x1F, 0x27), IsAntialias = true };
		using var faint = new SKPaint { Color = new SKColor(0x66, 0x6C, 0x7A), IsAntialias = true };
		using var measure = new SKPaint { IsAntialias = true };

		var lines = BuildCatalogue(document, bodyFont, boldFont, measure, PageWidth - PageMargin * 2 - 12);
		var top = PageMargin + 70f;
		var bottom = PageHeight - PageMargin - 18f;

		var index = 0;
		var pageNumber = 1;
		while (index < lines.Count)
		{
			var canvas = pdf.BeginPage(PageWidth, PageHeight);
			canvas.Clear(SKColors.White);
			canvas.DrawText(L.T("Export_Index"), PageMargin, PageMargin + 26, SKTextAlign.Left, headingFont, ink);
			canvas.DrawText(document.Title, PageMargin, PageMargin + 46, SKTextAlign.Left, bodyFont, faint);

			var y = top;
			while (index < lines.Count && y < bottom)
			{
				var line = lines[index++];
				if (line.Text.Length > 0)
				{
					var font = line.Bold ? boldFont : bodyFont;
					canvas.DrawText(line.Text, PageMargin, y, SKTextAlign.Left, font, line.Bold ? ink : faint);
				}

				y += line.Height;
			}

			canvas.DrawText(pageNumber.ToString(), PageWidth - PageMargin, bottom + 14, SKTextAlign.Right, bodyFont, faint);
			pdf.EndPage();
			pageNumber++;
		}
	}

	private static List<CatalogueLine> BuildCatalogue(
		AdventureMapDocument document,
		SKFont bodyFont,
		SKFont boldFont,
		SKPaint measure,
		float width)
	{
		var lines = new List<CatalogueLine>();

		foreach (var layer in document.Layers.OrderByDescending(l => l.Number))
		{
			lines.Add(new CatalogueLine(L.F("Export_LayerHeading", layer.Number, layer.Name), true, 26f));

			var rooms = document.RoomsOn(layer.Number).OrderBy(r => r.Row).ThenBy(r => r.Column).ToList();
			if (rooms.Count == 0)
				lines.Add(new CatalogueLine("   " + L.T("Export_NoRooms"), false, 15f));

			foreach (var room in rooms)
			{
				var kind = room.Kind == RoomKind.Normal ? string.Empty : $"  [{L.T($"RoomKind_{room.Kind}")}]";
				lines.Add(new CatalogueLine($"{room.Reference}  {room.Name}{kind}", true, 17f));

				AppendWrapped(lines, room.Description, bodyFont, measure, width, "   ");

				var connections = room.Connections.Count == 0
					? L.T("Export_NoConnections")
					: L.F("Export_Connections", string.Join(L.T("Export_Connector"), room.Connections.Select(c => $"{MapRules.DirectionLabel(c.Direction)} → {c.TargetReference}")));
				AppendWrapped(lines, connections, bodyFont, measure, width, "   ");

				if (room.Kind == RoomKind.Bridge)
				{
					var text = room.BridgeSpanRoomId is int spanId
						? L.F("Export_BridgeOverRoom", $"{room.BridgeSpanLayer}-{spanId}", room.BridgeSpanLayer)
						: L.F("Export_BridgeOverLayer", room.BridgeSpanLayer, room.BridgeSpanLayer);
					AppendWrapped(lines, text, bodyFont, measure, width, "   ");
				}

				foreach (var puzzle in room.Puzzles)
				{
					var prompt = string.IsNullOrWhiteSpace(puzzle.Prompt) ? string.Empty : $"{puzzle.Prompt} ";
					var solution = string.IsNullOrWhiteSpace(puzzle.Solution) ? string.Empty : $"Solution: {puzzle.Solution}";
					AppendWrapped(lines, L.F("Export_Puzzle", puzzle.Title, L.T($"PuzzleKind_{puzzle.Kind}"), prompt, solution), bodyFont, measure, width, "   ");
				}

				lines.Add(new CatalogueLine(string.Empty, false, 7f));
			}

			lines.Add(new CatalogueLine(string.Empty, false, 10f));
		}

		return lines;
	}

	private static void AppendWrapped(
		List<CatalogueLine> lines,
		string text,
		SKFont font,
		SKPaint measure,
		float width,
		string prefix)
	{
		if (string.IsNullOrWhiteSpace(text))
			return;

		var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var builder = new System.Text.StringBuilder(prefix);
		var indentWidth = font.MeasureText(prefix, measure);

		foreach (var word in words)
		{
			var candidate = builder.Length == prefix.Length ? builder + word : builder + " " + word;
			if (builder.Length > prefix.Length && font.MeasureText(candidate, measure) > width)
			{
				lines.Add(new CatalogueLine(builder.ToString(), false, 15f));
				builder.Clear().Append(prefix).Append(word);
			}
			else
			{
				builder.Clear().Append(candidate);
			}
		}

		if (builder.Length > 0)
			lines.Add(new CatalogueLine(builder.ToString(), false, 15f));

		_ = indentWidth;
	}

	private sealed record CatalogueLine(string Text, bool Bold, float Height);
}
