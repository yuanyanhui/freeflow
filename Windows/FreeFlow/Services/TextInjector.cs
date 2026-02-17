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
                        Clipboard.SetDataObject(text, true);
                        setSuccess = true;
                    }
                    catch { }
                });
                if (setSuccess) break;
                await Task.Delay(50);
            }

            if (!setSuccess) return;

            // Wait for clipboard to settle
            await Task.Delay(100);

            // Simulate Ctrl+V
            _inputSimulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);

            // Wait longer for the app to process the paste before restoring clipboard
            await Task.Delay(500);

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
