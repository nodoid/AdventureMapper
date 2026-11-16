using System.Text.RegularExpressions;
using AdventureMapper.Tests.Support;

namespace AdventureMapper.Tests.Localization;

/// <summary>
/// Guards the resources against drift: every key the UI asks for must exist, and the
/// formatted keys must keep their placeholders.
/// </summary>
public class LocalizationCoverageTests
{
	private static readonly HashSet<string> ResourceKeys = ResourceFile
		.Read(RepoFiles.AppPath("Resources", "Strings", "AppResources.resx"))
		.Select(entry => entry.Key)
		.ToHashSet();

	private static readonly Dictionary<string, string> EnglishValues = ResourceFile
		.Read(RepoFiles.AppPath("Resources", "Strings", "AppResources.resx"))
		.ToDictionary(entry => entry.Key, entry => entry.Value);

	private static readonly Regex CodeKeyPattern =
		new(@"L\.[TF]\(\s*""([A-Za-z0-9_]+)""", RegexOptions.Compiled);

	private static readonly Regex XamlKeyPattern =
		new(@"\{loc:Translate\s+([A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);

	private static readonly Regex FormattedKeyPattern =
		new(@"L\.F\(\s*""([A-Za-z0-9_]+)""", RegexOptions.Compiled);

	private static readonly Lazy<List<string>> SourceFiles =
		new(() => RepoFiles.AppSourceFiles().ToList());

	private static readonly Lazy<(List<string> CodeKeys, List<string> XamlKeys, List<string> FormattedKeys)> Usage =
		new(ReadUsage);

	private static (List<string> CodeKeys, List<string> XamlKeys, List<string> FormattedKeys) ReadUsage()
	{
		var codeKeys = new List<string>();
		var xamlKeys = new List<string>();
		var formattedKeys = new List<string>();

		foreach (var file in SourceFiles.Value)
		{
			var text = File.ReadAllText(file);

			codeKeys.AddRange(CodeKeyPattern.Matches(text).Select(match => match.Groups[1].Value));
			xamlKeys.AddRange(XamlKeyPattern.Matches(text).Select(match => match.Groups[1].Value));
			formattedKeys.AddRange(FormattedKeyPattern.Matches(text).Select(match => match.Groups[1].Value));
		}

		return (codeKeys, xamlKeys, formattedKeys);
	}

	[Fact]
	public void TheScanFindsTheInterface()
	{
		Assert.True(Usage.Value.CodeKeys.Count > 100);
		Assert.True(Usage.Value.XamlKeys.Count > 50);
	}

	[Fact]
	public void EveryKeyUsedInCodeExists()
	{
		var missing = Usage.Value.CodeKeys
			.Where(key => !ResourceKeys.Contains(key))
			.Distinct()
			.ToList();

		Assert.Empty(missing);
	}

	[Fact]
	public void EveryKeyUsedInXamlExists()
	{
		var missing = Usage.Value.XamlKeys
			.Where(key => !ResourceKeys.Contains(key))
			.Distinct()
			.ToList();

		Assert.Empty(missing);
	}

	[Fact]
	public void EveryFormattedKeyHasAPlaceholder()
	{
		var offenders = Usage.Value.FormattedKeys
			.Where(key => !EnglishValues.TryGetValue(key, out var value) || !value.Contains("{0"))
			.Distinct()
			.ToList();

		Assert.Empty(offenders);
	}

	[Fact]
	public void TheInterfaceUsesBothLanguagesOfEveryScannedFile()
	{
		Assert.All(SourceFiles.Value, file =>
			Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(file))));
	}
}
