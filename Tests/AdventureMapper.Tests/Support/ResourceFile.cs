using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AdventureMapper.Tests.Support;

internal sealed record ResourceEntry(string Key, string Value);

/// <summary>Reads a .resx file directly, so tests can compare cultures key by key.</summary>
internal static class ResourceFile
{
	private static readonly Regex PlaceholderPattern = new(@"\{(\d+)", RegexOptions.Compiled);

	public static IReadOnlyList<ResourceEntry> Read(string path)
	{
		var document = XDocument.Load(path);
		return document.Root!
			.Elements("data")
			.Where(element => element.Attribute("name") is not null)
			.Select(element => new ResourceEntry(
				element.Attribute("name")!.Value,
				element.Element("value")?.Value ?? string.Empty))
			.ToList();
	}

	public static HashSet<int> Placeholders(string value) =>
		PlaceholderPattern.Matches(value)
			.Select(match => int.Parse(match.Groups[1].Value))
			.ToHashSet();
}
