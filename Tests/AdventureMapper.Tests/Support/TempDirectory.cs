namespace AdventureMapper.Tests.Support;

/// <summary>A unique scratch directory that is deleted when the test ends.</summary>
internal sealed class TempDirectory : IDisposable
{
	public TempDirectory()
	{
		Root = Path.Combine(Path.GetTempPath(), "advmapper-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Root);
	}

	public string Root { get; }

	public string File(string name) => Path.Combine(Root, name);

	public void Dispose()
	{
		try
		{
			if (Directory.Exists(Root))
				Directory.Delete(Root, recursive: true);
		}
		catch (IOException)
		{
			// A test may still hold a file open; the OS cleans temp directories up.
		}
		catch (UnauthorizedAccessException)
		{
		}
	}
}
