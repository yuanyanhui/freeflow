using System;
using System.IO;
using NAudio.Wave;

namespace FreeFlow.Services;

public class AudioRecorderService : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _tempFilePath;

    public float CurrentLevel { get; private set; }
    public event Action<float>? OnLevelChanged;

    public void StartRecording(int deviceNumber = 0)
    {
        StopRecording();

        _tempFilePath = Path.Combine(Path.GetTempPath(), $"freeflow_{Guid.NewGuid()}.wav");
        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(44100, 1) // 44.1kHz mono
        };

        _waveIn.DataAvailable += (s, e) =>
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);

            // Calculate RMS level for visual feedback
            float max = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
                float sample32 = sample / 32768f;
                if (sample32 < 0) sample32 = -sample32;
                if (sample32 > max) max = sample32;
            }
            CurrentLevel = max;
            OnLevelChanged?.Invoke(max);
        };

        _writer = new WaveFileWriter(_tempFilePath, _waveIn.WaveFormat);
        _waveIn.StartRecording();
    }

    public string? StopRecording()
    {
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;

        _writer?.Dispose();
        _writer = null;

        return _tempFilePath;
    }

    public void Dispose()
    {
        StopRecording();
    }

    public static WaveInCapabilities[] GetInputDevices()
    {
        int count = WaveIn.DeviceCount;
        var devices = new WaveInCapabilities[count];
        for (int i = 0; i < count; i++)
        {
            devices[i] = WaveIn.GetCapabilities(i);
        }
        return devices;
    }
}
