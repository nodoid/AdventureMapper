using System.Globalization;

namespace AdventureMapper.Tests.Support;

/// <summary>
/// Sets the UI culture and/or the ADVENTUREMAPPER_CULTURE override for one test,
/// restoring both afterwards.
/// </summary>
internal sealed class CultureScope : IDisposable
{
	private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
	private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
	private readonly string? _environment = Environment.GetEnvironmentVariable("ADVENTUREMAPPER_CULTURE");

	public CultureScope(string? uiCulture = null, string? environmentCulture = null)
	{
		Environment.SetEnvironmentVariable("ADVENTUREMAPPER_CULTURE", environmentCulture);
		CultureInfo.CurrentUICulture = uiCulture is null ? _uiCulture : CultureInfo.GetCultureInfo(uiCulture);
	}

	public void Dispose()
	{
		CultureInfo.CurrentUICulture = _uiCulture;
		CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
		Environment.SetEnvironmentVariable("ADVENTUREMAPPER_CULTURE", _environment);
	}
}
