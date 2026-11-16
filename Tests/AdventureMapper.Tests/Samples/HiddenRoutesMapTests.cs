using AdventureMapper.Models;
using AdventureMapper.Services;
using AdventureMapper.Tests.Support;

namespace AdventureMapper.Tests.Samples;

/// <summary>
/// Guards the checked-in hidden-routes sample: two secret rooms, four hidden entrances and
/// two one-way routes. It is the file used to check the Studio import of secrets.
/// </summary>
public class HiddenRoutesMapTests
{
	private static AdventureMapDocument LoadSample() =>
		MapSerializer.LoadFromFile(RepoFiles.AppPath("Samples", "hidden-routes-map.ama"));

	[Fact]
	public void Sample_HasTwelveRoomsIncludingTwoHiddenOnes()
	{
		var document = LoadSample();

		Assert.Equal("Hidden Routes Test", document.Title);
		Assert.Equal(12, document.Rooms.Count);
		Assert.Equal(new[] { "0-11", "-1-12" }, document.Rooms.Where(room => room.IsHidden).Select(room => room.Reference));
		Assert.Equal(new[] { -1, 0, 1 }, document.Layers.Select(layer => layer.Number));
	}

	[Fact]
	public void Sample_HasFourHiddenEntrances()
	{
		var document = LoadSample();
		var hidden = document.Rooms
			.SelectMany(room => room.Connections)
			.Where(connection => connection.Hidden)
			.ToList();

		Assert.Equal(4, hidden.Count);
		Assert.All(document.Rooms.Where(room => room.IsHidden), room =>
			Assert.Contains(hidden, connection => connection.Target == new RoomReference(room.Layer, room.Id)));
	}

	[Fact]
	public void Sample_HasThreeOneWayRoutes()
	{
		var document = LoadSample();

		var oneWay = document.Rooms
			.SelectMany(room => room.Connections.Select(connection => (Room: room, Connection: connection)))
			.Where(entry =>
			{
				var target = document.FindRoom(entry.Connection.Target);
				return target is null || !target.Connections.Any(back =>
					back.Direction == MapRules.Opposite(entry.Connection.Direction) &&
					back.TargetLayer == entry.Room.Layer &&
					back.TargetRoom == entry.Room.Id);
			})
			.Select(entry => $"{entry.Room.Reference} {entry.Connection.Direction}->{entry.Connection.TargetReference}")
			.ToList();

		Assert.Equal(new[] { "0-5 West->0-6", "0-8 Up->1-10", "1-10 Down->0-5" }, oneWay);
	}

	[Fact]
	public void Sample_ValidatesWithoutWarnings()
	{
		Assert.Empty(MapRules.Validate(LoadSample()));
	}

	[Fact]
	public void Sample_RoundTripsTheHiddenFlags()
	{
		var document = LoadSample();

		var loaded = MapSerializer.Deserialize(MapSerializer.Serialize(document));

		Assert.Equal(
			document.Rooms.Where(room => room.IsHidden).Select(room => room.Reference),
			loaded.Rooms.Where(room => room.IsHidden).Select(room => room.Reference));
		Assert.Equal(
			document.Rooms.SelectMany(room => room.Connections).Count(connection => connection.Hidden),
			loaded.Rooms.SelectMany(room => room.Connections).Count(connection => connection.Hidden));
	}
}
