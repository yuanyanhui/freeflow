# FreeFlow Project Overview

FreeFlow is a free, open-source, and privacy-focused alternative to paid AI dictation tools like Wispr Flow, Superwhisper, and Monologue. It provides context-aware voice-to-text transcription by leveraging the Groq API for high-speed inference.

## Architecture & Core Features

- **Cross-Platform:** Originally developed for macOS (Swift), now ported to Windows (C# / .NET 8 / WPF).
- **Push-to-Talk:** Activates transcription while a hotkey is held down (Default: `Fn` on macOS, `Right Alt` on Windows).
- **Deep Context:** Captures the active window's title and a screenshot to provide context to the LLM, helping it correctly spell names, technical terms, and follow the conversation flow.
- **Transcription Pipeline:**
    1. **Record:** Audio is captured using `AVFoundation` (macOS) or `NAudio` (Windows).
    2. **Context:** A screenshot and active window info are gathered.
    3. **Transcribe:** Audio is sent to Groq's `whisper-large-v3` model.
    4. **Post-Process:** The raw transcript and context are sent to a Groq LLM (e.g., `llama-3.2-90b-vision-preview` or `llama-4-scout-17b-16e-instruct`) for cleaning and formatting.
    5. **Inject:** The final text is pasted into the active application using accessibility APIs (macOS) or clipboard/keystroke simulation (Windows).

## Project Structure

### Windows (`/Windows/FreeFlow/`)
- **App.xaml.cs:** Entry point, tray icon management, and main orchestration of the recording flow.
- **Services/**:
    - `AudioRecorderService.cs`: Handles microphone input and WAV file generation using `NAudio`.
    - `GroqClient.cs`: Manages communication with the Groq API (transcription and chat completions).
    - `LowLevelKeyboardHook.cs`: Win32 API implementation for detecting global hotkeys.
    - `ScreenCaptureService.cs`: Captures the active window and converts it to Base64 for vision processing.
    - `TextInjector.cs`: Handles pasting text into external apps by simulating `Ctrl+V`.
    - `CredentialService.cs`: Manages secure storage of the Groq API key.
- **Views/**:
    - `RecordingOverlay.xaml`: A transparent, top-most window that provides visual feedback during recording and processing.
    - `SettingsWindow.xaml`: UI for configuring API keys, microphones, and custom vocabulary.

### macOS (`/macOS/`)
- **Sources/**: Swift-based implementation following a similar service-oriented architecture.
- **Resources/**: Application icons and demo assets.

## Tech Stack

- **Windows:** C# 12, .NET 8, WPF.
- **macOS:** Swift, SwiftUI.
- **AI Backend:** Groq Cloud API (Whisper + Llama).
- **Key Dependencies (Windows):** `NAudio`, `Hardcodet.NotifyIcon.Wpf`, `H.InputSimulator`, `Newtonsoft.Json`.

## Development Guidelines

- **Building:**
    - **Windows:** Use Visual Studio 2022 or the .NET CLI: `dotnet build Windows/FreeFlow/FreeFlow.csproj`.
    - **macOS:** Use the provided `Makefile` or open the project in Xcode.
- **Conventions:**
    - Use async/await for all I/O and API operations to keep the UI responsive.
    - Follow standard C#/.NET naming conventions for the Windows port.
    - Ensure low-level hooks are properly disposed of to avoid memory leaks or system instability.
- **Testing:**
    - Manual testing of the "Hold -> Speak -> Release -> Paste" flow in various applications (Notepad, Browser, Terminal).
    - Verify context awareness by dictating names visible on screen.

## Setup Requirements

1. **Groq API Key:** Required for transcription and LLM processing. Obtain from [console.groq.com](https://console.groq.com/).
2. **Windows:** .NET 8 SDK and Runtime.
3. **macOS:** Xcode and macOS 14.0+.
