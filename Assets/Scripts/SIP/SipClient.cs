using SIPSorcery.SIP.App;
using SIPSorcery.SIP;
using System.Net;
using UnityEngine;
using SIPSorcery.Net;

public class SipClient : MonoBehaviour
{
    public UnitySipAudioSource unityAudioEndpoint;

    // SIP server details
    public string speakerIp = "172.168.10.111";
    private const string Username = "MaBoy";
    private string FromUri => $"sip:{Username}@{speakerIp}";
    private string TransfereeDst => $"sip:HikvisonDS-QAZ1325G1TEOLNetworkHornSpeaker25W@{speakerIp}";
    private RTPSession rtpSession;
    private SIPTransport sipTransport;
    private SIPClientUserAgent sipClientUserAgent;

    public void Call()
    {
        Hangup();

        sipTransport = new SIPTransport();
        sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, 6969)));
        rtpSession = new AudioSendMediaSession(unityAudioEndpoint);
        rtpSession.AcceptRtpFromAny = true;
        rtpSession.SetMediaStreamStatus(SDPMediaTypesEnum.audio, MediaStreamStatusEnum.SendOnly);
        rtpSession.OnStarted += () => Debug.Log("RTP Session STARTED");

        var offerSDP = rtpSession.CreateOffer(IPAddress.Parse(speakerIp));
        Debug.Log($"Offer created: {offerSDP}");

        sipClientUserAgent = new SIPClientUserAgent(sipTransport);
        SetupSipClientUACallbacks();

        sipTransport.SIPTransportRequestReceived += async (SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest) =>
        {
            Debug.Log($"{sipRequest.Method} {sipRequest.Body} \n{localSIPEndPoint} {remoteEndPoint}");

            SIPResponse okResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
            await sipTransport.SendResponseAsync(okResponse).ConfigureAwait(false);
            if (sipRequest.Method == SIPMethodsEnum.BYE && sipClientUserAgent.IsUACAnswered)
            {
                Debug.LogWarning("Call was hungup by remote server.");
            }
        };

        var callUri = SIPURI.ParseSIPURI(TransfereeDst);
        var callDescriptor = new SIPCallDescriptor(
            Username,
            null,
            callUri.ToString(),
            FromUri,
            callUri.CanonicalAddress,
            null,
            null,
            null,
            SIPCallDirection.Out,
            SDP.SDP_MIME_CONTENTTYPE,
            offerSDP.ToString(),
            null);

        sipClientUserAgent.Call(callDescriptor, null);
    }

    private void SetupSipClientUACallbacks()
    {
        sipClientUserAgent.CallTrying += (uac, resp) =>
        {
            Debug.Log($"{uac.CallDescriptor.To} Trying: {resp.StatusCode} {resp.ReasonPhrase}.");
        };
        sipClientUserAgent.CallRinging += async (uac, resp) =>
        {
            Debug.Log($"{uac.CallDescriptor.To} Ringing: {resp.StatusCode} {resp.ReasonPhrase}.");
            if (resp.Status == SIPResponseStatusCodesEnum.SessionProgress)
            {
                if (resp.Body != null)
                {
                    var result = rtpSession.SetRemoteDescription(SdpType.answer, SDP.ParseSDPDescription(resp.Body));
                    new SDP();
                    if (result == SetDescriptionResultEnum.OK)
                    {
                        await rtpSession.Start().ConfigureAwait(false);
                        Debug.Log($"Remote SDP set from in progress response. RTP session started.");
                    }
                }
            }
        };
        sipClientUserAgent.CallFailed += (uac, err, resp) =>
        {
            Debug.Log($"Call attempt to {uac.CallDescriptor.To} Failed: {err}");
        };
        sipClientUserAgent.CallAnswered += async (iuac, resp) =>
        {
            if (resp.Status == SIPResponseStatusCodesEnum.Ok)
            {
                Debug.Log($"{sipClientUserAgent.CallDescriptor.To} Answered: {resp.StatusCode} {resp.ReasonPhrase}.");

                if (resp.Body != null)
                {
                    // https://github.com/sipsorcery-org/sipsorcery/issues/1329
                    var answerSdp = SDP.ParseSDPDescription(resp.Body.Replace("m=audio 30000/2 RTP/AVP", "m=audio 30000 RTP/AVP"));
                    Debug.Log($"Answer: {resp.Body}");
                    var result = rtpSession.SetRemoteDescription(SdpType.answer, answerSdp);
                    Debug.Log(result);
                    if (result == SetDescriptionResultEnum.OK)
                    {
                        Debug.Log(rtpSession.AudioStreamList.Count);
                        await rtpSession.Start().ConfigureAwait(false);
                    }
                    else
                    {
                        Debug.LogError($"Failed to set remote description {result}.");
                        sipClientUserAgent.Hangup();
                    }
                }
                else if (!rtpSession.IsStarted)
                {
                    Debug.LogError($"Failed to set get remote description in session progress or final response.");
                    sipClientUserAgent.Hangup();
                }
            }
            else
            {
                Debug.Log($"{sipClientUserAgent.CallDescriptor.To} Answered: {resp.StatusCode} {resp.ReasonPhrase}.");
            }
        };
    }

    public void Hangup() => HangupAndDisposeCall();

    private void OnDestroy()
    {
        HangupAndDisposeCall();
    }

    private void HangupAndDisposeCall()
    {
        if (sipClientUserAgent != null)
        {
            if (sipClientUserAgent.IsUACAnswered)
                sipClientUserAgent.Hangup();
            sipClientUserAgent = null;
        }
        rtpSession?.Dispose();
        rtpSession = null;
        sipTransport?.Dispose();
        sipTransport = null;
    }
}
