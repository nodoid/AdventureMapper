using AdventureMapper.Resources.Strings;
using AdventureMapper.Services;
using Foundation;
using UIKit;

namespace AdventureMapper;

/// <summary>
/// Mac Catalyst replaces an "Edit" MenuBarItem with the system Edit menu, so the layout
/// history commands are added to that menu natively instead of in XAML.
/// </summary>
internal static class MacEditMenu
{
	private static MapEditorState? _state;
	private static UIAction? _undo;
	private static UIAction? _redo;

	public static void Install(IUIMenuBuilder builder)
	{
		_undo = UIAction.Create(L.T("Menu_Undo"), null, null, _ => _state?.Undo());
		_redo = UIAction.Create(L.T("Menu_Redo"), null, null, _ => _state?.Redo());

		var history = UIMenu.Create(
			string.Empty,
			null,
			null,
			UIMenuOptions.DisplayInline,
			new UIMenuElement[] { _undo, _redo });

		builder.InsertChildMenuAtStart(history, UIMenuIdentifier.Edit.GetConstant()?.ToString() ?? "edit");
		ApplyState();
	}

	/// <summary>Called once the workspace exists, so the items can follow its history.</summary>
	public static void Attach(MapEditorState state)
	{
		if (ReferenceEquals(_state, state))
			return;

		if (_state is not null)
			_state.PropertyChanged -= OnStateChanged;

		_state = state;
		_state.PropertyChanged += OnStateChanged;
		ApplyState();
	}

	private static void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName is not (nameof(MapEditorState.CanUndo) or nameof(MapEditorState.CanRedo)))
			return;

		ApplyState();

		// Existing menu elements keep their first enabled state, so rebuild the menus.
		UIMenuSystem.MainSystem.SetNeedsRebuild();
	}

	private static void ApplyState()
	{
		if (_undo is not null)
			_undo.Attributes = _state?.CanUndo == true
				? (UIMenuElementAttributes)0
				: UIMenuElementAttributes.Disabled;
		if (_redo is not null)
			_redo.Attributes = _state?.CanRedo == true
				? (UIMenuElementAttributes)0
				: UIMenuElementAttributes.Disabled;
	}
}
