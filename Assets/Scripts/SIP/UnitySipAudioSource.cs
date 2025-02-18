using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SIPSorceryMedia.Abstractions;
using SIPSorcery.Media;
using System.Threading;

public class UnitySipAudioSource : MonoBehaviour, IAudioSource
{
    public IAudioEncoder AudioEncoder
    {
        get; private set;
    }
    public event EncodedSampleDelegate OnAudioSourceEncodedSample;
    public event RawAudioSampleDelegate OnAudioSourceRawSample;
    public event SourceErrorDelegate OnAudioSourceError;

    public AudioSource audioSource;
    public int microphoneId = 0;
    public bool loopBackSound = false;
    public bool isStarted = false;
    public bool isPaused = false;

    private List<AudioFormat> audioSupportedFormats;
    private MediaFormatManager<AudioFormat> audioFormatManager;
    private const int ClipLength = 1; // 1 second buffer
    private string microphoneDevice;
    private AudioClip audioClip;
    private readonly bool debugLog = false;

    private SynchronizationContext synchronizationContext;

    private void Awake()
    {
        synchronizationContext = SynchronizationContext.Current;
        AudioEncoder = new AudioEncoder();
        audioSupportedFormats = new List<AudioFormat>()
        {
            new AudioFormat(SDPWellKnownMediaFormatsEnum.G722),
            new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMA),
            new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU)
        };
        audioFormatManager = new MediaFormatManager<AudioFormat>(audioSupportedFormats);
    }

    private void Log(string msg)
    {
        if (debugLog)
        {
            Debug.Log(msg);
        }
    }

    private void StartMicro()
    {
        if (Microphone.devices.Length > 0)
        {
            var format = audioFormatManager.SelectedFormat;
            var audioConfig = AudioSettings.GetConfiguration();
            audioConfig.sampleRate = format.ClockRate;
            audioConfig.speakerMode = AudioSpeakerMode.Mono;
            AudioSettings.Reset(audioConfig);

            microphoneDevice = Microphone.devices[microphoneId];
            audioClip = Microphone.Start(
                microphoneDevice,
                true,
                ClipLength,
                audioFormatManager.SelectedFormat.ClockRate);
            audioSource.clip = audioClip;
            audioSource.loop = true;
            audioSource.Play();
            Log("Microphone started: " + microphoneDevice);
        }
        else
        {
            Debug.LogError("Microphone not found!");
        }
    }

    private void OnDestroy()
    {
        StopMicro();
    }

    private void StopMicro()
    {
        if (microphoneDevice != null && Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }
    }

    private void RestartMicro()
    {
        StopMicro();
        StartMicro();
    }

    /// <summary>
    /// Captures audio data from Unity's audio pipeline.
    /// </summary>
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (isStarted && !isPaused)
        {
            // Convert float[] to byte[] (PCM format)
            var pcmData = ConvertFloatArrayToPCM(data);

            // Encode the PCM data
            var encodedSample = AudioEncoder.EncodeAudio(pcmData, audioFormatManager.SelectedFormat);

            // Raise the encoded sample event
            OnAudioSourceEncodedSample?.Invoke((uint)encodedSample.Length, encodedSample);
        }

        // Not 
        if (!loopBackSound)
        {
            Array.Clear(data, 0, data.Length);
        }
    }

    /// <summary>
    /// Converts float[] audio data to PCM format.
    /// </summary>
    private short[] ConvertFloatArrayToPCM(float[] data)
    {
        var pcmData = new short[data.Length]; // 16-bit PCM (2 bytes per sample)
        for (int i = 0; i < data.Length; i++)
        {
            pcmData[i] = (short)(data[i] * short.MaxValue);
        }
        return pcmData;
    }

    /// <summary>
    /// Starts the audio endpoint.
    /// </summary>
    public Task StartAudio()
    {
        synchronizationContext.Post(_ =>
        {
            Log("StartAudio");
            if (!isStarted)
            {
                isStarted = true;
                RestartMicro();
            }
        }, null);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Closes the audio endpoint.
    /// </summary>
    public Task CloseAudio()
    {
        synchronizationContext.Post(_ =>
        {
            Log("CloseAudio");
            if (isStarted)
            {
                isStarted = false;

                if (Microphone.IsRecording(microphoneDevice))
                {
                    Microphone.End(microphoneDevice);
                }
                audioSource.Stop();
            }
        }, null);
        return Task.CompletedTask;
    }

    public void ExternalAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
    {
        Log($"ExternalAudioSourceRawSample {samplingRate} {durationMilliseconds} {sample.Length}");
    }

    public bool HasEncodedAudioSubscribers() 
    {
        Log($"HasEncodedAudioSubs {OnAudioSourceEncodedSample != null}");
        return OnAudioSourceEncodedSample != null;
    }

    public bool IsAudioSourcePaused()
    {
        Log($"IsAudioSourcePaused = {isPaused} ");
        return isPaused;
    }

    public void RestrictFormats(Func<AudioFormat, bool> filter)
    {
        Log("RestrictFormats");
        audioFormatManager.RestrictFormats(filter);
    }

    public List<AudioFormat> GetAudioSourceFormats()
    {
        Log("GetAudioSourceFormats");
        var formats = audioFormatManager.GetSourceFormats();
        return formats;
    }

    public void SetAudioSourceFormat(AudioFormat audioFormat)
    {
        Log("SetAudioSourceFormat");
        audioFormatManager.SetSelectedFormat(audioFormat);
    }

    public Task PauseAudio()
    {
        synchronizationContext.Post(_ =>
        {
            Log("PauseAudio called.");
            isPaused = true;
        }, null);
        return Task.CompletedTask;
    }

    public Task ResumeAudio()
    {
        synchronizationContext.Post(_ =>
        {
            Log("ResumeAudio called.");
        }, null);
        isPaused = false;
        return Task.CompletedTask;
    }
}