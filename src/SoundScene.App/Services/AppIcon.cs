using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace SoundScene.App.Services;

/// <summary>
/// Draws the tray icon at runtime. Avoids shipping a .ico asset that has to be
/// regenerated at every size.
/// </summary>
internal static class AppIcon
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var background = new SolidBrush(Color.FromArgb(255, 27, 27, 31));
            graphics.FillEllipse(background, 0, 0, 31, 31);

            using var bar = new SolidBrush(Color.FromArgb(255, 76, 194, 255));
            int[] heights = [7, 13, 19, 11];
            for (var i = 0; i < heights.Length; i++)
            {
                var x = 7 + (i * 5);
                var height = heights[i];
                graphics.FillRectangle(bar, x, 16 - (height / 2), 3, height);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
