using System.Globalization;
using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using AdventureMapper.Tests.Support;

namespace AdventureMapper.Tests.Localization;

public class LocalizationTests : IDisposable
{
	public LocalizationTests() => Preferences.Reset();

	public void Dispose() => Preferences.Reset();

	[Fact]
	public void T_ReturnsTheEnglishValueForKnownKeys()
	{
		var value = L.T("App_Title");

		Assert.False(string.IsNullOrWhiteSpace(value));
		Assert.NotEqual("App_Title", value);
	}

	[Fact]
	public void T_ReturnsTheKeyForUnknownKeys()
	{
		Assert.Equal("This_Key_Does_Not_Exist", L.T("This_Key_Does_Not_Exist"));
	}

	[Fact]
	public void F_FormatsTheArguments()
	{
		Assert.Contains("3", L.F("Layer_Upper", 3));
		Assert.Contains("map.ama", L.F("Status_Opened", "map.ama"));
	}

	[Fact]
	public void GermanSatellite_IsAvailableForTranslatedKeys()
	{
		var english = L.T("Common_Save");
		var englishTab = L.T("Tab_Map");

		using (new CultureScope("de"))
		{
			Assert.NotEqual(english, L.T("Common_Save"));
			Assert.NotEqual(englishTab, L.T("Tab_Map"));
			Assert.False(string.IsNullOrWhiteSpace(L.T("Common_Save")));
		}

		Assert.Equal(english, L.T("Common_Save"));
	}

	[Fact]
	public void GermanSatellite_TranslatesTheGeneratedEnumKeys()
	{
		using (new CultureScope("de"))
		{
			foreach (var direction in Enum.GetValues<MapDirection>())
			{
				Assert.NotEqual($"Direction_{direction}", MapRules.DirectionLabel(direction));
				Assert.NotEqual($"DirectionShort_{direction}", MapRules.DirectionShortLabel(direction));
			}

			foreach (var kind in Enum.GetValues<RoomKind>())
				Assert.NotEqual($"RoomKind_{kind}", L.T($"RoomKind_{kind}"));

			foreach (var kind in Enum.GetValues<PuzzleKind>())
				Assert.NotEqual($"PuzzleKind_{kind}", L.T($"PuzzleKind_{kind}"));
		}
	}

	[Fact]
	public void ApplyStartupCulture_UsesTheEnvironmentOverride()
	{
		using var scope = new CultureScope();
		Environment.SetEnvironmentVariable("ADVENTUREMAPPER_CULTURE", "de");

		L.ApplyStartupCulture();

		Assert.Equal("de", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
	}

	[Fact]
	public void ApplyStartupCulture_UsesTheSavedPreference()
	{
		using var scope = new CultureScope();
		Environment.SetEnvironmentVariable("ADVENTUREMAPPER_CULTURE", null);
		Preferences.Set(L.CulturePreference, "de");

		L.ApplyStartupCulture();

		Assert.Equal("de", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
	}

	[Fact]
	public void ApplyStartupCulture_PrefersTheEnvironmentOverride()
	{
		using var scope = new CultureScope(uiCulture: "de-GB");
		Preferences.Set(L.CulturePreference, "de");
		Environment.SetEnvironmentVariable("ADVENTUREMAPPER_CULTURE", "en");

		L.ApplyStartupCulture();

		Assert.Equal("en", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
	}

	[Fact]
	public void ApplyStartupCulture_IgnoresUnknownCultures()
	{
		using var scope = new CultureScope("de-DE");
		Environment.SetEnvironmentVariable("ADVENTUREMAPPER_CULTURE", "!!!");

		L.ApplyStartupCulture();

		Assert.Equal("de", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
	}

	[Fact]
	public void ApplyStartupCulture_LeavesTheDeviceCultureWhenUnset()
	{
		using var scope = new CultureScope("de-DE");
		Environment.SetEnvironmentVariable("ADVENTUREMAPPER_CULTURE", null);

		L.ApplyStartupCulture();

		Assert.Equal("de", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
	}

	[Fact]
	public void AllDirectionShortLabels_AreDistinct()
	{
		var labels = Enum.GetValues<MapDirection>()
			.Select(direction => MapRules.DirectionShortLabel(direction))
			.ToList();

		Assert.Equal(10, labels.Count);
		Assert.Equal(10, labels.Distinct().Count());
	}

	[Fact]
	public void LocalisedRoomAndPuzzleKinds_AreDistinct()
	{
		var rooms = Enum.GetValues<RoomKind>().Select(kind => L.T($"RoomKind_{kind}")).ToList();
		var puzzles = Enum.GetValues<PuzzleKind>().Select(kind => L.T($"PuzzleKind_{kind}")).ToList();

		Assert.Equal(Enum.GetValues<RoomKind>().Length, rooms.Distinct().Count());
		Assert.Equal(Enum.GetValues<PuzzleKind>().Length, puzzles.Distinct().Count());
		Assert.All(rooms.Concat(puzzles), text => Assert.False(string.IsNullOrWhiteSpace(text)));
	}
}
