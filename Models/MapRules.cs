using AdventureMapper.Resources.Strings;
namespace AdventureMapper.Models;

public enum MapWarningSeverity
{
	Info,
	Warning,
	Error,
}

public sealed record MapWarning(string Message, MapWarningSeverity Severity);

/// <summary>
/// The layer model:
///  * layer 0 is the ground; positive layers are above it, negative layers are below it;
///  * Up and Down move one layer number at a time;
///  * a bridge sits on the layer it spans and its walkway is one layer higher, so stepping
///    onto a bridge takes you up a layer and stepping down returns you to the spanned layer
///    (the ground by default) unless the room below is a pit, in which case you fall.
/// </summary>
public static class MapRules
{
	public static string DefaultLayerName(int number) => number switch
	{
		0 => L.T("Layer_Ground"),
		> 0 => L.F("Layer_Upper", number),
		_ => L.F("Layer_Lower", -number),
	};

	public static MapDirection Opposite(MapDirection direction) => direction switch
	{
		MapDirection.North => MapDirection.South,
		MapDirection.NorthEast => MapDirection.SouthWest,
		MapDirection.East => MapDirection.West,
		MapDirection.SouthEast => MapDirection.NorthWest,
		MapDirection.South => MapDirection.North,
		MapDirection.SouthWest => MapDirection.NorthEast,
		MapDirection.West => MapDirection.East,
		MapDirection.NorthWest => MapDirection.SouthEast,
		MapDirection.Up => MapDirection.Down,
		_ => MapDirection.Up,
	};

	/// <summary>Grid step for a direction. Up/Down do not move on the layer grid.</summary>
	public static (int Column, int Row) Offset(MapDirection direction) => direction switch
	{
		MapDirection.North => (0, -1),
		MapDirection.NorthEast => (1, -1),
		MapDirection.East => (1, 0),
		MapDirection.SouthEast => (1, 1),
		MapDirection.South => (0, 1),
		MapDirection.SouthWest => (-1, 1),
		MapDirection.West => (-1, 0),
		MapDirection.NorthWest => (-1, -1),
		_ => (0, 0),
	};

	public static int LayerStep(MapDirection direction) => direction switch
	{
		MapDirection.Up => 1,
		MapDirection.Down => -1,
		_ => 0,
	};

	/// <summary>True for the eight compass directions (everything except Up and Down).</summary>
	public static bool IsCompass(MapDirection direction) => direction is
		MapDirection.North or MapDirection.NorthEast or MapDirection.East or MapDirection.SouthEast or
		MapDirection.South or MapDirection.SouthWest or MapDirection.West or MapDirection.NorthWest;

	/// <summary>Readable name used by the menus, pickers and connection lists.</summary>
	public static string DirectionLabel(MapDirection direction) => L.T($"Direction_{direction}");

	/// <summary>Short compass code used on the map chips.</summary>
	public static string DirectionShortLabel(MapDirection direction) => direction switch
	{
		MapDirection.North => L.T("DirectionShort_N"),
		MapDirection.NorthEast => L.T("DirectionShort_NE"),
		MapDirection.East => L.T("DirectionShort_E"),
		MapDirection.SouthEast => L.T("DirectionShort_SE"),
		MapDirection.South => L.T("DirectionShort_S"),
		MapDirection.SouthWest => L.T("DirectionShort_SW"),
		MapDirection.West => L.T("DirectionShort_W"),
		MapDirection.NorthWest => L.T("DirectionShort_NW"),
		MapDirection.Up => L.T("DirectionShort_Up"),
		_ => L.T("DirectionShort_Down"),
	};

	/// <summary>
	/// The layer a Down move leaves a room for: the layer below, except on a bridge,
	/// which drops back to the layer it spans (the ground unless it spans a pit).
	/// </summary>
	public static int DownTargetLayer(MapRoom room) =>
		room.Kind == RoomKind.Bridge ? room.BridgeSpanLayer : room.Layer - 1;

	public static IReadOnlyList<MapWarning> Validate(AdventureMapDocument document)
	{
		var warnings = new List<MapWarning>();

		if (document.Rooms.Count == 0)
			warnings.Add(new MapWarning(L.T("Warn_NoRooms"), MapWarningSeverity.Info));

		var seenIds = new HashSet<int>();
		foreach (var room in document.Rooms)
		{
			if (!seenIds.Add(room.Id))
				warnings.Add(new MapWarning(L.F("Warn_DuplicateId", room.Id), MapWarningSeverity.Error));

			if (room.Description.Length > MapConstants.MaxDescriptionLength)
				warnings.Add(new MapWarning(L.F("Warn_DescriptionTooLong", room.Name, MapConstants.MaxDescriptionLength), MapWarningSeverity.Error));

			if (!document.Layers.Any(l => l.Number == room.Layer))
				warnings.Add(new MapWarning(L.F("Warn_UndefinedLayer", room.Reference, room.Layer), MapWarningSeverity.Error));

			if (room.Connections.Count == 0)
				warnings.Add(new MapWarning(L.F("Warn_NoConnections", room.Name, room.Reference), MapWarningSeverity.Info));

			var directions = new HashSet<MapDirection>();
			foreach (var connection in room.Connections)
			{
				if (!directions.Add(connection.Direction))
					warnings.Add(new MapWarning(L.F("Warn_DuplicateDirection", room.Name, room.Reference, DirectionLabel(connection.Direction)), MapWarningSeverity.Error));

				var target = document.FindRoom(connection.Target);
				if (target is null)
				{
					warnings.Add(new MapWarning(L.F("Warn_MissingTarget", room.Reference, DirectionLabel(connection.Direction), connection.TargetReference), MapWarningSeverity.Error));
					continue;
				}

				switch (connection.Direction)
				{
					case MapDirection.Up when connection.TargetLayer != room.Layer + 1:
						warnings.Add(new MapWarning(L.F("Warn_UpLayer", room.Reference, room.Layer + 1, connection.TargetLayer), MapWarningSeverity.Warning));
						break;
					case MapDirection.Down when room.Kind == RoomKind.Bridge && connection.TargetLayer != room.BridgeSpanLayer:
						warnings.Add(new MapWarning(L.F("Warn_DownBridge", room.Reference, room.BridgeSpanLayer, connection.TargetLayer), MapWarningSeverity.Warning));
						break;
					case MapDirection.Down when room.Kind != RoomKind.Bridge && connection.TargetLayer != room.Layer - 1:
						warnings.Add(new MapWarning(L.F("Warn_DownLayer", room.Reference, room.Layer - 1, connection.TargetLayer), MapWarningSeverity.Warning));
						break;
					case MapDirection.North or MapDirection.NorthEast or MapDirection.East or MapDirection.SouthEast or
						MapDirection.South or MapDirection.SouthWest or MapDirection.West or MapDirection.NorthWest
						when connection.TargetLayer != room.Layer:
						warnings.Add(new MapWarning(L.F("Warn_SameLayer", DirectionLabel(connection.Direction), room.Reference, room.Layer), MapWarningSeverity.Info));
						break;
				}
			}

			if (room.Kind == RoomKind.Bridge)
			{
				if (room.BridgeSpanLayer > room.Layer)
					warnings.Add(new MapWarning(L.F("Warn_BridgeSpanAbove", room.Reference, room.BridgeSpanLayer), MapWarningSeverity.Error));

				if (room.BridgeSpanRoomId is int spanRoomId)
				{
					var beneath = document.FindRoom(room.BridgeSpanLayer, spanRoomId);
					if (beneath is null)
						warnings.Add(new MapWarning(L.F("Warn_BridgeMissing", room.Reference, $"{room.BridgeSpanLayer}-{spanRoomId}"), MapWarningSeverity.Warning));
					else if (beneath.Kind == RoomKind.Pit)
						warnings.Add(new MapWarning(L.F("Warn_BridgeOverPit", room.Reference, beneath.Reference), MapWarningSeverity.Warning));
				}

				warnings.Add(new MapWarning(L.F("Warn_BridgeRaises", room.Reference, room.Layer + 1), MapWarningSeverity.Info));
			}

			if (room.Kind == RoomKind.Pit)
				warnings.Add(new MapWarning(L.F("Warn_Pit", room.Reference), MapWarningSeverity.Info));
		}

		return warnings;
	}
}
