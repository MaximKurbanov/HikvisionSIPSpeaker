using SIPSorcery.SIP.App;
using System.Net;
using UnityEngine;
using System.Threading.Tasks;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System.Collections.Generic;
using System.Linq;

public class AudioSendMediaSession : RTPSession, IMediaSession
{
    public IAudioSource AudioSource
    {
        get; private set;
    }

    public AudioSendMediaSession(
        IAudioSource audioSource,
        bool isMediaMultiplexed = false,
        bool isRtcpMultiplexed = false,
        IPAddress bindAddress = null,
        int bindPort = 0)
        : base(false, false, false, bindAddress, bindPort)
    {
        AudioSource = audioSource;
        AudioSource.OnAudioSourceEncodedSample += SendAudio;

        base.OnAudioFormatsNegotiated += AudioFormatsNegotiated;

        var audioTrack = new MediaStreamTrack(AudioSource.GetAudioSourceFormats());
        base.addTrack(audioTrack);
    }

    private void AudioFormatsNegotiated(List<AudioFormat> audioFormats)
    {
        var audioFormat = audioFormats.First();
        Debug.Log($"Setting audio source format to {audioFormat.FormatID}:{audioFormat.Codec}.");
        AudioSource.SetAudioSourceFormat(audioFormat);
    }

    public async override Task Start()
    {
        if (!base.IsStarted)
        {
            await base.Start().ConfigureAwait(true);
            await AudioSource.StartAudio().ConfigureAwait(true);
        }
    }

    public async override void Close(string reason)
    {
        if (!base.IsClosed)
        {
            base.Close(reason);

            AudioSource.OnAudioSourceEncodedSample -= SendAudio;
            await AudioSource.CloseAudio().ConfigureAwait(true);
        }
    }
}