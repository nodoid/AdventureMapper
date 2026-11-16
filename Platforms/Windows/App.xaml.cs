using AdventureMapper.Services;

namespace AdventureMapper.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	public App()
	{
		this.InitializeComponent();
		CaptureFileActivation();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	/// <summary>Handles "Open with" / double-click of a .ama file on Windows 11.</summary>
	private static void CaptureFileActivation()
	{
		try
		{
			var arguments = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
			if (arguments?.Kind != Microsoft.Windows.AppLifecycle.ExtendedActivationKind.File)
				return;

			if (arguments.Data is not Windows.ApplicationModel.Activation.IFileActivatedEventArgs fileArguments)
				return;

			var file = fileArguments.Files?.FirstOrDefault();
			if (file?.Path is { Length: > 0 } path)
				PendingFileRouter.Enqueue(path);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Could not read the file activation: {ex.Message}");
		}
	}
}
