using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using FreeFlow.Services;
using FreeFlow.Views;
using Hardcodet.Wpf.TaskbarNotification;
using System.Drawing;

namespace FreeFlow;

public partial class App : Application
{
    private LowLevelKeyboardHook? _keyboardHook;
    private AudioRecorderService? _audioRecorder;
    private ScreenCaptureService? _screenCapture;
    private TextInjector? _textInjector;
    private CredentialService? _credentialService;
    private SettingsService? _settingsService;
    private TaskbarIcon? _notifyIcon;
    private RecordingOverlay? _overlay;

    private bool _isRecording = false;
    private string? _lastContextSummary;
    private string? _lastScreenshotBase64;
    private IntPtr _targetWindowHandle = IntPtr.Zero;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        InitializeServices();
        InitializeTray();
        InitializeHotkey();
    }

    private void InitializeServices()
    {
        _audioRecorder = new AudioRecorderService();
        _screenCapture = new ScreenCaptureService();
        _textInjector = new TextInjector();
        _credentialService = new CredentialService();
        _settingsService = new SettingsService();
    }

    private bool _isWinPressed = false;
    private bool _isJPressed = false;

    private void InitializeTray()
    {
        _notifyIcon = new TaskbarIcon();
        // Use a system icon as fallback
        _notifyIcon.Icon = SystemIcons.Application;
        _notifyIcon.ToolTipText = "FreeFlow - Hold Win + J to record";

        var contextMenu = new System.Windows.Controls.ContextMenu();

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settingsItem.Click += (s, e) => new SettingsWindow().Show();

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (s, e) => Shutdown();

        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new System.Windows.Controls.Separator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenu = contextMenu;
    }

    private void InitializeHotkey()
    {
        _keyboardHook = new LowLevelKeyboardHook();
        _keyboardHook.OnKeyDown += OnKeyDown;
        _keyboardHook.OnKeyUp += OnKeyUp;
    }

    private void OnKeyDown(Key key)
    {
        if (key == Key.LWin || key == Key.RWin)
        {
            _isWinPressed = true;
            // Capture the foreground window the INSTANT Win is pressed,
            // before Windows has any chance to activate Start menu or change focus
            if (!_isRecording)
            {
                _targetWindowHandle = GetForegroundWindow();
                System.Diagnostics.Debug.WriteLine($"Captured target window handle on Win press: {_targetWindowHandle}");
            }
        }
        if (key == Key.J) _isJPressed = true;

        if (_isWinPressed && _isJPressed && !_isRecording)
        {
            StartRecordingFlow();
        }
    }

    private void OnKeyUp(Key key)
    {
        if (key == Key.LWin || key == Key.RWin) _isWinPressed = false;
        if (key == Key.J) _isJPressed = false;

        if ((!_isWinPressed || !_isJPressed) && _isRecording)
        {
            StopRecordingFlow();
        }
    }

    private void StartRecordingFlow()
    {
        _isRecording = true;

        // Capture context immediately
        _lastContextSummary = _screenCapture?.GetActiveWindowTitle();
        _lastScreenshotBase64 = _screenCapture?.CaptureActiveWindowAsBase64();

        // Start audio
        _audioRecorder?.StartRecording(_settingsService?.CurrentSettings.SelectedMicrophoneIndex ?? 0);

        // Show overlay
        Dispatcher.Invoke(() =>
        {
            _overlay = new RecordingOverlay();
            _overlay.Show();
        });
    }

    private async void StopRecordingFlow()
    {
        _isRecording = false;

        // Update overlay
        Dispatcher.Invoke(() => _overlay?.SetStatus("Transcribing..."));

        // Stop audio
        string? audioPath = _audioRecorder?.StopRecording();

        if (string.IsNullOrEmpty(audioPath))
        {
            Dispatcher.Invoke(() => _overlay?.Close());
            return;
        }

        try
        {
            string? apiKey = _credentialService?.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("Please set your Groq API Key in Settings.", "FreeFlow", MessageBoxButton.OK, MessageBoxImage.Warning);
                Dispatcher.Invoke(() => _overlay?.Close());
                return;
            }

            var groqClient = new GroqClient(apiKey);

            // 1. Transcribe
            string rawTranscript = await groqClient.TranscribeAsync(audioPath);
            System.Diagnostics.Debug.WriteLine($"Raw transcript: {rawTranscript}");

            if (string.IsNullOrWhiteSpace(rawTranscript))
            {
                Dispatcher.Invoke(() => _overlay?.Close());
                return;
            }

            // 2. Post-process
            string finalTranscript = rawTranscript;
            if (_settingsService?.CurrentSettings.IsPostProcessingEnabled == true)
            {
                Dispatcher.Invoke(() => _overlay?.SetStatus("Processing..."));
                finalTranscript = await groqClient.PostProcessAsync(
                    rawTranscript,
                    _lastContextSummary ?? "",
                    _lastScreenshotBase64,
                    _settingsService?.CurrentSettings.CustomVocabulary ?? "");
            }
            System.Diagnostics.Debug.WriteLine($"Final transcript: {finalTranscript}");

            // Close overlay BEFORE pasting to ensure focus returns to target app
            Dispatcher.Invoke(() => _overlay?.Close());
            await Task.Delay(150); // Give OS time to restore focus

            // 3. Paste
            if (!string.IsNullOrEmpty(finalTranscript) && _textInjector != null)
            {
                await _textInjector.PasteTextAsync(finalTranscript, _targetWindowHandle);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}\n{ex.StackTrace}", "FreeFlow Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            // Just in case it wasn't closed in the try block
            Dispatcher.Invoke(() => _overlay?.Close());
            if (System.IO.File.Exists(audioPath))
            {
                try { System.IO.File.Delete(audioPath); } catch { }
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _keyboardHook?.Dispose();
        _audioRecorder?.Dispose();
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }
}
