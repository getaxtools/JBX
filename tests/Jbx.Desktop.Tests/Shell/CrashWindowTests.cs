using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Jbx.Core.Diagnostics;
using Jbx.Desktop.Features.Crash;
using Jbx.Desktop.Tests.Harness;
using Xunit;

namespace Jbx.Desktop.Tests.Shell;

public sealed class CrashWindowTests : IDisposable
{
    private readonly TestShell _shell = new();

    public void Dispose()
    {
        _shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Shows_the_report_it_would_share_and_copies_it_exactly()
    {
        var report = CrashReportBuilder.Build(new InvalidOperationException(@"\\fs01.contoso.test\secret"), "0.1.0", DateTimeOffset.UnixEpoch);
        var model = new CrashViewModel(report, _shell.Interaction);
        var window = new CrashWindow { DataContext = model };
        window.Show();
        window.Settle();

        await model.CopyCommand.ExecuteAsync(null);

        Assert.Equal(report, window.ByAutomationId<TextBox>("Crash.Report").Text);
        Assert.Equal(report, Assert.Single(_shell.Interaction.CopiedTexts));
        Assert.DoesNotContain("secret", report, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task F1_opens_about_and_the_shortcut_list()
    {
        var window = _shell.CreateWindow();
        window.Show();
        await _shell.ViewModel.InitializeAsync();

        window.Press(Key.F1);

        Assert.Equal(1, _shell.Interaction.AboutShownCount);
    }
}
