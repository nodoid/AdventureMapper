using System.Globalization;
using System.Resources;

namespace AdventureMapper.Resources.Strings;

/// <summary>
/// UI string lookup. The neutral resources are English; satellite cultures (for example
/// German in AppResources.de.resx) are picked up from the device culture, a saved
/// preference, or the ADVENTUREMAPPER_CULTURE environment variable used by the tests.
/// </summary>
public static class L
{
	public const string CulturePreference = "ui.culture";

	private static readonly ResourceManager Resources =
		new("AdventureMapper.Resources.Strings.AppResources", typeof(L).Assembly);

	public static string T(string key) =>
		Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

	public static string F(string key, params object?[] args) =>
		string.Format(CultureInfo.CurrentCulture, T(key), args);

	/// <summary>Selects the UI culture once, before any page is built.</summary>
	public static void ApplyStartupCulture()
	{
		var name = Environment.GetEnvironmentVariable("ADVENTUREMAPPER_CULTURE");
		if (string.IsNullOrWhiteSpace(name))
		{
			try
			{
				name = Preferences.Default.Get(CulturePreference, string.Empty);
			}
			catch
			{
				// Preferences are unavailable very early on some platforms.
			}
		}

		if (string.IsNullOrWhiteSpace(name))
			return;

		try
		{
			var culture = CultureInfo.GetCultureInfo(name);
			CultureInfo.DefaultThreadCurrentUICulture = culture;
			CultureInfo.CurrentUICulture = culture;
		}
		catch (CultureNotFoundException)
		{
			// Unknown culture: keep the device culture.
		}
	}
}
