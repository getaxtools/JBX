namespace Jbx.Services.ConfigSession;

public enum ConfigOpenStatus
{
    Opened,
    NothingRemembered,
    InvalidPath,
    NotFound,
    Denied,
    Timeout,
    Unreadable,
    TooLarge,
    Invalid,
}
