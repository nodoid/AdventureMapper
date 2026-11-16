using AdventureMapper.Models;
using AdventureMapper.Services;
using AdventureMapper.Tests.Support;

namespace AdventureMapper.Tests.Services;

public class AutoSaveServiceTests : IDisposable
{
	public AutoSaveServiceTests() => Preferences.Reset();

	public void Dispose() => Preferences.Reset();

	[Fact]
	public void Defaults_OfferTheDocumentedIntervals()
	{
		Assert.Equal(60, AutoSaveService.DefaultIntervalSeconds);
		Assert.Equal(new[] { 0, 30, 60, 120, 300, 600 }, AutoSaveService.IntervalOptions);
		Assert.Equal("autosave.seconds", AutoSaveService.IntervalPreference);
	}

	[Fact]
	public void Interval_DefaultsWhenThePreferenceIsMissing()
	{
		using var service = new AutoSaveService(new MapEditorState());

		Assert.Equal(AutoSaveService.DefaultIntervalSeconds, service.IntervalSeconds);
	}

	[Fact]
	public void Interval_IsRememberedInPreferences()
	{
		using var service = new AutoSaveService(new MapEditorState());

		service.IntervalSeconds = 120;

		Assert.Equal(120, Preferences.Get(AutoSaveService.IntervalPreference, 0));
		using var second = new AutoSaveService(new MapEditorState());
		Assert.Equal(120, second.IntervalSeconds);

		service.IntervalSeconds = 0;
		Assert.Equal(0, service.IntervalSeconds);
	}

	[Fact]
	public void ZeroInterval_StopsTheTimer()
	{
		using var service = new AutoSaveService(new MapEditorState()) { IntervalSeconds = 0 };

		service.Start();
		service.Restart();
		service.Stop();

		Assert.Equal(0, service.IntervalSeconds);
	}

	[Fact]
	public void WorkingDirectory_SitsInTheDocumentsFolder()
	{
		using var service = new AutoSaveService(new MapEditorState());

		Assert.True(Path.IsPathRooted(service.WorkingDirectory));
		Assert.EndsWith("Adventure Mapper", service.WorkingDirectory);
	}

	[Theory]
	[InlineData("The Vault", "The Vault.ama")]
	[InlineData("  Spaces  ", "Spaces.ama")]
	public void GetWorkingPath_SanitisesTheTitle(string title, string expectedFile)
	{
		using var service = new AutoSaveService(new MapEditorState());

		var path = service.GetWorkingPath(title);

		Assert.Equal(service.WorkingDirectory, Path.GetDirectoryName(path));
		Assert.EndsWith(".ama", path);
		Assert.Equal(expectedFile, Path.GetFileName(path));
	}

	[Fact]
	public void GetWorkingPath_ReplacesInvalidFileNameCharacters()
	{
		using var service = new AutoSaveService(new MapEditorState());
		var invalid = Path.GetInvalidFileNameChars()[0];

		var path = service.GetWorkingPath($"bad{invalid}name");

		Assert.Equal("bad-name.ama", Path.GetFileName(path));
	}

	[Fact]
	public void GetWorkingPath_UsesTheDefaultTitleWhenBlank()
	{
		using var service = new AutoSaveService(new MapEditorState());

		var path = service.GetWorkingPath("   ");

		Assert.False(string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(path)));
		Assert.EndsWith(".ama", path);
	}

	[Fact]
	public async Task Save_WhenNothingChanged_IsSkipped()
	{
		using var temp = new TempDirectory();
		var state = new MapEditorState();
		state.Load(AdventureMapDocument.CreateNew("Clean"), temp.File("clean.ama"));
		using var service = new AutoSaveService(state);

		var saved = await service.SaveAsync(userInitiated: false);

		Assert.False(saved);
		Assert.False(File.Exists(temp.File("clean.ama")));
		Assert.Null(service.LastSavedUtc);
	}

	[Fact]
	public async Task Save_WritesTheMapAndMarksItClean()
	{
		using var temp = new TempDirectory();
		var path = temp.File("map.ama");
		var state = new MapEditorState();
		state.Load(AdventureMapDocument.CreateNew("Saved"), path);
		state.SetTitle("Saved title");
		using var service = new AutoSaveService(state);
		DateTime? announced = null;
		service.AutoSaved += (_, at) => announced = at;

		var saved = await service.SaveAsync(userInitiated: false);

		Assert.True(saved);
		Assert.True(File.Exists(path));
		Assert.False(File.Exists(path + ".tmp"));
		Assert.False(state.IsDirty);
		Assert.NotNull(service.LastSavedUtc);
		Assert.Equal(service.LastSavedUtc, announced);
		Assert.Null(service.LastError);
		Assert.Contains("map.ama", state.Status);

		var loaded = MapSerializer.LoadFromFile(path);
		Assert.Equal("Saved title", loaded.Title);
	}

	[Fact]
	public async Task Save_UserInitiatedWritesEvenWhenClean()
	{
		using var temp = new TempDirectory();
		var path = temp.File("clean.ama");
		var state = new MapEditorState();
		state.Load(AdventureMapDocument.CreateNew("Clean"), path);
		using var service = new AutoSaveService(state);

		var saved = await service.SaveAsync(userInitiated: true);

		Assert.True(saved);
		Assert.True(File.Exists(path));
	}

	[Fact]
	public async Task Save_WhenTheTargetCannotBeWritten_RecordsTheError()
	{
		using var temp = new TempDirectory();
		var blocker = temp.File("blocker");
		await File.WriteAllTextAsync(blocker, "not a directory");
		var state = new MapEditorState();
		state.Load(AdventureMapDocument.CreateNew("Blocked"), Path.Combine(blocker, "map.ama"));
		state.SetTitle("Blocked");
		using var service = new AutoSaveService(state);

		var saved = await service.SaveAsync(userInitiated: true);

		Assert.False(saved);
		Assert.False(string.IsNullOrWhiteSpace(service.LastError));
		Assert.True(state.IsDirty);
		Assert.False(string.IsNullOrWhiteSpace(state.Status));
	}

	[Fact]
	public void Dispose_IsSafeToCallTwice()
	{
		var service = new AutoSaveService(new MapEditorState());

		service.Stop();
		service.Dispose();
		service.Dispose();
	}

	[Fact]
	public void Restart_KeepsTheServiceRunning()
	{
		using var service = new AutoSaveService(new MapEditorState()) { IntervalSeconds = 30 };

		service.Restart();
		service.Stop();
		service.Start();
		service.Stop();

		Assert.Equal(30, service.IntervalSeconds);
	}
}
