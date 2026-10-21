namespace Jbx.Core.Diagnostics;

public enum EventCode
{
    AppStarted = 1,
    AppExiting = 2,

    ConfigLoaded = 100,
    ConfigReadFailed = 101,
    ConfigTooLarge = 102,
    ConfigParseFailed = 103,
    ConfigValidationFailed = 104,
    ConfigChangedOnDisk = 105,

    SettingsLoaded = 200,
    SettingsSetAside = 201,
    SettingsWriteFailed = 202,

    ProbeCompleted = 300,
    ProbeTimedOut = 301,
    ProbeFailed = 302,
    ListingTruncated = 310,
    ListingTimedOut = 311,

    LogOpened = 320,
    LogReadTimedOut = 321,
    LogReadFailed = 322,
    LogIndexCapped = 323,
    LogTruncated = 324,

    ShellLaunchFailed = 400,

    UnhandledException = 900,
}
