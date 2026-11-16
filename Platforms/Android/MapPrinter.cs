using Android.Content;
using Android.OS;
using Android.Print;
using Java.IO;

namespace AdventureMapper.Services;

public static partial class MapPrinter
{
	private static partial Task PrintPdfAsync(byte[] pdf, string jobName)
	{
		var activity = Platform.CurrentActivity
			?? throw new InvalidOperationException("No Android activity is available for printing.");
		var manager = activity.GetSystemService(Context.PrintService) as PrintManager
			?? throw new InvalidOperationException("Android printing is unavailable.");

		manager.Print(jobName, new PdfPrintAdapter(pdf, jobName), null);
		return Task.CompletedTask;
	}

	private sealed class PdfPrintAdapter : PrintDocumentAdapter
	{
		private readonly byte[] _pdf;
		private readonly string _jobName;

		public PdfPrintAdapter(byte[] pdf, string jobName)
		{
			_pdf = pdf;
			_jobName = jobName;
		}

		public override void OnLayout(
			PrintAttributes? oldAttributes,
			PrintAttributes? newAttributes,
			CancellationSignal? cancellationSignal,
			LayoutResultCallback? callback,
			Bundle? extras)
		{
			if (cancellationSignal?.IsCanceled == true)
			{
				callback?.OnLayoutCancelled();
				return;
			}

			var info = new PrintDocumentInfo.Builder(_jobName)
				.SetContentType(PrintContentType.Document)
				.SetPageCount(PrintDocumentInfo.PageCountUnknown)
				.Build();
			callback?.OnLayoutFinished(info, true);
		}

		public override void OnWrite(
			PageRange[]? pages,
			ParcelFileDescriptor? destination,
			CancellationSignal? cancellationSignal,
			WriteResultCallback? callback)
		{
			if (destination is null)
			{
				callback?.OnWriteFailed("No print destination was provided.");
				return;
			}

			try
			{
				using var output = new FileOutputStream(destination.FileDescriptor);
				output.Write(_pdf, 0, _pdf.Length);
				output.Flush();
				callback?.OnWriteFinished(pages ?? new[] { PageRange.AllPages! });
			}
			catch (Exception ex)
			{
				callback?.OnWriteFailed(ex.Message);
			}
		}
	}
}
