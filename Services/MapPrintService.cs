using AdventureMapper.Models;

namespace AdventureMapper.Services;

/// <summary>Prints the same map sheet and catalogue that the PDF export produces.</summary>
public sealed class MapPrintService
{
	private readonly ExportService _exportService;

	public MapPrintService(ExportService exportService) => _exportService = exportService;

	public Task PrintAsync(AdventureMapDocument document)
	{
		var pdf = _exportService.RenderPdf(document);
		var jobName = MapFileService.SanitizeFileName(document.Title);
		return MapPrinter.PrintAsync(pdf, jobName);
	}
}
