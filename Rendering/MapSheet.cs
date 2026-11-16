using AdventureMapper.Models;
using SkiaSharp;

namespace AdventureMapper.Rendering;

/// <summary>
/// A printable sheet of every layer stacked from the highest floor to the lowest.
/// </summary>
public sealed class MapSheet
{
	public const float OuterMargin = 56f;
	public const float PanelPadding = 22f;
	public const float HeaderHeight = 74f;
	public const float PanelGap = 28f;
	public const float TitleBlockHeight = 104f;

	public List<SheetPanel> Panels { get; } = new();

	public float Width { get; private set; }

	public float Height { get; private set; }

	public float Scale { get; private set; } = 1f;

	public static MapSheet Build(AdventureMapDocument document)
	{
		var sheet = new MapSheet();
		var layers = document.Layers.OrderByDescending(l => l.Number).ToList();
		if (layers.Count == 0)
			layers.Add(new MapLayer { Number = 0, Name = MapRules.DefaultLayerName(0) });

		var top = OuterMargin + TitleBlockHeight;
		var widest = 1040f;

		foreach (var layer in layers)
		{
			var layout = MapLayout.Build(document, layer.Number, 1040f, 460f);
			var panelWidth = layout.Width + PanelPadding * 2;
			var panelHeight = layout.Height + HeaderHeight + PanelPadding;

			sheet.Panels.Add(new SheetPanel(layer, layout, new SKRect(OuterMargin, top, OuterMargin + panelWidth, top + panelHeight)));
			widest = Math.Max(widest, panelWidth);
			top += panelHeight + PanelGap;
		}

		sheet.Width = widest + OuterMargin * 2;
		sheet.Height = top - PanelGap + OuterMargin;
		return sheet;
	}

	/// <summary>Fits the whole sheet inside a box, never enlarging it.</summary>
	public void FitTo(float maxWidth, float maxHeight)
	{
		if (Width <= 0 || Height <= 0)
		{
			Scale = 1f;
			return;
		}

		Scale = Math.Min(1f, Math.Min(maxWidth / Width, maxHeight / Height));
		if (Scale <= 0 || float.IsNaN(Scale))
			Scale = 1f;
	}
}

public sealed record SheetPanel(MapLayer Layer, MapLayout Layout, SKRect Frame);
