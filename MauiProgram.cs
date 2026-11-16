using AdventureMapper.Resources.Strings;
using AdventureMapper.Services;
using AdventureMapper.Views;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Mopups.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace AdventureMapper;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		L.ApplyStartupCulture();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.UseSkiaSharp()
			.ConfigureMopups()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<MapEditorState>();
		builder.Services.AddSingleton<AutoSaveService>();
		builder.Services.AddSingleton<ExportService>();
		builder.Services.AddSingleton<MapFileService>();
		builder.Services.AddSingleton<MapPrintService>();
		builder.Services.AddSingleton<EditorActions>();

		builder.Services.AddTransient<MapToolbarView>();
		builder.Services.AddTransient<MapCanvasView>();
		builder.Services.AddTransient<InspectorView>();
		builder.Services.AddTransient<WorkspacePage>();
		builder.Services.AddTransient<MapTabPage>();
		builder.Services.AddTransient<InspectorTabPage>();
		builder.Services.AddTransient<AppShell>();
		builder.Services.AddTransient<UnsupportedDevicePage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
