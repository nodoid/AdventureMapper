using AdventureMapper.Services;

namespace AdventureMapper.Tests.Services;

public class PendingFileRouterTests
{
	public PendingFileRouterTests()
	{
		while (PendingFileRouter.Dequeue() is not null)
		{
		}
	}

	[Fact]
	public void Dequeue_ReturnsNullWhenNothingIsWaiting()
	{
		Assert.Null(PendingFileRouter.Dequeue());
	}

	[Fact]
	public void Enqueue_ThenDequeue_KeepsArrivalOrder()
	{
		PendingFileRouter.Enqueue("/maps/first.ama");
		PendingFileRouter.Enqueue("/maps/second.ama");

		Assert.Equal("/maps/first.ama", PendingFileRouter.Dequeue());
		Assert.Equal("/maps/second.ama", PendingFileRouter.Dequeue());
		Assert.Null(PendingFileRouter.Dequeue());
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Enqueue_IgnoresBlankPaths(string? path)
	{
		PendingFileRouter.Enqueue(path!);

		Assert.Null(PendingFileRouter.Dequeue());
	}

	[Fact]
	public void Enqueue_AnnouncesTheFile()
	{
		string? announced = null;
		void Handler(object? sender, string path) => announced = path;

		PendingFileRouter.FileReady += Handler;
		try
		{
			PendingFileRouter.Enqueue("/maps/opened.ama");
		}
		finally
		{
			PendingFileRouter.FileReady -= Handler;
		}

		Assert.Equal("/maps/opened.ama", announced);
		Assert.Equal("/maps/opened.ama", PendingFileRouter.Dequeue());
	}
}
