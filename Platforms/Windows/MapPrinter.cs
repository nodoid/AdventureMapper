using System.Diagnostics;

namespace AdventureMapper.Services;

public static partial class MapPrinter
{
	private static partial Task PrintPdfAsync(byte[] pdf, string jobName)
	{
		var path = Path.Combine(
			FileSystem.CacheDirectory,
			$"{MapFileService.SanitizeFileName(jobName)}-{Guid.NewGuid():N}.pdf");

		File.WriteAllBytes(path, pdf);
		Process.Start(new ProcessStartInfo
		{
			FileName = path,
			Verb = "print",
			UseShellExecute = true,
		});

		return Task.CompletedTask;
	}
}
