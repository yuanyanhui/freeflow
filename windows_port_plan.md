This is a comprehensive implementation plan to port the **FreeFlow** macOS app to Windows.

Based on the original repository, FreeFlow is a "context-aware" voice-to-text tool. It works by:
1.  **Recording audio** while a key is held.
2.  **Capturing context** (a screenshot of the active window) to help the AI understand what you are doing.
3.  **Sending both** to an AI inference API (Groq).
4.  **Pasting the transcribed text** into the active text field.

To port this to Windows, we will use **C# with .NET 8 (WPF)**. This stack offers the best balance of modern UI, easy access to native Windows APIs (Win32), and strong performance.

---

### **Phase 1: Technology Stack & Prerequisites**

*   **Language:** C# (.NET 8.0 or later)
*   **Framework:** WPF (Windows Presentation Foundation)
    *   *Why?* It allows for invisible windows (background app), system tray icons, and easy interop with Win32 APIs which are essential for global hotkeys and text injection.
*   **Key Libraries (NuGet Packages):**
    *   `NAudio`: For microphone recording and audio processing.
    *   `H.InputSimulator` or `PInvoke.User32`: For simulating keystrokes (Ctrl+V) to paste text.
    *   `System.Drawing.Common` or `Windows.Graphics.Capture`: For taking screenshots.
    *   `GlobalHotKey` or custom `SetWindowsHookEx` wrapper: To detect the "push-to-talk" key interaction.

---

### **Phase 2: Core Architecture**

The app will run as a **System Tray Application** (no main window visible by default).

#### **1. Application Entry Point (System Tray)**
*   **Goal:** The app should start silently and sit in the notification area (system tray).
*   **Implementation:**
    *   Remove `StartupUri` from `App.xaml`.
    *   In `OnStartup`, create a `NotifyIcon` (using `System.Windows.Forms` or a WPF library like `Hardcodet.NotifyIcon`).
    *   Add a Context Menu: *Settings, View Logs, Quit*.

#### **2. Global "Push-to-Talk" Hook**
*   **Challenge:** The Mac app uses the `Fn` key. On Windows, the `Fn` key is handled by the BIOS/firmware and is often invisible to the OS.
*   **Solution:** Use a universally accessible key like **Right Alt**, **Caps Lock**, or allow the user to bind a custom key (e.g., `F1`).
*   **Implementation:**
    *   Use `SetWindowsHookEx` (User32.dll) to install a low-level keyboard hook (`WH_KEYBOARD_LL`).
    *   **Logic:**
        *   `OnKeyDown`: If (HotKey) AND (Not Recording) -> Start Recording.
        *   `OnKeyUp`: If (HotKey) AND (Recording) -> Stop Recording -> Trigger Transcription Pipeline.

#### **3. Audio Recording**
*   **Goal:** Record microphone input to a temporary WAV/MP3 file.
*   **Implementation:**
    *   Use `NAudio.WaveInEvent`.
    *   Start recording on `OnKeyDown`.
    *   Stop recording on `OnKeyUp` and save the buffer to a temporary file (e.g., `temp_audio.wav`).

#### **4. Context Capture (The "Deep Context" Feature)**
*   **Goal:** Capture the active window to send to the AI for context (e.g., to see who you are replying to).
*   **Implementation:**
    *   Get the handle of the foreground window: `GetForegroundWindow()`.
    *   Get window bounds: `GetWindowRect()`.
    *   Capture screenshot:
        *   *Simple:* `Graphics.CopyFromScreen` (System.Drawing).
        *   *Robust:* `BitBlt` (Win32) to handle different DPI settings correctly.
    *   Convert the image to base64 to send with the API request.

---

### **Phase 3: The Transcription Pipeline**

This is the logic that runs immediately after the user releases the hotkey.

1.  **Prepare Payload:**
    *   Read the audio file bytes.
    *   Read the screenshot base64 string.
    *   Construct the prompt (similar to the original repo's prompts).
2.  **API Call (Groq/OpenAI):**
    *   Use `HttpClient` to POST to the Groq API (or OpenAI compatible endpoint).
    *   *Endpoint:* Typically `v1/chat/completions` (for Vision/Context) or `v1/audio/transcriptions` (if just audio).
    *   *Note:* FreeFlow uses a "Vision" model (like Llama 3.2 Vision on Groq) to analyze the screenshot *and* the audio transcript together.
3.  **Receive Text:**
    *   Parse the JSON response to extract the clean text.

---

### **Phase 4: Text Injection (The "Paste" Action)**

*   **Goal:** Insert the transcribed text into the user's active application.
*   **Implementation:**
    1.  **Clipboard Method (Most Reliable):**
        *   Save current Clipboard content (to restore later).
        *   Set Clipboard text to the AI response: `Clipboard.SetText(response)`.
        *   Simulate `Ctrl + V` using `SendInput` (Win32) or `InputSimulator`.
        *   *Wait 50-100ms.*
        *   Restore original Clipboard content.
    2.  **SendKeys Method (Fallback):**
        *   If the application doesn't support paste, simulate typing characters. (Slower and less reliable for emojis/unicode).

---

### **Phase 5: Settings & UI**

Create a simple WPF Window (`SettingsWindow.xaml`) accessible from the Tray Icon.

*   **API Key Field:** Input for Groq API Key (save securely using `Properties.Settings` or Windows Credential Locker).
*   **Prompt Customization:** Allow users to edit the system prompt (e.g., "You are a helpful assistant that fixes grammar...").
*   **Microphone Selector:** Dropdown to choose input device (enumerate via NAudio).
*   **Hotkey Selector:** UI to bind a different key.

---

### **Step-by-Step Porting Checklist**

1.  **Project Setup:**
    *   [ ] Create new WPF .NET 8 Application.
    *   [ ] Add NuGet packages: `NAudio`, `Newtonsoft.Json` (or System.Text.Json), `H.InputSimulator`.

2.  **Core Systems:**
    *   [ ] Implement `KeyboardHookService` class (Win32 `SetWindowsHookEx`).
    *   [ ] Implement `AudioRecorderService` class (NAudio).
    *   [ ] Implement `ScreenCaptureService` class (System.Drawing/BitBlt).

3.  **Business Logic:**
    *   [ ] Create `GroqClient` to handle the specific JSON payload format used by FreeFlow.
    *   [ ] Orchestrate the "Hold -> Record -> Release -> Upload -> Paste" flow.

4.  **UI & Polish:**
    *   [ ] Add System Tray icon.
    *   [ ] Create Settings Window.
    *   [ ] Add "Visual Feedback" (e.g., a small floating dot or border color change) so the user knows recording is active. *Note: In WPF, use a `Window` with `AllowsTransparency=True`, `WindowStyle=None`, `Topmost=True`.*

5.  **Deployment:**
    *   [ ] Create an installer. For open-source projects, **Squirrel.Windows** or a simple **Inno Setup** script is recommended to generate an `.exe` installer.

### **Key Differences from Mac Version**
| Feature | Mac Implementation | Windows Implementation |
| :--- | :--- | :--- |
| **Hotkey** | `CGEventTap` (Fn Key) | `SetWindowsHookEx` (Use R-Alt or F1) |
| **Context** | `CGWindowListCreateImage` | `GetForegroundWindow` + `BitBlt` |
| **Paste** | Accessibility API / Event injection | `SendInput` (Ctrl+V simulation) |
| **Audio** | AVFoundation | NAudio / WASAPI |
