using System;
using System.Threading;
using System.Windows;
using WindowsInput;

namespace FreeFlow.Services;

public class TextInjector
{
    private readonly InputSimulator _inputSimulator;

    public TextInjector()
    {
        _inputSimulator = new InputSimulator();
    }

    public async Task PasteTextAsync(string text)
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

            if (!setSuccess) return;

            // Wait for clipboard to settle
            await Task.Delay(100);

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
