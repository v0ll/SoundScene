using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SoundScene.App.Services;

/// <summary>
/// Gives a window the dark title bar so it does not look like a white strip
/// glued onto a dark app.
/// </summary>
internal static class DarkTitleBar
{
    private const int UseImmersiveDarkMode = 20;

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            var enabled = 1;

            // Best effort: older Windows builds reject the attribute and keep
            // the light title bar, which is cosmetic only.
            _ = DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref enabled, sizeof(int));
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
