using AdventureMapper.Models;

namespace AdventureMapper.Tests.Support;

internal static class TestDocuments
{
	/// <summary>A document with no rooms and no layers, for tests that build their own.</summary>
	public static AdventureMapDocument NewBlank() => new() { Title = "Test map" };

	public static MapRoom AddBridge(
		this AdventureMapDocument document,
		int layer,
		int column,
		int row,
		int spanLayer,
		int? spanRoomId = null)
	{
		var room = document.AddRoom(layer, column, row, "Bridge");
		room.Kind = RoomKind.Bridge;
		room.BridgeSpanLayer = spanLayer;
		room.BridgeSpanRoomId = spanRoomId;
		return room;
	}

	public static MapRoom AddPit(this AdventureMapDocument document, int layer, int column, int row)
	{
		var room = document.AddRoom(layer, column, row, "Pit");
		room.Kind = RoomKind.Pit;
		return room;
	}
}
