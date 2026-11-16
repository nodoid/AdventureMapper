using AdventureMapper.Models;
using AdventureMapper.Rendering;
using AdventureMapper.Services;
using AdventureMapper.Tests.Support;
using SkiaSharp;

namespace AdventureMapper.Tests.Services;

public class ExportServiceTests
{
	private static AdventureMapDocument MapWithLayers(int roomsPerLayer)
	{
		var document = AdventureMapDocument.CreateNew("Exported map");
		document.Author = "Ada";
		for (var layer = 1; layer <= 2; layer++)
		{
			for (var index = 0; index < roomsPerLayer; index++)
			{
				var room = document.AddRoom(layer, index, index, $"Room {layer}-{index}");
				room.Description = string.Join(' ', Enumerable.Repeat("Dust and old stone.", 6));
			}
		}

		document.Rooms[0].Puzzles.Add(new MapPuzzle { Id = 1, Title = "The door" });
		document.Rooms[0].Puzzles[0].Prompt = "What opens the door?";
		document.Rooms[0].Puzzles[0].Solution = "The brass key";
		return document;
	}

	[Fact]
	public void RenderJpeg_ProducesAValidJpeg()
	{
		var bytes = new ExportService().RenderJpeg(MapWithLayers(2));

		Assert.True(bytes.Length > 1000);
		Assert.Equal(0xFF, bytes[0]);
		Assert.Equal(0xD8, bytes[1]);
		Assert.Equal(0xFF, bytes[^2]);
		Assert.Equal(0xD9, bytes[^1]);

		using var data = SKData.CreateCopy(bytes);
		using var bitmap = SKBitmap.Decode(data);
		Assert.NotNull(bitmap);
		Assert.True(bitmap.Width >= 1000);
		Assert.True(bitmap.Height >= 500);
	}

	[Fact]
	public void RenderJpeg_HandlesAnEmptyDocument()
	{
		var bytes = new ExportService().RenderJpeg(TestDocuments.NewBlank());

		Assert.NotEmpty(bytes);
		using var data = SKData.CreateCopy(bytes);
		using var bitmap = SKBitmap.Decode(data);
		Assert.NotNull(bitmap);
	}

	[Fact]
	public void RenderJpeg_HonoursTheQualitySetting()
	{
		var document = MapWithLayers(1);
		var service = new ExportService();

		var low = service.RenderJpeg(document, quality: 20);
		var high = service.RenderJpeg(document, quality: 95);

		Assert.True(high.Length > low.Length);
	}

	[Fact]
	public void RenderPdf_ProducesAPdf()
	{
		var bytes = new ExportService().RenderPdf(MapWithLayers(1));
		var text = System.Text.Encoding.ASCII.GetString(bytes);

		Assert.True(bytes.Length > 1000);
		Assert.StartsWith("%PDF", text);
		Assert.Contains("%%EOF", text.TrimEnd());
	}

	[Fact]
	public void RenderPdf_IncludesACatalogueForLargerMaps()
	{
		var service = new ExportService();

		var single = service.RenderPdf(AdventureMapDocument.CreateNew("One room"));
		var many = service.RenderPdf(MapWithLayers(6));

		Assert.True(many.Length > single.Length);
	}

	[Fact]
	public void RenderPdf_AcceptsDocumentsWithoutLayers()
	{
		var bytes = new ExportService().RenderPdf(TestDocuments.NewBlank());

		Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes));
	}

	[Fact]
	public void RenderJpeg_WorksForEveryRoomKind()
	{
		var document = AdventureMapDocument.CreateNew("Kinds");
		document.AddRoom(0, 1, 0, "Bridge").Kind = RoomKind.Bridge;
		document.AddRoom(0, 2, 0, "Pit").Kind = RoomKind.Pit;

		var bytes = new ExportService().RenderJpeg(document);

		using var data = SKData.CreateCopy(bytes);
		using var bitmap = SKBitmap.Decode(data);
		Assert.NotNull(bitmap);
	}

	[Fact]
	public void MapSheet_IncludesEveryLayer()
	{
		var document = MapWithLayers(1);

		var sheet = MapSheet.Build(document);

		Assert.Equal(document.Layers.Count, sheet.Panels.Count);
	}
}
