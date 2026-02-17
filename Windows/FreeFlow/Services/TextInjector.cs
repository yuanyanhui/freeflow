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

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    private const int SW_SHOW = 5;

    public TextInjector()
    {
        _inputSimulator = new InputSimulator();
    }

    /// <summary>
    /// Attaches to the target window's thread, gets the focused child control,
    /// brings the window to foreground, and returns the focused child handle.
    /// </summary>
    private (bool focusRestored, IntPtr focusedChild) ForceForegroundAndGetFocus(IntPtr hWnd)
    {
        IntPtr currentForeground = GetForegroundWindow();
        uint currentThreadId = GetCurrentThreadId();
        uint targetThreadId = GetWindowThreadProcessId(hWnd, out _);
        uint foregroundThreadId = GetWindowThreadProcessId(currentForeground, out _);
        IntPtr focusedChild = IntPtr.Zero;

        // Track which thread attachments we made so we can clean up
        bool attachedCurrentToForeground = false;
        bool attachedCurrentToTarget = false;

        try
        {
            // Attach our thread to the target window's thread so GetFocus works
            if (currentThreadId != targetThreadId)
            {
                AttachThreadInput(currentThreadId, targetThreadId, true);
                attachedCurrentToTarget = true;
            }

            // Also attach to the current foreground's thread if different
            if (currentThreadId != foregroundThreadId && foregroundThreadId != targetThreadId)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, true);
                attachedCurrentToForeground = true;
            }

            // Get the focused child control (e.g., Scintilla in Notepad++)
            focusedChild = GetFocus();
            System.Diagnostics.Debug.WriteLine($"GetFocus returned: {focusedChild}");

            // Now bring the target to foreground
            ShowWindow(hWnd, SW_SHOW);
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
        }
        finally
        {
            if (attachedCurrentToTarget)
                AttachThreadInput(currentThreadId, targetThreadId, false);
            if (attachedCurrentToForeground)
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }

        bool focusRestored = GetForegroundWindow() == hWnd;
        return (focusRestored, focusedChild);
    }

    public async Task PasteTextAsync(string text, IntPtr targetWindowHandle)
    {
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            // Set text to clipboard
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
            IntPtr focusedChild = IntPtr.Zero;

            if (targetWindowHandle != IntPtr.Zero && IsWindow(targetWindowHandle))
            {
                (focusRestored, focusedChild) = ForceForegroundAndGetFocus(targetWindowHandle);
                System.Diagnostics.Debug.WriteLine($"ForceForeground: restored={focusRestored}, child={focusedChild}, target={targetWindowHandle}, current={GetForegroundWindow()}");
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
            await Task.Delay(100);

            // Strategy 1: Send WM_PASTE directly to the focused child control.
            // This works even without foreground focus and targets the correct
            // child window (e.g., Scintilla editor inside Notepad++).
            IntPtr pasteTarget = (focusedChild != IntPtr.Zero && IsWindow(focusedChild))
                ? focusedChild
                : targetWindowHandle;

            if (pasteTarget != IntPtr.Zero && IsWindow(pasteTarget))
            {
                System.Diagnostics.Debug.WriteLine($"Sending WM_PASTE to {pasteTarget}");
                SendMessage(pasteTarget, WM_PASTE, IntPtr.Zero, IntPtr.Zero);
            }

            // Strategy 2: Also simulate Ctrl+V as belt-and-suspenders.
            // If the window is now in foreground, this will work too.
            await Task.Delay(100);
            _inputSimulator.Keyboard.KeyDown(VirtualKeyCode.CONTROL);
            await Task.Delay(50);
            _inputSimulator.Keyboard.KeyPress(VirtualKeyCode.VK_V);
            await Task.Delay(50);
            _inputSimulator.Keyboard.KeyUp(VirtualKeyCode.CONTROL);
            System.Diagnostics.Debug.WriteLine("Ctrl+V simulated");

            // Wait for paste to be processed
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Paste failed: {ex.Message}");
        }
    }
}

