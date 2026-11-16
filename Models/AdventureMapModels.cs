using AdventureMapper.Resources.Strings;
using System.Text.Json.Serialization;

namespace AdventureMapper.Models;

public static class MapConstants
{
	/// <summary>Marker written into every .ama file.</summary>
	public const string FormatId = "adventure-mapper";

	public const int FormatVersion = 1;

	/// <summary>Default file extension for adventure maps.</summary>
	public const string FileExtension = ".ama";

	/// <summary>Uniform type identifier / MIME type registered for .ama files.</summary>
	public const string MimeType = "application/vnd.allthejohnsons.adventuremapper+json";

	public const string TypeIdentifier = "uk.co.allthejohnsons.advmapper.ama";

	/// <summary>Room descriptions are short by design.</summary>
	public const int MaxDescriptionLength = 127;

	/// <summary>Canvas grid step: room width plus gap (250 + 34) and height plus gap (150 + 30).</summary>
	public const double GridCellWidth = 284;

	public const double GridCellHeight = 180;
}

public enum MapDirection
{
	North,
	NorthEast,
	East,
	SouthEast,
	South,
	SouthWest,
	West,
	NorthWest,
	Up,
	Down,
}

public enum RoomKind
{
	Normal,
	Bridge,
	Pit,
}

public enum PuzzleKind
{
	Lock,
	Riddle,
	Mechanism,
	Trap,
	Item,
	Other,
}

/// <summary>
/// A room is addressed as "layer-room" (for example "0-4" or "-1-2").
/// </summary>
public readonly record struct RoomReference(int Layer, int Room)
{
	public override string ToString() => $"{Layer}-{Room}";

	public static bool TryParse(string? text, out RoomReference reference)
	{
		reference = default;
		if (string.IsNullOrWhiteSpace(text))
			return false;

		var trimmed = text.Trim();
		var separator = trimmed.LastIndexOf('-');
		if (separator <= 0 || separator == trimmed.Length - 1)
			return false;

		if (!int.TryParse(trimmed[..separator], out var layer))
			return false;
		if (!int.TryParse(trimmed[(separator + 1)..], out var room))
			return false;
		if (room <= 0)
			return false;

		reference = new RoomReference(layer, room);
		return true;
	}
}

public sealed class MapLayer
{
	public int Number { get; set; }

	public string Name { get; set; } = string.Empty;

	[JsonIgnore]
	public string Display => L.F("Layer_Display", Number, Name);
}

/// <summary>
/// A one-way link from a room to another room. The destination is stored as
/// layer-room, matching the notation used on the map.
/// </summary>
public sealed class MapConnection
{
	public MapDirection Direction { get; set; }

	public int TargetLayer { get; set; }

	public int TargetRoom { get; set; }

	/// <summary>A secret doorway: the exit stays hidden in play until it is found.</summary>
	public bool Hidden { get; set; }

	[JsonIgnore]
	public RoomReference Target => new(TargetLayer, TargetRoom);

	[JsonIgnore]
	public string TargetReference => Target.ToString();

	[JsonIgnore]
	public string Display => $"{MapRules.DirectionLabel(Direction)} → {TargetReference}";
}

public sealed class MapPuzzle
{
	public int Id { get; set; }

	public string Title { get; set; } = L.T("Puzzle_NewDefault");

	public PuzzleKind Kind { get; set; }

	public string Prompt { get; set; } = string.Empty;

	public string Solution { get; set; } = string.Empty;

	public string Reward { get; set; } = string.Empty;
}

public sealed class MapRoom
{
	private string _description = string.Empty;

	public int Id { get; set; }

	/// <summary>Floor number: 0 is the ground, positive numbers are higher, negative numbers are lower.</summary>
	public int Layer { get; set; }

	/// <summary>Grid column on its layer (east/west).</summary>
	public int Column { get; set; }

	/// <summary>Grid row on its layer (north/south, increasing southwards).</summary>
	public int Row { get; set; }

	/// <summary>Free placement on the canvas in points, added to the grid position when drawing.</summary>
	public double OffsetX { get; set; }

	/// <summary>Free placement on the canvas in points, added to the grid position when drawing.</summary>
	public double OffsetY { get; set; }

	public string Name { get; set; } = L.T("Room_DefaultName");

	public RoomKind Kind { get; set; } = RoomKind.Normal;

	/// <summary>Hidden rooms are secret: they are drawn with a dashed border and are not revealed in play.</summary>
	public bool IsHidden { get; set; }

	/// <summary>For a bridge: the layer it spans (normally the bridge's own layer, the ground).</summary>
	public int BridgeSpanLayer { get; set; }

	/// <summary>For a bridge: the room underneath the walkway.</summary>
	public int? BridgeSpanRoomId { get; set; }

	public string Description
	{
		get => _description;
		set => _description = Truncate(value);
	}

	public List<MapConnection> Connections { get; set; } = new();

	public List<MapPuzzle> Puzzles { get; set; } = new();

	/// <summary>Address of this room in layer-room notation.</summary>
	[JsonIgnore]
	public string Reference => $"{Layer}-{Id}";

	[JsonIgnore]
	public string Display => $"{Reference} {Name}";

	/// <summary>The layer a player stands on when they enter the room (bridges raise them one floor).</summary>
	[JsonIgnore]
	public int WalkwayLayer => Kind == RoomKind.Bridge ? Layer + 1 : Layer;

	public static string Truncate(string? value)
	{
		if (string.IsNullOrEmpty(value))
			return string.Empty;
		return value.Length <= MapConstants.MaxDescriptionLength
			? value
			: value[..MapConstants.MaxDescriptionLength];
	}

	public MapConnection? FindConnection(MapDirection direction) =>
		Connections.FirstOrDefault(c => c.Direction == direction);

	public override string ToString() => $"{Reference} {Name}";
}

/// <summary>
/// Root object serialized into a .ama file. The file is plain JSON so it can be
/// opened, diffed and generated by other tools.
/// </summary>
public sealed class AdventureMapDocument
{
	public string Format { get; set; } = MapConstants.FormatId;

	public int FormatVersion { get; set; } = MapConstants.FormatVersion;

	public string Title { get; set; } = "Untitled adventure";

	public string Author { get; set; } = string.Empty;

	public string Notes { get; set; } = string.Empty;

	public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

	public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

	public int NextRoomId { get; set; } = 1;

	public List<MapLayer> Layers { get; set; } = new();

	public List<MapRoom> Rooms { get; set; } = new();

	[JsonIgnore]
	public IEnumerable<MapRoom> SortedRooms =>
		Rooms.OrderBy(r => r.Layer).ThenBy(r => r.Row).ThenBy(r => r.Column);

	public static AdventureMapDocument CreateNew(string? title = null)
	{
		var document = new AdventureMapDocument { Title = title ?? L.T("Default_Untitled") };
		document.Layers.Add(new MapLayer { Number = 0, Name = MapRules.DefaultLayerName(0) });

		var entrance = document.AddRoom(0, 0, 0, L.T("Default_Entrance"));
		entrance.Description = L.T("Default_EntranceDescription");
		return document;
	}

	public MapLayer GetOrCreateLayer(int number, string? name = null)
	{
		var layer = Layers.FirstOrDefault(l => l.Number == number);
		if (layer is not null)
			return layer;

		layer = new MapLayer { Number = number, Name = name ?? MapRules.DefaultLayerName(number) };
		Layers.Add(layer);
		Layers.Sort((a, b) => a.Number.CompareTo(b.Number));
		return layer;
	}

	public MapRoom? FindRoom(int layer, int roomId) =>
		Rooms.FirstOrDefault(r => r.Layer == layer && r.Id == roomId);

	public MapRoom? FindRoom(RoomReference reference) =>
		FindRoom(reference.Layer, reference.Room);

	public IEnumerable<MapRoom> RoomsOn(int layer) => Rooms.Where(r => r.Layer == layer);

	public MapRoom AddRoom(int layer, int column, int row, string? name = null)
	{
		var id = NextRoomId++;
		var room = new MapRoom
		{
			Id = id,
			Layer = layer,
			Column = column,
			Row = row,
			Name = string.IsNullOrWhiteSpace(name) ? L.F("Default_RoomName", id) : name,
		};

		Rooms.Add(room);
		GetOrCreateLayer(layer);
		return room;
	}

	public void RemoveRoom(MapRoom room)
	{
		Rooms.Remove(room);

		foreach (var other in Rooms)
			other.Connections.RemoveAll(c => c.TargetLayer == room.Layer && c.TargetRoom == room.Id);

		PruneEmptyLayers();
	}

	/// <summary>Drops layers that no room lives on (the ground floor is always kept).</summary>
	public void PruneEmptyLayers()
	{
		var used = Rooms.Select(r => r.Layer).ToHashSet();
		Layers.RemoveAll(l => l.Number != 0 && !used.Contains(l.Number));
	}

	/// <summary>Adds (or replaces) a connection from <paramref name="from"/> to <paramref name="to"/>.</summary>
	public MapConnection Connect(MapRoom from, MapDirection direction, MapRoom to, bool replaceExisting = true)
	{
		if (replaceExisting)
			from.Connections.RemoveAll(c => c.Direction == direction);

		var connection = new MapConnection
		{
			Direction = direction,
			TargetLayer = to.Layer,
			TargetRoom = to.Id,
		};

		from.Connections.Add(connection);
		return connection;
	}

	public bool IsCellFree(int layer, int column, int row) =>
		!Rooms.Any(r => r.Layer == layer && r.Column == column && r.Row == row);

	public void Touch() => ModifiedUtc = DateTime.UtcNow;
}

public sealed class MapFormatException : Exception
{
	public MapFormatException(string message) : base(message)
	{
	}

	public MapFormatException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
