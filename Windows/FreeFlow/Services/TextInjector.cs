using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using WindowsInput;

namespace FreeFlow.Services;

public class TextInjector
{
    private readonly InputSimulator _inputSimulator;

    private const uint WM_PASTE = 0x0302;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const int SW_SHOW = 5;

    public TextInjector()
    {
        _inputSimulator = new InputSimulator();
    }

    /// <summary>
    /// Forcefully sets the foreground window using the AttachThreadInput trick.
    /// Plain SetForegroundWindow only works once per session due to Windows restrictions.
    /// </summary>
    private bool ForceForegroundWindow(IntPtr hWnd)
    {
        IntPtr currentForeground = GetForegroundWindow();
        if (currentForeground == hWnd) return true;

        uint currentThreadId = GetCurrentThreadId();
        uint foregroundThreadId = GetWindowThreadProcessId(currentForeground, out _);
        uint targetThreadId = GetWindowThreadProcessId(hWnd, out _);

        bool attached = false;
        try
        {
            // Attach to the current foreground window's thread
            if (currentThreadId != foregroundThreadId)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, true);
                attached = true;
            }

            // Also attach to the target thread if different
            if (foregroundThreadId != targetThreadId)
            {
                AttachThreadInput(foregroundThreadId, targetThreadId, true);
            }

            ShowWindow(hWnd, SW_SHOW);
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
        }
        finally
        {
            // Detach threads
            if (currentThreadId != foregroundThreadId)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
            if (foregroundThreadId != targetThreadId)
            {
                AttachThreadInput(foregroundThreadId, targetThreadId, false);
            }
        }

        return GetForegroundWindow() == hWnd;
    }

    public async Task PasteTextAsync(string text, IntPtr targetWindowHandle)
    {
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            // Save current clipboard content
            IDataObject? oldData = null;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try { oldData = Clipboard.GetDataObject(); } catch { }
            });

            // Set new text to clipboard
            bool setSuccess = false;
            for (int i = 0; i < 10; i++)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        Clipboard.Clear();
                        Clipboard.SetText(text, TextDataFormat.UnicodeText);
                        setSuccess = true;
                    }
                    catch { }
                });
                if (setSuccess) break;
                await Task.Delay(100);
            }

            if (!setSuccess)
            {
                System.Diagnostics.Debug.WriteLine("Paste failed: Could not set clipboard text");
                return;
            }

            // Wait for clipboard to settle
            await Task.Delay(100);

            bool focusRestored = false;

            // Explicitly restore focus to the target window
            if (targetWindowHandle != IntPtr.Zero && IsWindow(targetWindowHandle))
            {
                focusRestored = ForceForegroundWindow(targetWindowHandle);
                System.Diagnostics.Debug.WriteLine($"ForceForegroundWindow result: {focusRestored} (target={targetWindowHandle}, current={GetForegroundWindow()})");
                await Task.Delay(200);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Target window handle invalid ({targetWindowHandle})");
            }

            // Release all modifiers that might be physically held
            var modifiers = new[]
            {
                VirtualKeyCode.LWIN, VirtualKeyCode.RWIN,
                VirtualKeyCode.SHIFT, VirtualKeyCode.LSHIFT, VirtualKeyCode.RSHIFT,
                VirtualKeyCode.CONTROL, VirtualKeyCode.LCONTROL, VirtualKeyCode.RCONTROL,
                VirtualKeyCode.MENU, VirtualKeyCode.LMENU, VirtualKeyCode.RMENU
            };
            foreach (var mod in modifiers) _inputSimulator.Keyboard.KeyUp(mod);

            // Wait for system to process key releases
            await Task.Delay(100);

            // Primary: simulate Ctrl+V
            _inputSimulator.Keyboard.KeyDown(VirtualKeyCode.CONTROL);
            await Task.Delay(50);
            _inputSimulator.Keyboard.KeyPress(VirtualKeyCode.VK_V);
            await Task.Delay(50);
            _inputSimulator.Keyboard.KeyUp(VirtualKeyCode.CONTROL);

            System.Diagnostics.Debug.WriteLine("Ctrl+V simulated");

            // Fallback: if focus wasn't confirmed, also send WM_PASTE directly
            if (!focusRestored && targetWindowHandle != IntPtr.Zero && IsWindow(targetWindowHandle))
            {
                await Task.Delay(300);

                // Check if the Ctrl+V worked by seeing if target now has focus
                if (GetForegroundWindow() != targetWindowHandle)
                {
                    System.Diagnostics.Debug.WriteLine("Ctrl+V likely missed target, sending WM_PASTE directly");
                    SendMessage(targetWindowHandle, WM_PASTE, IntPtr.Zero, IntPtr.Zero);
                }
            }

            // Wait for the app to process the paste before restoring clipboard
            await Task.Delay(1000);

            // Restore clipboard
            if (oldData != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try { Clipboard.SetDataObject(oldData); } catch { }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Paste failed: {ex.Message}");
        }
    }
}
