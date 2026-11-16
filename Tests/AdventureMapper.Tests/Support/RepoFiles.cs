namespace AdventureMapper.Tests.Support;

/// <summary>
/// Locates the application project on disk so the tests can inspect the real resource
/// files and the strings the UI asks for.
/// </summary>
internal static class RepoFiles
{
	public static string ProjectRoot { get; } = FindProjectRoot();

	public static string AppPath(params string[] parts) =>
		Path.Combine(new[] { ProjectRoot }.Concat(parts).ToArray());

	public static IEnumerable<string> AppSourceFiles()
	{
		var folders = new[] { "Models", "Services", "Rendering", "Views", "Resources", "Platforms" }
			.Select(folder => Path.Combine(ProjectRoot, folder))
			.Where(Directory.Exists)
			.Concat(new[] { ProjectRoot });

		foreach (var folder in folders)
		{
			foreach (var file in Directory.EnumerateFiles(folder, "*.cs").Concat(
				Directory.EnumerateFiles(folder, "*.xaml")))
			{
				if (IsBuildOutput(file))
					continue;

				yield return file;
			}

			var nested = new[] { "*.cs", "*.xaml" }
				.SelectMany(pattern => Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories))
				.Where(file => !IsBuildOutput(file));

			foreach (var file in nested)
				yield return file;
		}
	}

	private static bool IsBuildOutput(string path)
	{
		var relative = Path.GetRelativePath(ProjectRoot, path).Replace('\\', '/');
		return relative.StartsWith("bin/", StringComparison.Ordinal) ||
			relative.StartsWith("obj/", StringComparison.Ordinal) ||
			relative.Contains("/bin/", StringComparison.Ordinal) ||
			relative.Contains("/obj/", StringComparison.Ordinal) ||
			relative.StartsWith("Tests/", StringComparison.Ordinal);
	}

	private static string FindProjectRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (System.IO.File.Exists(Path.Combine(directory.FullName, "AdventureMapper.csproj")))
				return directory.FullName;

			directory = directory.Parent;
		}

		throw new InvalidOperationException(
			$"Could not find AdventureMapper.csproj above {AppContext.BaseDirectory}.");
	}
}
