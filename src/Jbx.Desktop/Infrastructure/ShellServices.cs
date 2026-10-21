using Jbx.Core.Diagnostics;
using Jbx.Core.Templating;
using Jbx.Platform;
using Jbx.Services.ConfigSession;
using Jbx.Services.Favourites;
using Jbx.Services.Logs;
using Jbx.Services.Probing;
using Jbx.Services.Settings;

namespace Jbx.Desktop.Infrastructure;

internal sealed record ShellServices(
    ConfigSessionService ConfigSession,
    SettingsService Settings,
    FavouritesService Favourites,
    ReachabilityProbe Probe,
    FolderListingService Listing,
    PathTemplateResolver Resolver,
    IPathCanonicalizer Paths,
    IShell Shell)
{
    public required IUserInteraction Interaction { get; init; }

    public required IUiDispatcher Dispatcher { get; init; }

    public required TimeProvider Time { get; init; }

    public required IDiagnosticLog DiagnosticLog { get; init; }

    public required LogFileReader Logs { get; init; }
}
