using AdventureMapper.Models;
using AdventureMapper.Resources.Strings;
using AdventureMapper.Views.Popups;
using Mopups.Services;

namespace AdventureMapper.Services;

/// <summary>
/// Commands shared by the desktop menus, the tablet toolbar and the Mopups dialogs.
/// </summary>
public sealed class EditorActions
{
	private readonly MapEditorState _state;
	private readonly MapFileService _files;
	private readonly MapPrintService _print;
	private readonly AutoSaveService _autoSave;

	public EditorActions(MapEditorState state, MapFileService files, MapPrintService print, AutoSaveService autoSave)
	{
		_state = state;
		_files = files;
		_print = print;
		_autoSave = autoSave;
	}

	public MapEditorState State => _state;

	public static Page? CurrentPage => Application.Current?.Windows.FirstOrDefault()?.Page;

	public async Task NewMapAsync()
	{
		if (!await ConfirmDiscardAsync())
			return;

		_state.NewDocument();
		_autoSave.Restart();
	}

	public async Task<bool> OpenAsync()
	{
		if (!await ConfirmDiscardAsync())
			return false;

		try
		{
			var result = await _files.PickOpenAsync();
			if (result is null)
				return false;

			_state.Load(result.Document, result.Path);
			_autoSave.Restart();
			return true;
		}
		catch (Exception ex)
		{
			await AlertAsync(L.T("Dialog_OpenError"), ex.Message);
			return false;
		}
	}

	/// <summary>Opens a map passed by the OS (double-clicked .ama file).</summary>
	public async Task<bool> OpenPathAsync(string path)
	{
		try
		{
			var result = await _files.OpenPathAsync(path);
			_state.Load(result.Document, result.Path);
			_autoSave.Restart();
			return true;
		}
		catch (Exception ex)
		{
			await AlertAsync(L.T("Dialog_OpenError"), ex.Message);
			return false;
		}
	}

	public async Task<bool> SaveAsync(bool saveAs)
	{
		try
		{
			if (saveAs || string.IsNullOrEmpty(_state.FilePath))
			{
				var chosen = await _files.SaveDocumentAsAsync(_state.Document, _state.Document.Title);
				if (chosen is null)
					return false;

				_state.SetFilePath(chosen);
			}

			return await _autoSave.SaveAsync(userInitiated: true);
		}
		catch (Exception ex)
		{
			await AlertAsync(L.T("Dialog_SaveError"), ex.Message);
			return false;
		}
	}

	public async Task ExportAsync(MapExportFormat format)
	{
		try
		{
			var path = await _files.ExportAsync(_state.Document, format);
			if (!string.IsNullOrEmpty(path))
				_state.Status = L.F("Status_Exported", Path.GetFileName(path));
		}
		catch (Exception ex)
		{
			await AlertAsync(L.T("Dialog_ExportFailed"), ex.Message);
		}
	}

	public async Task PrintAsync()
	{
		try
		{
			await _print.PrintAsync(_state.Document);
		}
		catch (Exception ex)
		{
			await AlertAsync(L.T("Dialog_PrintFailed"), ex.Message);
		}
	}

	public async Task ExportWithPromptAsync()
	{
		var page = CurrentPage;
		if (page is null)
			return;

		var jpeg = L.T("Dialog_ExportJpeg");
		var pdf = L.T("Dialog_ExportPdf");
		var json = L.T("Dialog_ExportJson");
		var choice = await page.DisplayActionSheetAsync(L.T("Dialog_ExportTitle"), L.T("Common_Cancel"), null,
			jpeg, pdf, json);

		var format = choice switch
		{
			var picked when picked == jpeg => MapExportFormat.Jpeg,
			var picked when picked == pdf => MapExportFormat.Pdf,
			var picked when picked == json => MapExportFormat.Json,
			_ => (MapExportFormat?)null,
		};

		if (format is not null)
			await ExportAsync(format.Value);
	}

	public async Task DeleteSelectedRoomAsync()
	{
		if (_state.SelectedRoom is null)
			return;

		var page = CurrentPage;
		if (page is not null)
		{
			var confirmed = await page.DisplayAlertAsync(
				L.T("Dialog_DeleteTitle"),
				L.F("Dialog_DeleteMessage", _state.SelectedRoom.Name, _state.SelectedRoom.Reference),
				L.T("Common_Delete"),
				L.T("Common_Cancel"));

			if (!confirmed)
				return;
		}

		_state.DeleteSelectedRoom();
	}

	public async Task ShowAboutAsync()
	{
		if (CurrentPage is not null)
			await MopupService.Instance.PushAsync(new AboutPopup());
	}

	private async Task<bool> ConfirmDiscardAsync()
	{
		if (!_state.IsDirty)
			return true;

		var page = CurrentPage;
		if (page is null)
			return true;

		return await page.DisplayAlertAsync(
			L.T("Dialog_UnsavedTitle"),
			L.T("Dialog_UnsavedMessage"),
			L.T("Dialog_Discard"),
			L.T("Common_Cancel"));
	}

	public static async Task AlertAsync(string title, string message)
	{
		var page = CurrentPage;
		if (page is not null)
			await page.DisplayAlertAsync(title, message, L.T("Common_OK"));
	}

	public static async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
	{
		var page = CurrentPage;
		return page is not null && await page.DisplayAlertAsync(title, message, accept, cancel);
	}
}
