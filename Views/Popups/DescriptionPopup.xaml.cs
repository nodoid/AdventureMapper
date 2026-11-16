using AdventureMapper.Models;
using Mopups.Pages;
using Mopups.Services;

namespace AdventureMapper.Views.Popups;

/// <summary>Centred Mopups dialog for the 127-character room description.</summary>
public partial class DescriptionPopup : PopupPage
{
	private readonly TaskCompletionSource<string?> _completion = new();

	public DescriptionPopup(string heading, string description)
	{
		InitializeComponent();
		HeadingLabel.Text = heading;
		DescriptionEditor.Text = description;
		UpdateCounter(description?.Length ?? 0);
		DescriptionEditor.TextChanged += (_, e) => UpdateCounter(e.NewTextValue?.Length ?? 0);
	}

	public Task<string?> Result => _completion.Task;

	private void UpdateCounter(int length) =>
		CounterLabel.Text = $"{length}/{MapConstants.MaxDescriptionLength}";

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		_completion.TrySetResult(MapRoom.Truncate(DescriptionEditor.Text));
		await MopupService.Instance.PopAsync();
	}

	private async void OnCancelClicked(object? sender, EventArgs e)
	{
		_completion.TrySetResult(null);
		await MopupService.Instance.PopAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_completion.TrySetResult(null);
	}
}
