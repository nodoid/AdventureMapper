using AdventureMapper.Models;
using AdventureMapper.Services;
using AdventureMapper.Tests.Support;

namespace AdventureMapper.Tests.Samples;

/// <summary>
/// Guards the checked-in ten-room sample map: it must load, be fully connected and pass
/// validation, because it is the file used to check the Adventure Creator Studio import.
/// </summary>
public class SampleMapTests
{
	private static AdventureMapDocument LoadSample() =>
		MapSerializer.LoadFromFile(RepoFiles.AppPath("Samples", "ten-room-map.ama"));

	[Fact]
	public void Sample_HasTenRoomsOnTwoFloors()
	{
		var document = LoadSample();

		Assert.Equal("Ten Room Test", document.Title);
		Assert.Equal(10, document.Rooms.Count);
		Assert.Equal(new[] { 0, 1 }, document.Layers.Select(layer => layer.Number));
		Assert.Equal(8, document.RoomsOn(0).Count());
		Assert.Equal(2, document.RoomsOn(1).Count());
		Assert.Equal(11, document.NextRoomId);
	}

	[Fact]
	public void Sample_IsFullyConnected()
	{
		var document = LoadSample();

		Assert.Equal(20, document.Rooms.Sum(room => room.Connections.Count));
		Assert.All(document.Rooms, room => Assert.NotEmpty(room.Connections));
		Assert.All(document.Rooms.SelectMany(room => room.Connections), connection =>
			Assert.NotNull(document.FindRoom(connection.Target)));
	}

	[Fact]
	public void Sample_HasNoValidationWarnings()
	{
		var warnings = MapRules.Validate(LoadSample());

		Assert.Empty(warnings);
	}

	[Fact]
	public void Sample_UsesCompassAndVerticalDirections()
	{
		var directions = LoadSample().Rooms
			.SelectMany(room => room.Connections)
			.Select(connection => connection.Direction)
			.ToHashSet();

		Assert.Equal(
			new[] { MapDirection.North, MapDirection.East, MapDirection.South, MapDirection.West, MapDirection.Up, MapDirection.Down },
			directions.OrderBy(direction => direction));
	}

	[Fact]
	public void Sample_IncludesPuzzlesWithPromptsAndSolutions()
	{
		var puzzles = LoadSample().Rooms.SelectMany(room => room.Puzzles).ToList();

		Assert.Equal(3, puzzles.Count);
		Assert.Equal(
			new[] { "The groaning door", "The locked cabinet", "The combination" },
			puzzles.Select(puzzle => puzzle.Title));
		Assert.Equal(3, puzzles.Select(puzzle => puzzle.Kind).Distinct().Count());
		Assert.All(puzzles, puzzle =>
		{
			Assert.False(string.IsNullOrWhiteSpace(puzzle.Prompt));
			Assert.False(string.IsNullOrWhiteSpace(puzzle.Solution));
		});
	}

	[Fact]
	public void Sample_KeepsPuzzlesOnTheirRooms()
	{
		var document = LoadSample();

		Assert.Single(document.FindRoom(0, 2)!.Puzzles);
		Assert.Single(document.FindRoom(0, 3)!.Puzzles);
		Assert.Single(document.FindRoom(0, 7)!.Puzzles);
		Assert.Equal(3, document.Rooms.Sum(room => room.Puzzles.Count));
	}

	[Fact]
	public void Sample_RoundTripsThroughTheSerializer()
	{
		var document = LoadSample();

		var loaded = MapSerializer.Deserialize(MapSerializer.Serialize(document));

		Assert.Equal(document.Rooms.Count, loaded.Rooms.Count);
		Assert.Equal(3, loaded.Rooms.Sum(room => room.Puzzles.Count));
		Assert.Equal(
			document.Rooms.SelectMany(room => room.Puzzles).Select(puzzle => puzzle.Title),
			loaded.Rooms.SelectMany(room => room.Puzzles).Select(puzzle => puzzle.Title));
	}

	[Fact]
	public void Sample_DescriptionsFitTheLimit()
	{
		Assert.All(LoadSample().Rooms, room =>
		{
			Assert.False(string.IsNullOrWhiteSpace(room.Description));
			Assert.True(room.Description.Length <= MapConstants.MaxDescriptionLength);
		});
	}
}
