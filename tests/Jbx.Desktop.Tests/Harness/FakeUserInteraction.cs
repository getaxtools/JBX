using Jbx.Core.Config;
using Jbx.Desktop.Features.Dialogs;
using Jbx.Desktop.Infrastructure;

namespace Jbx.Desktop.Tests.Harness;

internal sealed class FakeUserInteraction : IUserInteraction
{
    public string? ConfigToPick { get; set; }

    public string? EnvironmentToPick { get; set; }

    public string? TextToEnter { get; set; }

    public List<ConfigProblemsViewModel> ShownProblems { get; } = [];

    public List<string> CopiedTexts { get; } = [];

    public Task<string?> PickConfigFileAsync()
    {
        return Task.FromResult(ConfigToPick);
    }

    public Task<DeploymentEnvironment?> PickEnvironmentAsync(IReadOnlyList<DeploymentEnvironment> environments, string? suggestedId)
    {
        return Task.FromResult(environments.FirstOrDefault(environment => environment.Id == EnvironmentToPick));
    }

    public Task ShowConfigProblemsAsync(ConfigProblemsViewModel problems)
    {
        ShownProblems.Add(problems);
        return Task.CompletedTask;
    }

    public Task<string?> AskForTextAsync(TextPromptViewModel prompt)
    {
        return Task.FromResult(TextToEnter);
    }

    public Task CopyTextAsync(string text)
    {
        CopiedTexts.Add(text);
        return Task.CompletedTask;
    }

    public int AboutShownCount { get; private set; }

    public Task ShowAboutAsync()
    {
        AboutShownCount++;
        return Task.CompletedTask;
    }

    public Task<bool?> SaveTextAsync(string suggestedFileName, string text)
    {
        SavedTexts.Add(text);
        return Task.FromResult<bool?>(true);
    }

    public List<string> SavedTexts { get; } = [];
}
