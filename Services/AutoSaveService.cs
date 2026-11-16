using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;

namespace AdventureMapper.Services;

/// <summary>
/// Writes the map to disk on a timer, and on demand when the app is backgrounded.
/// The interval is remembered between launches.
/// </summary>
public sealed class AutoSaveService : IDisposable
{
	public const string IntervalPreference = "autosave.seconds";
	public const int DefaultIntervalSeconds = 60;

	/// <summary>Intervals offered in the UI: off, 30s, 1m, 2m, 5m, 10m.</summary>
	public static readonly int[] IntervalOptions = { 0, 30, 60, 120, 300, 600 };

	private static readonly SemaphoreSlim SaveGate = new(1, 1);

	private readonly MapEditorState _state;
	private CancellationTokenSource? _cancellation;
	private Task? _loop;

	public AutoSaveService(MapEditorState state) => _state = state;

	public event EventHandler<DateTime>? AutoSaved;

	public int IntervalSeconds
	{
		get => Preferences.Get(IntervalPreference, DefaultIntervalSeconds);
		set
		{
			Preferences.Set(IntervalPreference, value);
			LastError = null;
			Restart();
		}
	}

	public DateTime? LastSavedUtc { get; private set; }

	public string? LastError { get; private set; }

	public string WorkingDirectory
	{
		get
		{
			var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			if (string.IsNullOrEmpty(documents))
				documents = FileSystem.AppDataDirectory;
			return Path.Combine(documents, "Adventure Mapper");
		}
	}

	public void Restart()
	{
		Stop();
		if (IntervalSeconds > 0)
			Start();
	}

	public void Start()
	{
		Stop();
		if (IntervalSeconds <= 0)
			return;

		_cancellation = new CancellationTokenSource();
		_loop = RunAsync(_cancellation.Token);
	}

	public void Stop()
	{
		if (_cancellation is null)
			return;

		try
		{
			_cancellation.Cancel();
		}
		catch (ObjectDisposedException)
		{
			// Already torn down.
		}

		_cancellation.Dispose();
		_cancellation = null;
		_loop = null;
	}

	/// <summary>Returns the path of the most recent draft for this map title.</summary>
	public string GetWorkingPath(string title)
	{
		var name = SanitizeFileName(string.IsNullOrWhiteSpace(title) ? L.T("Default_Untitled") : title);
		return Path.Combine(WorkingDirectory, name + MapConstants.FileExtension);
	}

	public async Task<bool> SaveAsync(bool userInitiated)
	{
		if (!userInitiated && !_state.IsDirty)
			return false;

		await SaveGate.WaitAsync();
		try
		{
			var path = _state.FilePath;
			var isDraft = string.IsNullOrEmpty(path);
			if (isDraft)
				path = GetWorkingPath(_state.Document.Title);

			var json = await MainThread.InvokeOnMainThreadAsync(() =>
			{
				_state.Document.Touch();
				return MapSerializer.Serialize(_state.Document);
			});

			var directory = Path.GetDirectoryName(path!);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			var temporary = path + ".tmp";
			await File.WriteAllTextAsync(temporary, json);
			File.Move(temporary, path!, overwrite: true);

			if (isDraft)
				_state.SetFilePath(path);

			LastSavedUtc = DateTime.UtcNow;
			LastError = null;
			_state.MarkSaved(isDraft
				? L.F("Status_SavedDraft", Path.GetFileName(path))
				: L.F("Status_Saved", Path.GetFileName(path)));
			AutoSaved?.Invoke(this, LastSavedUtc.Value);
			return true;
		}
		catch (Exception ex)
		{
			LastError = ex.Message;
			_state.Status = L.F("Status_AutoSaveFailed", ex.Message);
			return false;
		}
		finally
		{
			SaveGate.Release();
		}
	}

	public void Dispose() => Stop();

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, IntervalSeconds)));
		try
		{
			while (await timer.WaitForNextTickAsync(cancellationToken))
				await SaveAsync(userInitiated: false);
		}
		catch (OperationCanceledException)
		{
			// Timer stopped with the service.
		}
	}

	private static string SanitizeFileName(string name)
	{
		var invalid = Path.GetInvalidFileNameChars();
		var cleaned = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
		return string.IsNullOrEmpty(cleaned) ? L.T("Default_Untitled") : cleaned;
	}
}
