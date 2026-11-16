using Mopups.Pages;
using Mopups.Services;

namespace AdventureMapper.Views.Popups;

/// <summary>Branded About dialog shared by the desktop Help menu and tablet layout.</summary>
public partial class AboutPopup : PopupPage
{
	public AboutPopup()
	{
		InitializeComponent();
	}

	private async void OnCloseClicked(object? sender, EventArgs e) =>
		await MopupService.Instance.PopAsync();
}
