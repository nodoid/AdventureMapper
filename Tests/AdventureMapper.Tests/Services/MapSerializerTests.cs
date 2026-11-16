using System.Text.Json;
using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using AdventureMapper.Services;
using AdventureMapper.Tests.Support;

namespace AdventureMapper.Tests.Services;

public class MapSerializerTests
{
	[Fact]
	public void Serialize_WritesTheDocumentEnvelopeInCamelCase()
	{
		var json = MapSerializer.Serialize(AdventureMapDocument.CreateNew("Camel"));

		Assert.Contains("\"format\": \"adventure-mapper\"", json);
		Assert.Contains("\"formatVersion\": 1", json);
		Assert.Contains("\"title\": \"Camel\"", json);
		Assert.Contains("\"nextRoomId\":", json);
		Assert.DoesNotContain("\"Title\"", json);
		Assert.DoesNotContain("\"Rooms\"", json);
	}

	[Fact]
	public void Serialize_WritesEnumValuesAsCamelCaseNames()
	{
		var document = AdventureMapDocument.CreateNew();
		var room = document.Rooms[0];
		remote(document, room);

		var json = MapSerializer.Serialize(document);

		Assert.Contains("\"direction\": \"northEast\"", json);
		Assert.Contains("\"kind\": \"normal\"", json);

		static void remote(AdventureMapDocument document, MapRoom room)
		{
			room.Kind = RoomKind.Bridge;
			room.Kind = RoomKind.Normal;
			document.Connect(room, MapDirection.NorthEast, room);
		}
	}

	[Fact]
	public void RoundTrip_PreservesTheWholeMap()
	{
		var document = AdventureMapDocument.CreateNew("Round trip");
		document.Author = "Ada";
		document.Notes = "Notes";
		var entrance = document.Rooms[0];
		var bridge = document.AddBridge(1, 2, 3, spanLayer: 0);
		bridge.Description = "A plank walkway.";
		var pit = document.AddPit(-1, 0, 0);
		document.Connect(entrance, MapDirection.Up, bridge);
		document.Connect(bridge, MapDirection.Down, entrance);
		document.Connect(entrance, MapDirection.Down, pit);
		document.Rooms[0].Puzzles.Add(new MapPuzzle { Id = 1, Title = "Trap" });

		var loaded = MapSerializer.Deserialize(MapSerializer.Serialize(document));

		Assert.Equal(document.Title, loaded.Title);
		Assert.Equal(document.Author, loaded.Author);
		Assert.Equal(document.Notes, loaded.Notes);
		Assert.Equal(document.NextRoomId, loaded.NextRoomId);
		Assert.Equal(document.Rooms.Count, loaded.Rooms.Count);
		Assert.Equal(document.Layers.Select(layer => layer.Number), loaded.Layers.Select(layer => layer.Number));

		var loadedEntrance = loaded.FindRoom(entrance.Layer, entrance.Id)!;
		Assert.Equal(entrance.Name, loadedEntrance.Name);
		Assert.Equal(2, loadedEntrance.Connections.Count);

		var loadedBridge = loaded.FindRoom(bridge.Layer, bridge.Id)!;
		Assert.Equal(RoomKind.Bridge, loadedBridge.Kind);
		Assert.Equal(0, loadedBridge.BridgeSpanLayer);
		Assert.Equal("A plank walkway.", loadedBridge.Description);

		var loadedPit = loaded.FindRoom(pit.Layer, pit.Id)!;
		Assert.Equal(RoomKind.Pit, loadedPit.Kind);
		Assert.Equal(RoomKind.Normal, loadedEntrance.Kind);
	}

	[Fact]
	public void RoundTrip_PreservesPuzzles()
	{
		var document = AdventureMapDocument.CreateNew();
		document.Rooms[0].Puzzles.Add(new MapPuzzle
		{
			Id = 4,
			Title = "The iron door",
			Kind = PuzzleKind.Riddle,
			Prompt = "Speak the word",
			Solution = "Ember",
			Reward = "A key",
		});

		var loaded = MapSerializer.Deserialize(MapSerializer.Serialize(document));
		var puzzle = Assert.Single(loaded.Rooms[0].Puzzles);

		Assert.Equal(4, puzzle.Id);
		Assert.Equal("The iron door", puzzle.Title);
		Assert.Equal(PuzzleKind.Riddle, puzzle.Kind);
		Assert.Equal("Speak the word", puzzle.Prompt);
		Assert.Equal("Ember", puzzle.Solution);
		Assert.Equal("A key", puzzle.Reward);
	}

	[Fact]
	public void RoundTrip_PreservesHiddenRoomsAndSecretExits()
	{
		var document = AdventureMapDocument.CreateNew("Secrets");
		var entrance = document.Rooms[0];
		var study = document.AddRoom(0, 1, 0, "Study");
		study.IsHidden = true;
		var exit = document.Connect(entrance, MapDirection.East, study);
		exit.Hidden = true;

		var json = MapSerializer.Serialize(document);
		var loaded = MapSerializer.Deserialize(json);

		Assert.Contains("\"isHidden\": true", json);
		Assert.Contains("\"hidden\": true", json);
		Assert.True(loaded.FindRoom(0, study.Id)!.IsHidden);
		Assert.True(loaded.FindRoom(0, entrance.Id)!.Connections.Single().Hidden);
	}

	[Fact]
	public void RoundTrip_PreservesFreePlacement()
	{
		var document = AdventureMapDocument.CreateNew();
		document.Rooms[0].OffsetX = 42.5;
		document.Rooms[0].OffsetY = -18;

		var loaded = MapSerializer.Deserialize(MapSerializer.Serialize(document)).Rooms[0];

		Assert.Equal(42.5, loaded.OffsetX, 3);
		Assert.Equal(-18, loaded.OffsetY, 3);
	}

	[Fact]
	public void Deserialize_TreatsMissingOffsetsAsZero()
	{
		var json = """{ "format": "adventure-mapper", "rooms": [ { "id": 1 } ] }""";

		var room = Assert.Single(MapSerializer.Deserialize(json).Rooms);

		Assert.Equal(0, room.OffsetX);
		Assert.Equal(0, room.OffsetY);
	}

	[Fact]
	public void Deserialize_TreatsMissingHiddenFlagsAsFalse()
	{
		var json = """
		{ "format": "adventure-mapper", "rooms": [ { "id": 1, "connections": [ { "direction": "north", "targetRoom": 1 } ] } ] }
		""";

		var room = Assert.Single(MapSerializer.Deserialize(json).Rooms);

		Assert.False(room.IsHidden);
		Assert.False(room.Connections.Single().Hidden);
	}

	[Fact]
	public void Deserialize_AcceptsCommentsAndTrailingCommas()
	{
		var json = """
		{
			// hand written map
			"format": "adventure-mapper",
			"title": "Hand written",
			"rooms": [
				{ "id": 1, "layer": 0, "name": "Hall", },
			],
		}
		""";

		var document = MapSerializer.Deserialize(json);

		Assert.Equal("Hand written", document.Title);
		Assert.Equal("Hall", document.Rooms[0].Name);
	}

	[Fact]
	public void Deserialize_IgnoresPropertyNameCase()
	{
		var json = """{ "Format": "adventure-mapper", "Title": "Shouty", "Rooms": [] }""";

		Assert.Equal("Shouty", MapSerializer.Deserialize(json).Title);
	}

	[Fact]
	public void Deserialize_AcceptsFilesWithoutAFormatMarker()
	{
		var document = MapSerializer.Deserialize("""{ "title": "Old map" }""");

		Assert.Equal("Old map", document.Title);
	}

	[Fact]
	public void Deserialize_RejectsAnotherApplicationFormat()
	{
		var exception = Assert.Throws<MapFormatException>(
			() => MapSerializer.Deserialize("""{ "format": "some-other-editor", "title": "Nope" }"""));

		Assert.Contains("some-other-editor", exception.Message);
	}

	[Fact]
	public void Deserialize_RejectsMalformedJson()
	{
		var exception = Assert.Throws<MapFormatException>(() => MapSerializer.Deserialize("{ not json"));

		Assert.IsType<JsonException>(exception.InnerException);
	}

	[Fact]
	public void Deserialize_RejectsAnEmptyPayload()
	{
		Assert.Throws<MapFormatException>(() => MapSerializer.Deserialize(string.Empty));
		Assert.Throws<MapFormatException>(() => MapSerializer.Deserialize("null"));
	}

	[Fact]
	public void Deserialize_RejectsUnknownEnumValues()
	{
		var json = """
		{ "format": "adventure-mapper", "rooms": [ { "id": 1, "connections": [ { "direction": "sideways", "targetRoom": 1 } ] } ] }
		""";

		Assert.Throws<MapFormatException>(() => MapSerializer.Deserialize(json));
	}

	[Fact]
	public void Deserialize_AddsTheGroundLayerAndMissingRoomLayers()
	{
		var json = """
		{
			"format": "adventure-mapper",
			"title": "Sparse",
			"rooms": [ { "id": 5, "layer": 2, "name": "Attic" } ]
		}
		""";

		var document = MapSerializer.Deserialize(json);

		Assert.Contains(document.Layers, layer => layer.Number == 0);
		Assert.Contains(document.Layers, layer => layer.Number == 2);
		Assert.All(document.Layers, layer => Assert.False(string.IsNullOrWhiteSpace(layer.Name)));
	}

	[Fact]
	public void Deserialize_NamesRoomsThatLostTheirName()
	{
		var json = """{ "format": "adventure-mapper", "rooms": [ { "id": 7, "name": "  " } ] }""";

		var room = Assert.Single(MapSerializer.Deserialize(json).Rooms);

		Assert.False(string.IsNullOrWhiteSpace(room.Name));
		Assert.Contains("7", room.Name);
	}

	[Fact]
	public void Deserialize_TruncatesOverlongDescriptions()
	{
		var json = $$"""
		{ "format": "adventure-mapper", "rooms": [ { "id": 1, "description": "{{new string('d', 300)}}" } ] }
		""";

		var room = Assert.Single(MapSerializer.Deserialize(json).Rooms);

		Assert.Equal(MapConstants.MaxDescriptionLength, room.Description.Length);
	}

	[Fact]
	public void Deserialize_DropsConnectionsWithoutATarget()
	{
		var json = """
		{
			"format": "adventure-mapper",
			"rooms": [
				{
					"id": 1,
					"connections": [
						{ "direction": "north", "targetLayer": 0, "targetRoom": 0 },
						{ "direction": "east", "targetLayer": 0, "targetRoom": 2 }
					]
				}
			]
		}
		""";

		var room = Assert.Single(MapSerializer.Deserialize(json).Rooms);

		var connection = Assert.Single(room.Connections);
		Assert.Equal(MapDirection.East, connection.Direction);
	}

	[Fact]
	public void Deserialize_RaisesNextRoomIdAboveTheHighestRoom()
	{
		var json = """
		{ "format": "adventure-mapper", "nextRoomId": 1, "rooms": [ { "id": 9 }, { "id": 4 } ] }
		""";

		Assert.Equal(10, MapSerializer.Deserialize(json).NextRoomId);
	}

	[Fact]
	public void Deserialize_NeverLeavesNextRoomIdBelowOne()
	{
		var json = """{ "format": "adventure-mapper", "nextRoomId": -5, "rooms": [] }""";

		Assert.Equal(1, MapSerializer.Deserialize(json).NextRoomId);
	}

	[Fact]
	public void SaveToFile_WritesAtomicallyAndTouchesTheDocument()
	{
		using var temp = new TempDirectory();
		var path = temp.File(Path.Combine("nested", "map.ama"));
		var document = AdventureMapDocument.CreateNew("Saved");
		var before = document.ModifiedUtc;

		MapSerializer.SaveToFile(document, path);

		Assert.True(File.Exists(path));
		Assert.False(File.Exists(path + ".tmp"));
		Assert.True(document.ModifiedUtc >= before);
		Assert.Contains("\"Saved\"", File.ReadAllText(path));
	}

	[Fact]
	public void SaveToFile_ReplacesAnExistingMap()
	{
		using var temp = new TempDirectory();
		var path = temp.File("map.ama");

		MapSerializer.SaveToFile(AdventureMapDocument.CreateNew("First"), path);
		MapSerializer.SaveToFile(AdventureMapDocument.CreateNew("Second"), path);

		Assert.Equal("Second", MapSerializer.LoadFromFile(path).Title);
	}

	[Fact]
	public void LoadFromFile_ReadsWhatSaveToFileWrote()
	{
		using var temp = new TempDirectory();
		var path = temp.File("map.ama");
		var document = AdventureMapDocument.CreateNew("On disk");
		document.Rooms[0].Description = "A cold stone floor.";
		document.AddRoom(0, 1, 0, "Hall");

		MapSerializer.SaveToFile(document, path);
		var loaded = MapSerializer.LoadFromFile(path);

		Assert.Equal("On disk", loaded.Title);
		Assert.Equal(2, loaded.Rooms.Count);
		Assert.Equal("A cold stone floor.", loaded.Rooms[0].Description);
	}

	[Fact]
	public void LoadFromFile_ReportsMissingFiles()
	{
		using var temp = new TempDirectory();

		Assert.Throws<FileNotFoundException>(() => MapSerializer.LoadFromFile(temp.File("absent.ama")));
	}

	[Fact]
	public void Options_KeepTheFileHumanReadable()
	{
		Assert.True(MapSerializer.Options.WriteIndented);
		Assert.True(MapSerializer.Options.AllowTrailingCommas);
		Assert.True(MapSerializer.Options.PropertyNameCaseInsensitive);
		Assert.Equal(JsonCommentHandling.Skip, MapSerializer.Options.ReadCommentHandling);
	}

	[Fact]
	public void Deserialize_UsesALocalisedErrorMessage()
	{
		var exception = Assert.Throws<MapFormatException>(() => MapSerializer.Deserialize("null"));

		Assert.Equal(L.T("File_Empty"), exception.Message);
	}
}
