using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;

namespace FreeFlow.Services;

public class ScreenCaptureService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    public string GetActiveWindowTitle()
    {
        var handle = GetForegroundWindow();
        var title = new System.Text.StringBuilder(256);
        GetWindowText(handle, title, 256);
        return title.ToString();
    }

    public byte[]? CaptureActiveWindow()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero) return null;

        if (!GetWindowRect(handle, out var rect)) return null;

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0) return null;

        using var bitmap = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Jpeg);
        return ms.ToArray();
    }

    public string? CaptureActiveWindowAsBase64()
    {
        var bytes = CaptureActiveWindow();
        if (bytes == null) return null;
        return Convert.ToBase64String(bytes);
    }
}
