using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using AdventureMapper.Tests.Support;

namespace AdventureMapper.Tests.Models;

public class MapRulesTests
{
	public static TheoryData<MapDirection, MapDirection> OppositePairs => new()
	{
		{ MapDirection.North, MapDirection.South },
		{ MapDirection.NorthEast, MapDirection.SouthWest },
		{ MapDirection.East, MapDirection.West },
		{ MapDirection.SouthEast, MapDirection.NorthWest },
		{ MapDirection.South, MapDirection.North },
		{ MapDirection.SouthWest, MapDirection.NorthEast },
		{ MapDirection.West, MapDirection.East },
		{ MapDirection.NorthWest, MapDirection.SouthEast },
		{ MapDirection.Up, MapDirection.Down },
		{ MapDirection.Down, MapDirection.Up },
	};

	[Theory]
	[MemberData(nameof(OppositePairs))]
	public void Opposite_ReturnsTheOppositeDirection(MapDirection direction, MapDirection expected)
	{
		Assert.Equal(expected, MapRules.Opposite(direction));
	}

	[Fact]
	public void Opposite_IsItsOwnInverseForEveryDirection()
	{
		foreach (var direction in Enum.GetValues<MapDirection>())
			Assert.Equal(direction, MapRules.Opposite(MapRules.Opposite(direction)));
	}

	[Theory]
	[InlineData(MapDirection.North, 0, -1)]
	[InlineData(MapDirection.NorthEast, 1, -1)]
	[InlineData(MapDirection.East, 1, 0)]
	[InlineData(MapDirection.SouthEast, 1, 1)]
	[InlineData(MapDirection.South, 0, 1)]
	[InlineData(MapDirection.SouthWest, -1, 1)]
	[InlineData(MapDirection.West, -1, 0)]
	[InlineData(MapDirection.NorthWest, -1, -1)]
	[InlineData(MapDirection.Up, 0, 0)]
	[InlineData(MapDirection.Down, 0, 0)]
	public void Offset_StepsOnTheGridForCompassDirectionsOnly(MapDirection direction, int column, int row)
	{
		Assert.Equal((column, row), MapRules.Offset(direction));
	}

	[Theory]
	[InlineData(MapDirection.Up, 1)]
	[InlineData(MapDirection.Down, -1)]
	[InlineData(MapDirection.North, 0)]
	[InlineData(MapDirection.SouthWest, 0)]
	public void LayerStep_OnlyUpAndDownChangeLayer(MapDirection direction, int expected)
	{
		Assert.Equal(expected, MapRules.LayerStep(direction));
	}

	[Theory]
	[InlineData(MapDirection.North, true)]
	[InlineData(MapDirection.NorthEast, true)]
	[InlineData(MapDirection.East, true)]
	[InlineData(MapDirection.SouthEast, true)]
	[InlineData(MapDirection.South, true)]
	[InlineData(MapDirection.SouthWest, true)]
	[InlineData(MapDirection.West, true)]
	[InlineData(MapDirection.NorthWest, true)]
	[InlineData(MapDirection.Up, false)]
	[InlineData(MapDirection.Down, false)]
	public void IsCompass_CoversTheEightCompassPoints(MapDirection direction, bool expected)
	{
		Assert.Equal(expected, MapRules.IsCompass(direction));
	}

	[Fact]
	public void DownTargetLayer_UsesTheLayerBelowExceptOnBridges()
	{
		Assert.Equal(-1, MapRules.DownTargetLayer(new MapRoom { Layer = 0 }));
		Assert.Equal(1, MapRules.DownTargetLayer(new MapRoom { Layer = 2 }));
		Assert.Equal(-3, MapRules.DownTargetLayer(new MapRoom { Layer = -2, Kind = RoomKind.Pit }));
		Assert.Equal(0, MapRules.DownTargetLayer(new MapRoom { Layer = 2, Kind = RoomKind.Bridge, BridgeSpanLayer = 0 }));
		Assert.Equal(1, MapRules.DownTargetLayer(new MapRoom { Layer = 2, Kind = RoomKind.Bridge, BridgeSpanLayer = 1 }));
	}

	[Fact]
	public void DefaultLayerName_DescribesTheFloor()
	{
		var ground = MapRules.DefaultLayerName(0);
		var upper = MapRules.DefaultLayerName(3);
		var lower = MapRules.DefaultLayerName(-2);

		Assert.False(string.IsNullOrWhiteSpace(ground));
		Assert.Contains("3", upper);
		Assert.Contains("2", lower);
		Assert.NotEqual(ground, upper);
		Assert.NotEqual(upper, lower);
	}

	[Fact]
	public void DirectionLabels_AreDistinctForEveryDirection()
	{
		var labels = Enum.GetValues<MapDirection>()
			.Select(direction => MapRules.DirectionLabel(direction))
			.ToList();

		Assert.Equal(Enum.GetValues<MapDirection>().Length, labels.Distinct().Count());
		Assert.All(labels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
		Assert.All(Enum.GetValues<MapDirection>(), direction =>
			Assert.NotEqual($"Direction_{direction}", MapRules.DirectionLabel(direction)));
	}

	[Fact]
	public void DirectionShortLabels_AreDistinctForEveryDirection()
	{
		var labels = Enum.GetValues<MapDirection>()
			.Select(direction => MapRules.DirectionShortLabel(direction))
			.ToList();

		Assert.Equal(Enum.GetValues<MapDirection>().Length, labels.Distinct().Count());
		Assert.All(labels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
	}

	[Fact]
	public void Validate_EmptyDocument_ReportsAnInfoWarningOnly()
	{
		var warnings = MapRules.Validate(TestDocuments.NewBlank());

		Assert.NotEmpty(warnings);
		Assert.All(warnings, warning => Assert.Equal(MapWarningSeverity.Info, warning.Severity));
	}

	[Fact]
	public void Validate_NewDocument_OnlyHasInfoWarnings()
	{
		var warnings = MapRules.Validate(AdventureMapDocument.CreateNew());

		Assert.All(warnings, warning => Assert.Equal(MapWarningSeverity.Info, warning.Severity));
		Assert.Contains(warnings, warning => warning.Message.Contains("0-1"));
	}

	[Fact]
	public void Validate_FullyConnectedGroundFloor_IsClean()
	{
		var document = TestDocuments.NewBlank();
		var a = document.AddRoom(0, 0, 0, "A");
		var b = document.AddRoom(0, 1, 0, "B");
		document.Connect(a, MapDirection.East, b);
		document.Connect(b, MapDirection.West, a);

		Assert.Empty(MapRules.Validate(document));
	}

	[Fact]
	public void Validate_DuplicateRoomIds_AreErrors()
	{
		var document = TestDocuments.NewBlank();
		var first = document.AddRoom(0, 0, 0, "First");
		var second = document.AddRoom(0, 1, 0, "Second");
		second.Id = first.Id;

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning =>
			warning.Severity == MapWarningSeverity.Error && warning.Message.Contains(first.Id.ToString()));
	}

	[Fact]
	public void Validate_RoomOnAnUndefinedLayer_IsAnError()
	{
		var document = AdventureMapDocument.CreateNew();
		document.Layers.Clear();

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning => warning.Severity == MapWarningSeverity.Error);
	}

	[Fact]
	public void Validate_DuplicateDirection_IsAnError()
	{
		var document = TestDocuments.NewBlank();
		var a = document.AddRoom(0, 0, 0, "A");
		var b = document.AddRoom(0, 1, 0, "B");
		document.Connect(a, MapDirection.East, b, replaceExisting: false);
		document.Connect(a, MapDirection.East, b, replaceExisting: false);
		document.Connect(b, MapDirection.West, a);

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning => warning.Severity == MapWarningSeverity.Error);
	}

	[Fact]
	public void Validate_ConnectionToAMissingRoom_IsAnError()
	{
		var document = AdventureMapDocument.CreateNew();
		var room = document.Rooms[0];
		room.Connections.Add(new MapConnection { Direction = MapDirection.East, TargetLayer = 9, TargetRoom = 99 });

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning =>
			warning.Severity == MapWarningSeverity.Error && warning.Message.Contains("9-99"));
	}

	[Fact]
	public void Validate_UpConnectionThatSkipsALayer_IsAWarning()
	{
		var document = TestDocuments.NewBlank();
		var ground = document.AddRoom(0, 0, 0, "Ground");
		var top = document.AddRoom(2, 0, 0, "Top");
		document.Connect(ground, MapDirection.Up, top);
		document.Connect(top, MapDirection.Down, ground);

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning => warning.Severity == MapWarningSeverity.Warning);
		Assert.DoesNotContain(warnings, warning => warning.Severity == MapWarningSeverity.Error);
	}

	[Fact]
	public void Validate_UpConnectionToTheLayerAbove_IsAccepted()
	{
		var document = TestDocuments.NewBlank();
		var ground = document.AddRoom(0, 0, 0, "Ground");
		var above = document.AddRoom(1, 0, 0, "Above");
		document.Connect(ground, MapDirection.Up, above);
		document.Connect(above, MapDirection.Down, ground);

		var warnings = MapRules.Validate(document);

		Assert.DoesNotContain(warnings, warning => warning.Severity is MapWarningSeverity.Warning or MapWarningSeverity.Error);
	}

	[Fact]
	public void Validate_DownConnectionFromTheGround_IsAccepted()
	{
		var document = TestDocuments.NewBlank();
		var ground = document.AddRoom(0, 0, 0, "Ground");
		var cellar = document.AddRoom(-1, 0, 0, "Cellar");
		document.Connect(ground, MapDirection.Down, cellar);
		document.Connect(cellar, MapDirection.Up, ground);

		var warnings = MapRules.Validate(document);

		Assert.DoesNotContain(warnings, warning => warning.Severity is MapWarningSeverity.Warning or MapWarningSeverity.Error);
	}

	[Fact]
	public void Validate_DownConnectionThatStaysOnTheSameLayer_IsAWarning()
	{
		var document = TestDocuments.NewBlank();
		var ground = document.AddRoom(0, 0, 0, "Ground");
		var other = document.AddRoom(0, 1, 0, "Other");
		document.Connect(ground, MapDirection.Down, other);

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning => warning.Severity == MapWarningSeverity.Warning);
	}

	[Fact]
	public void Validate_CompassConnectionAcrossLayers_IsInfoOnly()
	{
		var document = TestDocuments.NewBlank();
		var ground = document.AddRoom(0, 0, 0, "Ground");
		var above = document.AddRoom(1, 1, 0, "Above");
		document.Connect(ground, MapDirection.East, above);

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning => warning.Severity == MapWarningSeverity.Info);
		Assert.DoesNotContain(warnings, warning => warning.Severity is MapWarningSeverity.Warning or MapWarningSeverity.Error);
	}

	[Fact]
	public void Validate_BridgeSpanningALowerLayer_IsAccepted()
	{
		var document = TestDocuments.NewBlank();
		var bridge = document.AddBridge(2, 0, 0, spanLayer: 0);

		var warnings = MapRules.Validate(document);

		Assert.DoesNotContain(warnings, warning => warning.Severity is MapWarningSeverity.Warning or MapWarningSeverity.Error);
		Assert.Contains(warnings, warning => warning.Message.Contains(bridge.Reference));
	}

	[Fact]
	public void Validate_BridgeSpanAboveItsOwnLayer_IsAnError()
	{
		var document = TestDocuments.NewBlank();
		document.AddBridge(0, 0, 0, spanLayer: 1);

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning => warning.Severity == MapWarningSeverity.Error);
	}

	[Fact]
	public void Validate_BridgeSpanningAMissingRoom_IsAWarning()
	{
		var document = TestDocuments.NewBlank();
		document.AddBridge(1, 0, 0, spanLayer: 0, spanRoomId: 42);

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning => warning.Severity == MapWarningSeverity.Warning && warning.Message.Contains("0-42"));
	}

	[Fact]
	public void Validate_BridgeOverAPit_IsAWarning()
	{
		var document = TestDocuments.NewBlank();
		var pit = document.AddPit(0, 0, 0);
		document.AddBridge(2, 0, 0, spanLayer: 0, spanRoomId: pit.Id);

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning => warning.Severity == MapWarningSeverity.Warning && warning.Message.Contains(pit.Reference));
	}

	[Fact]
	public void Validate_BridgeDownMoveToItsSpanLayer_IsAccepted()
	{
		var document = TestDocuments.NewBlank();
		var bridge = document.AddBridge(2, 0, 0, spanLayer: 0);
		var below = document.AddRoom(0, 0, 0, "Below");
		document.Connect(bridge, MapDirection.Down, below);

		var warnings = MapRules.Validate(document);

		Assert.DoesNotContain(warnings, warning => warning.Severity is MapWarningSeverity.Warning or MapWarningSeverity.Error);
	}

	[Fact]
	public void Validate_BridgeDownMoveElsewhere_IsAWarning()
	{
		var document = TestDocuments.NewBlank();
		var bridge = document.AddBridge(2, 0, 0, spanLayer: 0);
		var elsewhere = document.AddRoom(1, 0, 0, "Elsewhere");
		document.Connect(bridge, MapDirection.Down, elsewhere);

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning => warning.Severity == MapWarningSeverity.Warning);
	}

	[Fact]
	public void Validate_Pit_IsReportedAsInfo()
	{
		var document = TestDocuments.NewBlank();
		var pit = document.AddPit(0, 0, 0);

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning =>
			warning.Severity == MapWarningSeverity.Info && warning.Message.Contains(pit.Reference));
		Assert.DoesNotContain(warnings, warning => warning.Severity is MapWarningSeverity.Warning or MapWarningSeverity.Error);
	}

	[Fact]
	public void Validate_DescriptionsAtTheLimit_AreNotReported()
	{
		var document = AdventureMapDocument.CreateNew();
		document.Rooms[0].Description = new string('c', MapConstants.MaxDescriptionLength);

		var warnings = MapRules.Validate(document);

		Assert.DoesNotContain(warnings, warning => warning.Severity == MapWarningSeverity.Error);
	}

	[Fact]
	public void Validate_RoomWithoutConnections_IsInfo()
	{
		var document = AdventureMapDocument.CreateNew();

		var warnings = MapRules.Validate(document);

		Assert.Contains(warnings, warning => warning.Severity == MapWarningSeverity.Info);
		Assert.DoesNotContain(warnings, warning => warning.Severity == MapWarningSeverity.Error);
	}

	[Fact]
	public void DirectionLabels_UseAllEightCompassPoints()
	{
		var compass = Enum.GetValues<MapDirection>().Where(MapRules.IsCompass).ToList();

		Assert.Equal(8, compass.Count);
		Assert.Equal(
			new[]
			{
				MapDirection.North, MapDirection.NorthEast, MapDirection.East, MapDirection.SouthEast,
				MapDirection.South, MapDirection.SouthWest, MapDirection.West, MapDirection.NorthWest,
			},
			compass);
	}

	[Fact]
	public void DirectionLabels_AreLocalisedForEveryKey()
	{
		foreach (var direction in Enum.GetValues<MapDirection>())
		{
			Assert.NotEqual($"Direction_{direction}", MapRules.DirectionLabel(direction));
			Assert.False(string.IsNullOrWhiteSpace(MapRules.DirectionShortLabel(direction)));
		}
	}
}
