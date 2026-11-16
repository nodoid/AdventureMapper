using AdventureMapper.Models;
using AdventureMapper.Tests.Support;

namespace AdventureMapper.Tests.Localization;

public class ResourceFileTests
{
	private static readonly string EnglishPath = RepoFiles.AppPath("Resources", "Strings", "AppResources.resx");
	private static readonly string GermanPath = RepoFiles.AppPath("Resources", "Strings", "AppResources.de.resx");

	private static readonly IReadOnlyList<ResourceEntry> English = ResourceFile.Read(EnglishPath);
	private static readonly IReadOnlyList<ResourceEntry> German = ResourceFile.Read(GermanPath);

	[Fact]
	public void English_HasTheFullKeySet()
	{
		Assert.True(English.Count > 200);
		Assert.Equal(English.Count, English.Select(entry => entry.Key).Distinct().Count());
	}

	[Fact]
	public void German_TranslatesExactlyTheSameKeys()
	{
		var englishKeys = English.Select(entry => entry.Key).ToHashSet();
		var germanKeys = German.Select(entry => entry.Key).ToHashSet();

		Assert.Empty(germanKeys.Except(englishKeys));
		Assert.Empty(englishKeys.Except(germanKeys));
	}

	[Fact]
	public void NoValueIsLeftEmpty()
	{
		Assert.All(English, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value)));
		Assert.All(German, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value)));
	}

	[Fact]
	public void German_KeepsTheFormatPlaceholders()
	{
		var german = German.ToDictionary(entry => entry.Key, entry => entry.Value);
		var mismatches = English
			.Where(entry => german.ContainsKey(entry.Key))
			.Where(entry => !ResourceFile.Placeholders(entry.Value).SetEquals(ResourceFile.Placeholders(german[entry.Key])))
			.Select(entry => entry.Key)
			.ToList();

		Assert.Empty(mismatches);
	}

	[Fact]
	public void German_ActuallyTranslatesTheInterface()
	{
		var german = German.ToDictionary(entry => entry.Key, entry => entry.Value);
		var translated = English.Count(entry => german.TryGetValue(entry.Key, out var value) && value != entry.Value);

		Assert.True(translated > 100, $"Only {translated} German strings differ from English.");
	}

	[Theory]
	[InlineData("App_Title")]
	[InlineData("Menu_File")]
	[InlineData("Tab_Map")]
	[InlineData("Common_Save")]
	[InlineData("Common_Cancel")]
	[InlineData("Status_NotSaved")]
	[InlineData("Layer_Ground")]
	[InlineData("RoomKind_Bridge")]
	[InlineData("PuzzleKind_Lock")]
	[InlineData("Direction_NorthWest")]
	[InlineData("DirectionShort_NW")]
	public void CoreKeys_ArePresent(string key)
	{
		Assert.Contains(English, entry => entry.Key == key);
		Assert.Contains(German, entry => entry.Key == key);
	}

	[Fact]
	public void EveryEnumDrivenKey_Exists()
	{
		var keys = English.Select(entry => entry.Key).ToHashSet();

		foreach (var direction in Enum.GetValues<MapDirection>())
			Assert.Contains($"Direction_{direction}", keys);

		foreach (var key in new[]
		{
			"DirectionShort_N", "DirectionShort_NE", "DirectionShort_E", "DirectionShort_SE",
			"DirectionShort_S", "DirectionShort_SW", "DirectionShort_W", "DirectionShort_NW",
			"DirectionShort_Up", "DirectionShort_Down",
		})
			Assert.Contains(key, keys);

		foreach (var kind in Enum.GetValues<RoomKind>())
			Assert.Contains($"RoomKind_{kind}", keys);

		foreach (var kind in Enum.GetValues<PuzzleKind>())
			Assert.Contains($"PuzzleKind_{kind}", keys);
	}

	[Fact]
	public void Keys_UseTheDocumentedNamingConvention()
	{
		var offenders = English
			.Select(entry => entry.Key)
			.Where(key => key.Contains(' ') || key.Contains('.'))
			.ToList();

		Assert.Empty(offenders);
	}
}
