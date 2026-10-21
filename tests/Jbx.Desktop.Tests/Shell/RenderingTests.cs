using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Jbx.Desktop.Features.FolderList;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Tests.Harness;
using Jbx.Platform;
using Jbx.Testing;
using Xunit;

namespace Jbx.Desktop.Tests.Shell;

/// <summary>
/// Renders the main states with the real renderer, in light and dark. Set JBX_SCREENSHOTS to a folder
/// to keep the frames for a visual review.
/// </summary>
public sealed class RenderingTests : IDisposable
{
    private readonly TestShell _shell = new();

    public void Dispose()
    {
        _shell.Dispose();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Renders_a_production_tab_with_an_open_folder_and_file_details_in_both_themes(string theme)
    {
        Application.Current!.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var document = TestConfigs.FullExample();
        _shell.AddConfig();
        _shell.RememberConfig();
        await _shell.Settings.LoadAsync(TestContext.Current.CancellationToken);
        await _shell.Settings.UpdateAsync(settings => settings with { LastEnvironmentId = "prod" }, TestContext.Current.CancellationToken);
        _shell.AddFoldersFor("prod", document);
        _shell.FileSystem.AddFolder(@"\\fs01.contoso.test\AppLogs\prod\BillingPortal\Api\logs", FolderBehaviour.Denied);
        _shell.FileSystem.AddFile(@"D:\Logs\prod\Orders\Scheduler\logs", "scheduler-2026-10-20.log", _shell.Time.GetUtcNow().AddMinutes(-3), 48_000);
        _shell.FileSystem.AddFile(@"D:\Logs\prod\Orders\Scheduler\logs", "scheduler-2026-10-19.log", _shell.Time.GetUtcNow().AddDays(-1), 1_200_000);
        var window = _shell.CreateWindow();
        window.Width = 1280;
        window.Height = 760;
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        var tab = _shell.ViewModel.SelectedTab!;
        await Eventually.TrueAsync(() => tab.ProgressText is null);
        tab.ExpandAllCommand.Execute(null);
        tab.ToggleExpandCommand.Execute(tab.Rows.OfType<FolderRowViewModel>().Single(row => row.Name == "Order Scheduler"));
        await Eventually.TrueAsync(() => tab.Rows.OfType<FileItemRowViewModel>().Count() == 2);
        tab.SelectedRow = tab.Rows.OfType<FileItemRowViewModel>().First();
        window.Settle();

        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        Save(frame!, $"tab-production-{theme}.png");
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Renders_a_log_with_levels_and_search_matches_in_both_themes(string theme)
    {
        Application.Current!.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        const string Folder = @"\\fs01.contoso.test\AppLogs\dev\BillingPortal\Api\logs";
        var file = _shell.Paths.From(Folder + @"\api-2026-10-20.log");
        _shell.AddConfig();
        _shell.RememberConfig();
        _shell.FileSystem.AddFolder(Folder);
        _shell.FileSystem.SetFileContent(file, System.Text.Encoding.UTF8.GetBytes(SampleLog()));
        var window = _shell.CreateWindow();
        window.Width = 1280;
        window.Height = 760;
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        var tab = _shell.ViewModel.SelectedTab!;
        tab.OpenInViewer(new Features.LogViewer.LogTarget(file, "api-2026-10-20.log", "Billing API · Billing Portal · Development"));
        var viewer = tab.SelectedDocument!;
        await window.SettleUntilAsync(() => viewer.IsReady);
        viewer.SearchText = "invoice";
        _shell.Time.Advance(Features.LogViewer.LogViewerViewModel.SearchDelay);
        await window.SettleUntilAsync(() => viewer.MatchText is not null);
        await window.SettleUntilAsync(() => Enumerable.Range(0, 20).All(index => viewer.Lines[index] is Features.LogViewer.LogLineViewModel { IsLoading: false }));

        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        Save(frame!, $"viewer-{theme}.png");
    }

    [AvaloniaFact]
    public async Task Follows_a_windows_contrast_theme_and_returns_to_the_normal_theme_when_it_is_turned_off()
    {
        var nightSky = new ContrastColors(0xFF000000, 0xFFFFFFFF, 0xFF8EE3F0, 0xFF263B50) { HotLight = 0xFF75E9FC, GrayText = 0xFFA6A6A6, ButtonFace = 0xFF000000, ButtonText = 0xFFFFFFFF };
        _shell.AddConfig();
        _shell.RememberConfig();
        _shell.AddFoldersFor("dev", TestConfigs.FullExample());
        var window = _shell.CreateWindow();
        window.Width = 1280;
        window.Height = 760;
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        var tab = _shell.ViewModel.SelectedTab!;
        await Eventually.TrueAsync(() => tab.ProgressText is null);
        tab.ExpandAllCommand.Execute(null);
        tab.SelectedRow = tab.Rows.OfType<FolderRowViewModel>().First();

        ContrastThemes.Apply(Application.Current!, nightSky);
        window.Settle();
        var frame = window.CaptureRenderedFrame();
        Application.Current!.TryGetResource("TextPrimaryBrush", ThemeVariant.Dark, out var textInContrast);
        ContrastThemes.Apply(Application.Current!, null);

        Assert.Equal(Colors.White, Assert.IsType<SolidColorBrush>(textInContrast).Color);
        Assert.Equal(ThemeVariant.Default, Application.Current!.RequestedThemeVariant);
        Save(frame!, "contrast-night-sky.png");
    }

    [AvaloniaFact]
    public async Task Renders_the_command_palette()
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        var window = _shell.CreateWindow();
        window.Width = 1280;
        window.Height = 760;
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        _shell.ViewModel.OpenPaletteCommand.Execute(null);
        _shell.ViewModel.Palette.FilterText = "o";
        window.Settle();

        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        Save(frame!, "palette.png");
    }

    [AvaloniaFact]
    public async Task Renders_the_first_run_state()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = _shell.CreateWindow();
        window.Width = 1100;
        window.Height = 680;
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        window.Settle();

        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        Save(frame!, "first-run.png");
    }

    private static string SampleLog()
    {
        string[] lines =
        [
            "2026-10-20 08:59:58.120 INFO  Billing.Api starting on port 5010",
            "2026-10-20 08:59:59.004 DEBUG Billing.Api loaded 14 routes",
            "2026-10-20 09:00:01.311 INFO  GET /invoices/20412 200 18ms",
            "2026-10-20 09:00:02.870 WARN  Slow response from \\\\fs01.contoso.test\\Statements (2,950 ms)",
            "2026-10-20 09:00:03.442 ERROR Payment run 7781 failed for invoice 20413",
            "System.TimeoutException: The operation has timed out.",
            "   at Billing.Payments.Runner.Execute(Int32 run) in Runner.cs:line 112",
            "   at Billing.Api.PaymentsController.Post() in PaymentsController.cs:line 48",
            "2026-10-20 09:00:04.019 INFO  Retrying payment run 7781 (attempt 2 of 3)",
            "2026-10-20 09:00:05.735 INFO  GET /invoices/20413 200 22ms",
        ];
        return string.Join('\n', Enumerable.Range(0, 6).SelectMany(_ => lines)) + "\n";
    }

    private static void Save(Avalonia.Media.Imaging.WriteableBitmap frame, string name)
    {
        var folder = Environment.GetEnvironmentVariable("JBX_SCREENSHOTS");
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
            frame.Save(Path.Combine(folder, name), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        }
    }
}
