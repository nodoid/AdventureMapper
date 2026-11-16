namespace AdventureMapper.Views;

public partial class SplashPage : ContentPage
{
	public SplashPage()
	{
		InitializeComponent();
	}

	/// <summary>Fades the mark and wordmark out, leaving the navy launch background.</summary>
	public Task FadeOutAsync() =>
		SplashContent.FadeToAsync(0, 280, Easing.CubicIn);
}
