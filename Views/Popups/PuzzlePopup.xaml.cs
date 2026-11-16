using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using Mopups.Pages;
using Mopups.Services;

namespace AdventureMapper.Views.Popups;

/// <summary>Centred Mopups dialog for creating or editing a puzzle.</summary>
public partial class PuzzlePopup : PopupPage
{
	private readonly TaskCompletionSource<MapPuzzle?> _completion = new();
	private readonly MapPuzzle _source;

	public PuzzlePopup(MapPuzzle source)
	{
		InitializeComponent();
		_source = source;

		HeadingLabel.Text = L.F("Puzzle_Heading", source.Title);
		TitleEntry.Text = source.Title;
		KindPicker.ItemsSource = Enum.GetValues<PuzzleKind>().Select(kind => L.T($"PuzzleKind_{kind}")).ToList();
		KindPicker.SelectedIndex = (int)source.Kind;
		PromptEditor.Text = source.Prompt;
		SolutionEditor.Text = source.Solution;
		RewardEntry.Text = source.Reward;
	}

	public Task<MapPuzzle?> Result => _completion.Task;

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		var updated = new MapPuzzle
		{
			Id = _source.Id,
			Title = string.IsNullOrWhiteSpace(TitleEntry.Text) ? _source.Title : TitleEntry.Text.Trim(),
			Kind = KindPicker.SelectedIndex >= 0 ? (PuzzleKind)KindPicker.SelectedIndex : _source.Kind,
			Prompt = PromptEditor.Text ?? string.Empty,
			Solution = SolutionEditor.Text ?? string.Empty,
			Reward = RewardEntry.Text ?? string.Empty,
		};

		_completion.TrySetResult(updated);
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
