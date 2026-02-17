# FreeFlow Windows Port Tasks

This document tracks the progress of porting FreeFlow from macOS to Windows.

## 1. Project Setup
- [x] Initialize WPF .NET 8 project (`FreeFlow.csproj`) in `Windows/`.
- [x] Add NuGet dependencies:
    - `NAudio` (Audio recording)
    - `Newtonsoft.Json` (JSON parsing)
    - `H.InputSimulator` (Input simulation)
    - `System.Drawing.Common` (Graphics/Screen capture)
    - `Hardcodet.NotifyIcon.Wpf` (Tray icon)
- [x] Configure `App.xaml` for tray-only startup (no initial main window).

## 2. Global Hotkey System
- [x] Implement `LowLevelKeyboardHook` using `SetWindowsHookEx`.
- [x] Implement `HotkeyManager` (integrated into `App.xaml.cs`) to detect Press and Release events of a target key (default: Right Alt).

## 3. Audio Recording
- [x] Implement `AudioRecorder` using `NAudio`.
- [x] Support selecting input devices.
- [x] Save recorded audio to temporary files.

## 4. Context Capture
- [x] Implement `ScreenCaptureService` to capture the active window.
- [x] Implement metadata extraction (Active window title).

## 5. AI Transcription & Post-processing
- [x] Implement `GroqClient` for:
    - Audio Transcription (Whisper-3).
    - Context-aware post-processing (Vision/LLM).
- [x] Implement prompt engineering similar to the macOS version.

## 6. Text Injection
- [x] Implement `TextInjector` to:
    - Store current clipboard.
    - Copy transcribed text to clipboard.
    - Simulate `Ctrl + V`.
    - Restore original clipboard.

## 7. UI Components
- [x] **System Tray**: Implementation with "Settings" and "Exit".
- [x] **Settings Window**:
    - Groq API Key input (with secure storage in Credential Locker).
    - Microphone selection.
    - Custom Vocabulary.
- [x] **Recording Overlay**: A small, non-intrusive UI element that appears while recording (similar to the macOS notch overlay).

## 8. Integration & Refinement
- [x] Orchestrate the "Hold to Record -> Process -> Paste" loop.
- [ ] Add sound effects (similar to Mac version).
- [x] Handle error states and notifications.

## 9. Deployment
- [x] Create GitHub Action workflow to build and package the app as a portable `.zip`.
