namespace AdventureMapper.Services;

/// <summary>Hands print-ready PDF bytes to the current platform's print system.</summary>
public static partial class MapPrinter
{
	public static Task PrintAsync(byte[] pdf, string jobName) =>
		PrintPdfAsync(pdf, jobName);

	private static partial Task PrintPdfAsync(byte[] pdf, string jobName);
}
