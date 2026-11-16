using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AdventureMapper.Models;
using AdventureMapper.Services;

namespace AdventureMapper;

[Activity(
	Theme = "@style/Maui.SplashTheme",
	MainLauncher = true,
	LaunchMode = LaunchMode.SingleTop,
	ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
	new[] { Intent.ActionView },
	Categories = new[] { Intent.CategoryDefault },
	DataScheme = "content",
	DataHost = "*",
	DataMimeType = MapConstants.MimeType)]
[IntentFilter(
	new[] { Intent.ActionView },
	Categories = new[] { Intent.CategoryDefault },
	DataScheme = "file",
	DataHost = "*",
	DataPathPattern = ".*\\.ama")]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		HandleLaunchIntent(Intent);
	}

	protected override void OnNewIntent(Intent? intent)
	{
		base.OnNewIntent(intent);
		HandleLaunchIntent(intent);
	}

	private void HandleLaunchIntent(Intent? intent)
	{
		var uri = intent?.Data;
		if (uri is null)
			return;

		try
		{
			var destination = Path.Combine(FileSystem.CacheDirectory, $"opened-{Guid.NewGuid():N}.ama");
			using var input = ContentResolver?.OpenInputStream(uri);
			if (input is null)
				return;

			using var output = File.Create(destination);
			input.CopyTo(output);
			PendingFileRouter.Enqueue(destination);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Could not open the shared map file: {ex}");
		}
	}
}
