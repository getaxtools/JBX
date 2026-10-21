using System.Globalization;
using Jbx.Services.Logs;

namespace Jbx.Desktop.Features.LogViewer;

/// <summary>A line as the list shows it. Until its text arrives it is a placeholder, so scrolling never waits for a read.</summary>
internal sealed class LogLineViewModel
{
    private LogLineViewModel(int number, string text, LineLevel level, IReadOnlyList<MatchSpan> highlights, bool isLoading)
    {
        Number = number;
        Text = text;
        Level = level;
        Highlights = highlights;
        IsLoading = isLoading;
    }

    public int Number { get; }

    public string NumberText => (Number + 1).ToString("N0", CultureInfo.CurrentCulture);

    public string Text { get; }

    public LineLevel Level { get; }

    public IReadOnlyList<MatchSpan> Highlights { get; }

    public bool IsLoading { get; }

    public bool IsCut { get; private init; }

    public bool IsUnfinished { get; private init; }

    public bool IsError => Level == LineLevel.Error;

    public bool IsWarning => Level == LineLevel.Warning;

    public bool IsDebug => Level == LineLevel.Debug;

    public static LogLineViewModel Loading(int number)
    {
        return new LogLineViewModel(number, string.Empty, LineLevel.None, [], isLoading: true);
    }

    public static LogLineViewModel From(LogLine line, LogMatcher? search)
    {
        var highlights = search?.FindAll(line.Text) ?? [];
        return new LogLineViewModel(line.Number, line.Text, line.Level, highlights, isLoading: false) { IsCut = line.IsCut, IsUnfinished = line.IsUnfinished };
    }

    public override string ToString()
    {
        return Text;
    }
}
