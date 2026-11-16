using AdventureMapper.Models;

namespace AdventureMapper.Tests.Models;

public class RoomReferenceTests
{
	[Fact]
	public void ToString_UsesLayerDashRoomNotation()
	{
		Assert.Equal("0-4", new RoomReference(0, 4).ToString());
		Assert.Equal("3-10", new RoomReference(3, 10).ToString());
		Assert.Equal("-1-2", new RoomReference(-1, 2).ToString());
		Assert.Equal("-2-25", new RoomReference(-2, 25).ToString());
	}

	[Theory]
	[InlineData("0-1", 0, 1)]
	[InlineData("0-4", 0, 4)]
	[InlineData("3-10", 3, 10)]
	[InlineData("-1-2", -1, 2)]
	[InlineData("-2-25", -2, 25)]
	[InlineData(" 2-7 ", 2, 7)]
	public void TryParse_AcceptsLayerRoomNotation(string text, int layer, int room)
	{
		Assert.True(RoomReference.TryParse(text, out var reference));
		Assert.Equal(new RoomReference(layer, room), reference);
		Assert.Equal(text.Trim(), reference.ToString());
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("4")]
	[InlineData("0-")]
	[InlineData("-4")]
	[InlineData("a-1")]
	[InlineData("0-b")]
	[InlineData("0-0")]
	[InlineData("0--2")]
	[InlineData("0-1-2")]
	public void TryParse_RejectsMalformedText(string? text)
	{
		Assert.False(RoomReference.TryParse(text, out var reference));
		Assert.Equal(default, reference);
	}

	[Fact]
	public void TryParse_RoundTripsEveryLayerAndRoomCombination()
	{
		for (var layer = -3; layer <= 3; layer++)
		{
			for (var room = 1; room <= 5; room++)
			{
				var original = new RoomReference(layer, room);
				Assert.True(RoomReference.TryParse(original.ToString(), out var parsed));
				Assert.Equal(original, parsed);
			}
		}
	}

	[Fact]
	public void Equality_IsByLayerAndRoom()
	{
		Assert.Equal(new RoomReference(1, 2), new RoomReference(1, 2));
		Assert.NotEqual(new RoomReference(1, 2), new RoomReference(1, 3));
		Assert.NotEqual(new RoomReference(1, 2), new RoomReference(2, 2));
		Assert.Equal(new RoomReference(1, 2).GetHashCode(), new RoomReference(1, 2).GetHashCode());
	}
}
