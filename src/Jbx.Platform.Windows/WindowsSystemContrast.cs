using System.Runtime.InteropServices;

namespace Jbx.Platform.Windows;

/// <summary>Reads whether a Windows contrast theme is on and, if so, its system colours.</summary>
public sealed partial class WindowsSystemContrast : ISystemContrast
{
    private const uint GetHighContrast = 0x0042;
    private const uint HighContrastOn = 0x0001;
    private const int ColorWindow = 5;
    private const int ColorWindowText = 8;
    private const int ColorHighlight = 13;
    private const int ColorHighlightText = 14;
    private const int ColorButtonFace = 15;
    private const int ColorGrayText = 17;
    private const int ColorButtonText = 18;
    private const int ColorHotLight = 26;

    public ContrastColors? Current
    {
        get
        {
            var settings = new HighContrast { Size = (uint)Marshal.SizeOf<HighContrast>() };
            if (!SystemParametersInfo(GetHighContrast, settings.Size, ref settings, 0) || (settings.Flags & HighContrastOn) == 0)
            {
                return null;
            }

            return new ContrastColors(Color(ColorWindow), Color(ColorWindowText), Color(ColorHighlight), Color(ColorHighlightText))
            {
                HotLight = Color(ColorHotLight),
                GrayText = Color(ColorGrayText),
                ButtonFace = Color(ColorButtonFace),
                ButtonText = Color(ColorButtonText),
            };
        }
    }

    /// <summary>GetSysColor answers 0x00BBGGRR; the rest of the program uses 0xAARRGGBB.</summary>
    private static uint Color(int index)
    {
        var value = GetSysColor(index);
        var red = value & 0xFF;
        var green = (value >> 8) & 0xFF;
        var blue = (value >> 16) & 0xFF;
        return 0xFF000000 | (red << 16) | (green << 8) | blue;
    }

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint parameter, ref HighContrast value, uint winIni);

    [LibraryImport("user32.dll")]
    private static partial uint GetSysColor(int index);

    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrast
    {
        public uint Size;
        public uint Flags;
        public IntPtr DefaultScheme;
    }
}
