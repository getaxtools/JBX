using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Jbx.Desktop.Features.Details;
using Jbx.Desktop.Features.Dialogs;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;
using Jbx.Platform;

namespace Jbx.Desktop.Features.LogViewer;

internal sealed partial class LogViewerViewModel
{
    public const int MaxCopiedLines = 50_000;
    private const int CopyBatchLines = 2_000;

    [RelayCommand]
    private async Task CopyLinesAsync()
    {
        // Counted before anything is listed: Select All on a big file selects millions of lines.
        var selected = Selection.SelectedIndexes;
        if (selected.Count == 0)
        {
            return;
        }

        if (selected.Count > MaxCopiedLines)
        {
            NoticeText = Strings.FormatViewer_CopyTooMany(Counts.Number(MaxCopiedLines));
            return;
        }

        var numbers = selected.Where(index => index < Lines.Count).Select(LineNumberAt).Order().ToList();
        List<string>? texts;
        try
        {
            texts = await ReadTextsAsync(numbers).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (texts is null)
        {
            return;
        }

        await _services.Interaction.CopyTextAsync(string.Join(Environment.NewLine, texts)).ConfigureAwait(true);
        NoticeText = numbers.Count == 1 ? Strings.Viewer_CopiedOne : Strings.FormatViewer_Copied(Counts.Number(numbers.Count));
    }

    [RelayCommand]
    private async Task GoToLineAsync()
    {
        var current = (CurrentLineNumber() ?? 0) + 1;
        var prompt = new TextPromptViewModel(
            Strings.Viewer_GoToLineTitle,
            Strings.FormatViewer_GoToLinePrompt(Counts.Number(_document.LineCount)),
            current.ToString(CultureInfo.CurrentCulture),
            Strings.Viewer_GoToLineAccept);
        var answer = await _services.Interaction.AskForTextAsync(prompt).ConfigureAwait(true);
        if (answer is null)
        {
            return;
        }

        if (!int.TryParse(answer.Trim(), NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var wanted) || wanted < 1)
        {
            NoticeText = Strings.Viewer_GoToLineInvalid;
            return;
        }

        SelectLine(Math.Min(wanted, _document.LineCount) - 1);
    }

    [RelayCommand]
    private async Task OpenWithDefaultAppAsync()
    {
        NoticeText = await FileOpener.OpenAsync(_services.Shell, Target.Path, Target.Title).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ShowInFolderAsync()
    {
        var opened = await _services.Shell.RevealInFolderAsync(Target.Path).ConfigureAwait(true);
        NoticeText = opened ? null : Strings.Details_ExplorerFailed;
    }

    [RelayCommand]
    private async Task CopyPathAsync()
    {
        await _services.Interaction.CopyTextAsync(Target.Path.Display).ConfigureAwait(true);
        NoticeText = Strings.Details_PathCopied;
    }

    [RelayCommand]
    private void ToggleFollow()
    {
        Follow = !Follow;
    }

    [RelayCommand]
    private void FocusSearch()
    {
        RequestSearchFocus();
    }

    /// <summary>Reads the text of the given lines in runs of neighbours; null when the file stopped answering.</summary>
    private async Task<List<string>?> ReadTextsAsync(List<int> numbers)
    {
        var texts = new List<string>(numbers.Count);
        var start = 0;
        while (start < numbers.Count)
        {
            var end = start;
            while (end + 1 < numbers.Count && numbers[end + 1] == numbers[end] + 1 && end + 1 - start < CopyBatchLines)
            {
                end++;
            }

            var page = await _document.ReadLinesAsync(numbers[start], end - start + 1, _lifetime.Token).ConfigureAwait(true);
            if (page.Reachability != Reachability.Reachable)
            {
                ProblemText = Strings.FormatViewer_ReadFailed(StatusText.Label(page.Reachability));
                return null;
            }

            texts.AddRange(page.Lines.Select(line => line.Text));
            start = end + 1;
        }

        return texts;
    }
}
