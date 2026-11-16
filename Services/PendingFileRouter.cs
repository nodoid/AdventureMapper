namespace AdventureMapper.Services;

/// <summary>
/// Hands a file passed on the command line / by double-clicking a .ama file to
/// whichever page is currently on screen. Platform entry points enqueue here.
/// </summary>
public static class PendingFileRouter
{
	private static readonly object Gate = new();
	private static readonly List<string> Pending = new();

	public static event EventHandler<string>? FileReady;

	public static void Enqueue(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return;

		lock (Gate)
			Pending.Add(path);

		FileReady?.Invoke(null, path);
	}

	/// <summary>Returns the next file waiting to be opened, if any.</summary>
	public static string? Dequeue()
	{
		lock (Gate)
		{
			if (Pending.Count == 0)
				return null;

			var path = Pending[0];
			Pending.RemoveAt(0);
			return path;
		}
	}
}
