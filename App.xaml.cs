using AdventureMapper.Resources.Strings;
using AdventureMapper.Services;
using AdventureMapper.Views;

namespace AdventureMapper;

public partial class App : Application
{
	private readonly IServiceProvider _services;
	private readonly AutoSaveService _autoSave;

	public App(IServiceProvider services, AutoSaveService autoSave)
	{
		InitializeComponent();

		// The editor is a light-mode app on every platform, whatever the device setting.
		UserAppTheme = AppTheme.Light;

		_services = services;
		_autoSave = autoSave;
	}

	/// <summary>Tablets and phones get the tab bar; macOS and Windows get the desktop workspace.</summary>
	public static bool UseTabletLayout =>
		DeviceInfo.Idiom == DeviceIdiom.Tablet || DeviceInfo.Idiom == DeviceIdiom.Phone;

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var idiom = DeviceInfo.Idiom;
		Page root;
		if (idiom == DeviceIdiom.Tablet || idiom == DeviceIdiom.Phone)
			root = _services.GetRequiredService<AppShell>();
		else
		{
			var workspace = _services.GetRequiredService<WorkspacePage>();

			// MAUI only shows MenuBarItems when the page is hosted in a NavigationPage or Shell.
			var navigation = new NavigationPage(workspace);
#if WINDOWS
			// Windows draws MenuBarItems inside the navigation bar, so it must stay visible there.
			NavigationPage.SetHasNavigationBar(workspace, true);
#else
			NavigationPage.SetHasNavigationBar(workspace, false);
#endif
			root = navigation;
		}

		// Mac Catalyst ignores MauiSplashScreen, so desktop gets a short branded
		// startup page. Mobile already shows the native launch screen.
		var launchPage = root;
#if MACCATALYST
		if (idiom != DeviceIdiom.Phone && idiom != DeviceIdiom.Tablet)
			launchPage = new SplashPage();
#endif

		var window = new Window(launchPage) { Title = L.T("App_Title") };

		if (idiom != DeviceIdiom.Phone && idiom != DeviceIdiom.Tablet)
		{
			window.Width = 1480;
			window.Height = 940;
			window.MinimumWidth = 1024;
			window.MinimumHeight = 700;
		}

		window.Created += async (_, _) =>
		{
			_autoSave.Start();
#if MACCATALYST
			if (launchPage is SplashPage splash)
			{
				await Task.Delay(650);
				if (window.Handler is null)
					return;

				await splash.FadeOutAsync();
				if (window.Handler is not null)
					window.Page = root;
			}
#endif
		};
		window.Deactivated += (_, _) => _ = _autoSave.SaveAsync(userInitiated: false);
		window.Stopped += (_, _) => _ = _autoSave.SaveAsync(userInitiated: false);
		window.Destroying += (_, _) => _autoSave.Dispose();

		return window;
	}
}
