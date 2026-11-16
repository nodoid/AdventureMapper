using AdventureMapper.Services;
using Foundation;
using UIKit;

namespace AdventureMapper;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override void BuildMenu(IUIMenuBuilder builder)
	{
		base.BuildMenu(builder);
		MacEditMenu.Install(builder);
	}

	public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
	{
		if (url is not null && url.IsFileUrl && url.Path is { Length: > 0 } path)
		{
			PendingFileRouter.Enqueue(path);
			return true;
		}

		return base.OpenUrl(application, url!, options);
	}
}
