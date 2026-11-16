using AdventureMapper.Models;
using AdventureMapper.Rendering;
using AdventureMapper.Tests.Support;
using SkiaSharp;

namespace AdventureMapper.Tests.Rendering;

public class MapSheetTests
{
	[Fact]
	public void Build_StacksTheHighestLayerFirst()
	{
		var document = TestDocuments.NewBlank();
		document.AddRoom(0, 0, 0, "Ground");
		document.AddRoom(2, 0, 0, "Attic");
		document.AddRoom(-1, 0, 0, "Cellar");

		var sheet = MapSheet.Build(document);

		Assert.Equal(new[] { 2, 0, -1 }, sheet.Panels.Select(panel => panel.Layer.Number));
	}

	[Fact]
	public void Build_WithNoLayers_StillPrintsTheGroundFloor()
	{
		var sheet = MapSheet.Build(TestDocuments.NewBlank());

		var panel = Assert.Single(sheet.Panels);
		Assert.Equal(0, panel.Layer.Number);
		Assert.True(sheet.Width > 0);
		Assert.True(sheet.Height > 0);
	}

	[Fact]
	public void Build_PanelsFitTheirLayout()
	{
		var document = TestDocuments.NewBlank();
		document.AddRoom(0, 0, 0, "Ground");
		document.AddRoom(0, 3, 2, "Far");

		var sheet = MapSheet.Build(document);

		foreach (var panel in sheet.Panels)
		{
			Assert.Equal(panel.Layout.Width + MapSheet.PanelPadding * 2, panel.Frame.Width);
			Assert.Equal(panel.Layout.Height + MapSheet.HeaderHeight + MapSheet.PanelPadding, panel.Frame.Height);
			Assert.Equal(MapSheet.OuterMargin, panel.Frame.Left);
		}
	}

	[Fact]
	public void Build_StacksPanelsWithAGap()
	{
		var document = TestDocuments.NewBlank();
		document.AddRoom(0, 0, 0, "Ground");
		document.AddRoom(1, 0, 0, "Attic");

		var sheet = MapSheet.Build(document);

		Assert.Equal(2, sheet.Panels.Count);
		Assert.Equal(
			sheet.Panels[0].Frame.Bottom + MapSheet.PanelGap,
			sheet.Panels[1].Frame.Top);
		Assert.Equal(
			MapSheet.OuterMargin + MapSheet.TitleBlockHeight,
			sheet.Panels[0].Frame.Top);
	}

	[Fact]
	public void Build_SizesTheSheetToTheWidestPanel()
	{
		var document = TestDocuments.NewBlank();
		document.AddRoom(0, 0, 0, "Ground");

		var sheet = MapSheet.Build(document);

		var widest = sheet.Panels.Max(panel => panel.Frame.Width);
		Assert.Equal(widest + MapSheet.OuterMargin * 2, sheet.Width);
		Assert.Equal(sheet.Panels[^1].Frame.Bottom + MapSheet.OuterMargin, sheet.Height);
	}

	[Fact]
	public void Build_KeepsPanelsInsideTheSheet()
	{
		var document = TestDocuments.NewBlank();
		for (var layer = 0; layer < 4; layer++)
		{
			document.AddRoom(layer, 0, 0, $"Floor {layer}");
			document.AddRoom(layer, 4, 3, $"Floor {layer} far");
		}

		var sheet = MapSheet.Build(document);

		Assert.All(sheet.Panels, panel =>
		{
			Assert.True(panel.Frame.Left >= 0);
			Assert.True(panel.Frame.Top >= 0);
			Assert.True(panel.Frame.Right <= sheet.Width);
			Assert.True(panel.Frame.Bottom <= sheet.Height);
		});
	}

	[Fact]
	public void FitTo_ShrinksASheetThatIsTooLarge()
	{
		var document = TestDocuments.NewBlank();
		document.AddRoom(0, 0, 0, "Ground");
		var sheet = MapSheet.Build(document);

		sheet.FitTo(sheet.Width / 2, sheet.Height * 2);

		Assert.Equal(0.5f, sheet.Scale, 3);
	}

	[Fact]
	public void FitTo_NeverEnlarges()
	{
		var document = TestDocuments.NewBlank();
		document.AddRoom(0, 0, 0, "Ground");
		var sheet = MapSheet.Build(document);

		sheet.FitTo(sheet.Width * 4, sheet.Height * 4);

		Assert.Equal(1f, sheet.Scale);
	}

	[Fact]
	public void FitTo_UsesTheTighterDimension()
	{
		var document = TestDocuments.NewBlank();
		document.AddRoom(0, 0, 0, "Ground");
		var sheet = MapSheet.Build(document);

		sheet.FitTo(sheet.Width, sheet.Height / 4);

		Assert.Equal(0.25f, sheet.Scale, 3);
	}

	[Fact]
	public void Panel_HoldsTheLayerAndItsLayout()
	{
		var document = TestDocuments.NewBlank();
		var ground = document.AddRoom(0, 0, 0, "Ground");
		var attic = document.AddRoom(1, 0, 0, "Attic");

		var sheet = MapSheet.Build(document);
		var panel = sheet.Panels.Single(entry => entry.Layer.Number == 1);

		Assert.Equal(1, panel.Layer.Number);
		Assert.Equal(new[] { attic }, panel.Layout.Rooms);
		Assert.Equal(SKRect.Empty, panel.Layout.BoundsOf(ground));
	}
}
