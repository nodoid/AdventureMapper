using CoreGraphics;
using Foundation;
using UIKit;

namespace AdventureMapper.Services;

public static partial class MapPrinter
{
	private static partial Task PrintPdfAsync(byte[] pdf, string jobName)
	{
		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var view = Platform.GetCurrentUIViewController()?.View;
		if (view is null)
			return Task.FromException(new InvalidOperationException("The print sheet could not be opened."));

		var controller = UIPrintInteractionController.SharedPrintController;

		var printInfo = UIPrintInfo.PrintInfo;
		printInfo.JobName = jobName;
		printInfo.OutputType = UIPrintInfoOutputType.General;
		controller.PrintInfo = printInfo;
		controller.PrintingItem = NSData.FromArray(pdf);
		controller.ShowsPageRange = true;

		var anchor = new CGRect(view.Bounds.GetMidX(), view.Bounds.GetMidY(), 1, 1);
		var presented = controller.PresentFromRectInView(anchor, view, true, (_, _, error) =>
		{
			if (error is null)
				completion.TrySetResult();
			else
				completion.TrySetException(new InvalidOperationException(error.LocalizedDescription));
		});

		if (!presented)
			completion.TrySetException(new InvalidOperationException("The print sheet could not be opened."));

		return completion.Task;
	}
}
