using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;

namespace AdventureMapper.Services;

/// <summary>Reads and writes the JSON payload of a .ama file.</summary>
public static class MapSerializer
{
	public static JsonSerializerOptions Options { get; } = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
	};

	public static string Serialize(AdventureMapDocument document) =>
		JsonSerializer.Serialize(document, Options);

	public static AdventureMapDocument Deserialize(string json)
	{
		AdventureMapDocument? document;
		try
		{
			document = JsonSerializer.Deserialize<AdventureMapDocument>(json, Options);
		}
		catch (JsonException ex)
		{
			throw new MapFormatException(L.T("File_NotValid"), ex);
		}

		if (document is null)
			throw new MapFormatException(L.T("File_Empty"));

		if (!string.IsNullOrWhiteSpace(document.Format) &&
			!string.Equals(document.Format, MapConstants.FormatId, StringComparison.OrdinalIgnoreCase))
		{
			throw new MapFormatException(L.F("File_UnsupportedFormat", document.Format));
		}

		Normalize(document);
		return document;
	}

	public static void SaveToFile(AdventureMapDocument document, string path)
	{
		document.Touch();

		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		// Write next to the target first so a failed write cannot destroy the existing map.
		var temporary = path + ".tmp";
		File.WriteAllText(temporary, Serialize(document));
		File.Move(temporary, path, overwrite: true);
	}

	public static AdventureMapDocument LoadFromFile(string path) =>
		Deserialize(File.ReadAllText(path));

	private static void Normalize(AdventureMapDocument document)
	{
		document.Rooms ??= new List<MapRoom>();
		document.Layers ??= new List<MapLayer>();
		document.NextRoomId = Math.Max(document.NextRoomId, 1);

		foreach (var layer in document.Layers)
		{
			layer.Name = string.IsNullOrWhiteSpace(layer.Name)
				? MapRules.DefaultLayerName(layer.Number)
				: layer.Name;
		}

		document.GetOrCreateLayer(0);

		foreach (var room in document.Rooms)
		{
			room.Description = MapRoom.Truncate(room.Description);
			room.Connections ??= new List<MapConnection>();
			room.Puzzles ??= new List<MapPuzzle>();
			room.Name = string.IsNullOrWhiteSpace(room.Name) ? L.F("Default_RoomName", room.Id) : room.Name;
			room.Connections.RemoveAll(c => c.TargetRoom <= 0);
			document.GetOrCreateLayer(room.Layer);
		}

		var highestId = document.Rooms.Count == 0 ? 0 : document.Rooms.Max(r => r.Id);
		if (document.NextRoomId <= highestId)
			document.NextRoomId = highestId + 1;
	}
}
