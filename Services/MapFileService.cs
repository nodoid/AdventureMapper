using System.Text;
using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using CommunityToolkit.Maui.Storage;

namespace AdventureMapper.Services;

public enum MapExportFormat
{
	Json,
	Jpeg,
	Pdf,
}

public sealed record OpenMapResult(string Path, AdventureMapDocument Document);

/// <summary>Open/save dialogs and the bytes written by each export format.</summary>
public sealed class MapFileService
{
	private static readonly FilePickerFileType AmaFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
	{
		[DevicePlatform.Android] = new[] { MapConstants.MimeType, "application/json" },
		[DevicePlatform.iOS] = new[] { MapConstants.TypeIdentifier },
		[DevicePlatform.MacCatalyst] = new[] { MapConstants.TypeIdentifier },
		[DevicePlatform.WinUI] = new[] { MapConstants.FileExtension },
	});

	private readonly ExportService _exportService;

	public MapFileService(ExportService exportService) => _exportService = exportService;

	public async Task<OpenMapResult?> PickOpenAsync()
	{
		var picked = await FilePicker.Default.PickAsync(new PickOptions
		{
			PickerTitle = L.T("Picker_OpenMap"),
			FileTypes = AmaFileType,
		});

		if (picked is null)
			return null;

		return await OpenPathAsync(picked.FullPath);
	}

	public async Task<OpenMapResult> OpenPathAsync(string path)
	{
		if (!File.Exists(path))
			throw new MapFormatException(L.F("File_NotFound", Path.GetFileName(path)));

		var json = await File.ReadAllTextAsync(path);
		return new OpenMapResult(path, MapSerializer.Deserialize(json));
	}

	/// <summary>Picks a location with the platform save dialog and writes the chosen format.</summary>
	public async Task<string?> ExportAsync(AdventureMapDocument document, MapExportFormat format)
	{
		var extension = format switch
		{
			MapExportFormat.Jpeg => ".jpg",
			MapExportFormat.Pdf => ".pdf",
			_ => ".json",
		};

		var bytes = format switch
		{
			MapExportFormat.Jpeg => _exportService.RenderJpeg(document),
			MapExportFormat.Pdf => _exportService.RenderPdf(document),
			_ => Encoding.UTF8.GetBytes(MapSerializer.Serialize(document)),
		};

		var fileName = SanitizeFileName(document.Title) + extension;
		using var stream = new MemoryStream(bytes);
		var result = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);
		return result.IsSuccessful ? result.FilePath : null;
	}

	/// <summary>Writes the .ama file itself (used by Save As).</summary>
	public async Task<string?> SaveDocumentAsAsync(AdventureMapDocument document, string suggestedTitle)
	{
		var fileName = SanitizeFileName(suggestedTitle) + MapConstants.FileExtension;
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MapSerializer.Serialize(document)));
		var result = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);
		return result.IsSuccessful ? result.FilePath : null;
	}

	public static string SanitizeFileName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return L.T("Default_Untitled");

		var invalid = Path.GetInvalidFileNameChars();
		var cleaned = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
		return string.IsNullOrEmpty(cleaned) ? L.T("Default_Untitled") : cleaned;
	}
}
