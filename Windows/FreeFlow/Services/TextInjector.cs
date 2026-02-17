using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using WindowsInput;

namespace FreeFlow.Services;

public class TextInjector
{
    private readonly InputSimulator _inputSimulator;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    public TextInjector()
    {
        _inputSimulator = new InputSimulator();
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

            // Explicitly restore focus to the target window
            if (targetWindowHandle != IntPtr.Zero && IsWindow(targetWindowHandle))
            {
                SetForegroundWindow(targetWindowHandle);
                System.Diagnostics.Debug.WriteLine($"Restored focus to window handle: {targetWindowHandle}");
                await Task.Delay(200); // Give the target window time to fully activate
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Target window handle invalid ({targetWindowHandle}), pasting to current foreground window");
            }

            System.Diagnostics.Debug.WriteLine($"Current foreground window: {GetForegroundWindow()}");

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

            // Simulate Ctrl+V with explicit delays
            _inputSimulator.Keyboard.KeyDown(VirtualKeyCode.CONTROL);
            await Task.Delay(50);
            _inputSimulator.Keyboard.KeyPress(VirtualKeyCode.VK_V);
            await Task.Delay(50);
            _inputSimulator.Keyboard.KeyUp(VirtualKeyCode.CONTROL);

            System.Diagnostics.Debug.WriteLine("Ctrl+V simulated successfully");

            // Wait longer for the app to process the paste before restoring clipboard
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
