namespace Jbx.Platform.Windows;

public sealed class WindowsKnownFolders : IKnownFolders
{
    private const string PublisherFolderName = "getaxtools";
    private const string ProductFolderName = "JBX";
    private const string LogFolderName = "logs";

    public WindowsKnownFolders()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

        UserDataFolder = Path.Combine(localAppData, PublisherFolderName, ProductFolderName);
        DiagnosticLogFolder = Path.Combine(UserDataFolder, LogFolderName);
    }

    public string UserDataFolder { get; }

    public string DiagnosticLogFolder { get; }
}
