using System.Globalization;
using Jbx.Core.Config;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;
using Jbx.Platform;

namespace Jbx.Desktop.Features.Details;

/// <summary>One labelled line in the details panel, such as "Retention: 30 days".</summary>
internal sealed record DetailsFact(string Label, string Value)
{
    public static IReadOnlyList<DetailsFact> ForFolder(DetailsTarget target)
    {
        var facts = new List<DetailsFact>();
        Add(facts, Strings.Details_Type, target.TypeText);
        Add(facts, Strings.Details_Pattern, target.LogFilePattern);
        Add(facts, Strings.Details_Retention, target.RetentionDays is { } days ? Strings.FormatDetails_RetentionDays(days) : null);
        Add(facts, Strings.Details_Notes, target.Notes);
        return facts;
    }

    public static IReadOnlyList<DetailsFact> ForFile(FileEntry file, DateTimeOffset now)
    {
        return
        [
            new DetailsFact(Strings.Details_Modified, Strings.FormatDetails_ModifiedValue(RelativeTime.Describe(file.LastWriteUtc, now), RelativeTime.Absolute(file.LastWriteUtc))),
            new DetailsFact(Strings.Details_Size, RelativeTime.Size(file.SizeBytes)),
        ];
    }

    public static IReadOnlyList<DetailsFact> ForApplication(ApplicationDefinition application, int folderCount, int problemCount)
    {
        var facts = new List<DetailsFact>();
        Add(facts, Strings.Details_Owner, application.Owner);
        Add(facts, Strings.Details_Description, application.Description);
        Add(facts, Strings.Details_Tags, application.Tags.IsDefaultOrEmpty ? null : string.Join(", ", application.Tags));
        Add(facts, Strings.Details_Folders, folderCount.ToString(CultureInfo.CurrentCulture));
        Add(facts, Strings.Details_Problems, problemCount == 0 ? Strings.Details_NoProblems : Counts.Problems(problemCount));
        return facts;
    }

    private static void Add(List<DetailsFact> facts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            facts.Add(new DetailsFact(label, value));
        }
    }
}
