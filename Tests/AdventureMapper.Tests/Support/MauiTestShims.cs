// The application services are compiled against .NET MAUI. This project targets plain
// net10.0, so these stand-ins supply the small slice of MAUI Essentials they touch.
// They live in the global namespace, matching MAUI's implicit usings.
internal static class Preferences
{
	public static TestPreferences Default { get; } = new();

	public static T Get<T>(string key, T defaultValue) => Default.Get(key, defaultValue);

	public static void Set<T>(string key, T value) => Default.Set(key, value);

	public static void Reset() => Default.Clear();
}

internal sealed class TestPreferences
{
	private readonly Dictionary<string, object?> _values = new();

	public T Get<T>(string key, T defaultValue) =>
		_values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;

	public void Set<T>(string key, T value) => _values[key] = value;

	public bool Contains(string key) => _values.ContainsKey(key);

	public void Clear() => _values.Clear();
}

internal static class MainThread
{
	public static Task<T> InvokeOnMainThreadAsync<T>(Func<T> func) => Task.FromResult(func());

	public static Task InvokeOnMainThreadAsync(Action action)
	{
		action();
		return Task.CompletedTask;
	}
}

internal static class FileSystem
{
	public static string AppDataDirectory { get; set; } =
		Path.Combine(Path.GetTempPath(), "adventure-mapper-tests");
}
