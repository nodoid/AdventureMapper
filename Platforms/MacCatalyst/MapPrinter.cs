using Foundation;
using UIKit;

namespace AdventureMapper.Services;

public static partial class MapPrinter
{
	private static partial Task PrintPdfAsync(byte[] pdf, string jobName)
	{
		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var controller = UIPrintInteractionController.SharedPrintController;

		var printInfo = UIPrintInfo.PrintInfo;
		printInfo.JobName = jobName;
		printInfo.OutputType = UIPrintInfoOutputType.General;
		controller.PrintInfo = printInfo;
		controller.PrintingItem = NSData.FromArray(pdf);
		controller.ShowsPageRange = true;

		var presented = controller.Present(true, (_, _, error) =>
		{
			if (error is null)
				completion.TrySetResult();
			else
				completion.TrySetException(new InvalidOperationException(error.LocalizedDescription));
		});

		if (!presented)
			completion.TrySetException(new InvalidOperationException("The print panel could not be opened."));

		return completion.Task;
	}
}
