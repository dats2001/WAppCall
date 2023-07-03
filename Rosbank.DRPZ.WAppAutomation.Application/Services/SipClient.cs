using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Encoders;
using SIPSorceryMedia.Windows;
using System.Text.RegularExpressions;

namespace Rosbank.DRPZ.WAppAutomation.Application.Services;

public interface ISipClient
{ 
    /// <summary>
    /// Проверка на активность звонка
    /// </summary>
    bool IsCallActive { get; }

    /// <summary>
    /// Позвонить
    /// </summary>
    /// <param name="ext">номер</param>
    /// <returns></returns>
    Task CallAsync(string ext);

    /// <summary>
    /// Повесить трубку
    /// </summary>
    /// <returns></returns>
    Task HangUp();

    /// <summary>
    /// Позвонить и поставитьна удержание
    /// </summary>
    /// <param name="ext"></param>
    /// <returns></returns>
    Task CallAndHold(string ext);

    /// <summary>
    /// Возобновить разговор
    /// </summary>
    /// <returns></returns>
    Task ResumeCall();

    public event Action<ISipClient> CallAnswer;                 // Fires when an outgoing SIP call is answered.
    public event Action<ISipClient> RemotePutOnHold;            // Fires when the remote call party puts us on hold.	
    public event Action<ISipClient> RemoteTookOffHold;          // Fires when the remote call party takes us off hold.
}

public class SipClient : ISipClient
{
    private readonly SIPTransport _transport;
    private readonly WindowsAudioEndPoint _winAudio;
    private readonly SIPUserAgent _userAgent;
    private VoIPMediaSession _voIPMediaSession;

    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;

    public event Action<ISipClient> CallAnswer;
    public event Action<ISipClient> RemotePutOnHold;
    public event Action<ISipClient> RemoteTookOffHold;

    private static string _sdpMimeContentType = SDP.SDP_MIME_CONTENTTYPE;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    public SipClient(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<SipClient>();

        //Инициализируем все окружение для звонка

        _transport = new SIPTransport();
        _userAgent = new SIPUserAgent(_transport, null);

        _userAgent.ClientCallTrying += CallTrying;
        _userAgent.ClientCallRinging += CallRinging;
        _userAgent.ClientCallAnswered += CallAnswered;
        _userAgent.ClientCallFailed += CallFailed;
        _userAgent.OnCallHungup += CallFinished;
        _userAgent.ServerCallCancelled += IncomingCallCancelled;


        //_winAudio = new WindowsAudioEndPoint(new AudioEncoder());
        //_voIPMediaSession = new VoIPMediaSession(new MediaEndPoints { AudioSink = _winAudio, AudioSource = _winAudio });    
    }

    public bool IsCallActive
    {
        get 
        {
            if (_userAgent == null)
            {
                return false;
            }
            else
            {
                return _userAgent.IsCallActive;
            }
        }
    }

    public async Task CallAndHold(string ext)
    {
        await CallAsync(ext);
        _userAgent.PutOnHold();        
    }

    public async Task CallAsync(string ext)
    {
        string sipTrankUrl = _configuration["Worker:Sip:Uri"];
        string sipLogin = _configuration["Worker:Sip:UserName"];
        string sipPassword = _configuration["Worker:Sip:Password"];

        // This call will use the pre-configured SIP account.
        var callURI = SIPURI.ParseSIPURIRelaxed(sipTrankUrl);
        string fromHeader = (new SIPFromHeader("100", new SIPURI(sipLogin, "175.154.207.15", null), null)).ToString();

        _logger.LogInformation($"Starting call to {callURI}.");

        var dstEndpoint = await SIPDns.ResolveAsync(callURI, false, _cts.Token);

        if (dstEndpoint == null)
        {
            _logger.LogInformation($"Call failed, could not resolve {callURI}.");
        }
        else
        {
            _logger.LogInformation($"Call progressing, resolved {callURI} to {dstEndpoint}.");
            System.Diagnostics.Debug.WriteLine($"DNS lookup result for {callURI}: {dstEndpoint}.");
            SIPCallDescriptor callDescriptor = new SIPCallDescriptor(sipLogin, sipPassword, callURI.ToString(), fromHeader, null, null, null, null, SIPCallDirection.Out, _sdpMimeContentType, null, null);

            _voIPMediaSession = CreateMediaSession();

            _userAgent.RemotePutOnHold += OnRemotePutOnHold;
            _userAgent.RemoteTookOffHold += OnRemoteTookOffHold;

            await _userAgent.InitiateCallAsync(callDescriptor, _voIPMediaSession);
        }

        //return await _userAgent.Call(sipTrankUrl, sipLogin, sipPassword, _voIPMediaSession);
    }

    /// <summary>
    /// Creates the media session to use with the SIP call.
    /// </summary>
    /// <param name="audioSrcOpts">The audio source options to set when the call is first
    /// answered. These options can be adjusted afterwards to do things like put play
    /// on hold music etc.</param>
    /// <returns>A new media session object.</returns>
    private VoIPMediaSession CreateMediaSession()
    {
        var outDevIndex = _configuration["Worker:Sip:AudioOutDeviceIndex"];
        var windowsAudioEndPoint = new WindowsAudioEndPoint(new AudioEncoder(), -1);
        var windowsVideoEndPoint = new WindowsVideoEndPoint(new VpxVideoEncoder());

        MediaEndPoints mediaEndPoints = new MediaEndPoints
        {
            AudioSink = windowsAudioEndPoint,
            AudioSource = windowsAudioEndPoint,
            VideoSink = windowsVideoEndPoint,
            VideoSource = windowsVideoEndPoint,
        };

        // Fallback video source if a Windows webcam cannot be accessed.
        var testPatternSource = new VideoTestPatternSource(new VpxVideoEncoder());

        var voipMediaSession = new VoIPMediaSession(mediaEndPoints, testPatternSource);
        voipMediaSession.AcceptRtpFromAny = true;

        return voipMediaSession;
    }

    public async Task HangUp()
    {
        if (_userAgent.IsCalling || _userAgent.IsCallActive)
        {
            _userAgent.Hangup();
            //_transport.Shutdown();
        }
    }

    public async Task ResumeCall()
    {
        if (_userAgent.IsOnLocalHold)
        {
            _userAgent.TakeOffHold();
        }
    }


    /// <summary>
    /// A trying response has been received from the remote SIP UAS on an outgoing call.
    /// </summary>
    private void CallTrying(ISIPClientUserAgent uac, SIPResponse sipResponse)
    {
        _logger.LogInformation( "Call trying: " + sipResponse.StatusCode + " " + sipResponse.ReasonPhrase + ".");
    }

    /// <summary>
    /// A ringing response has been received from the remote SIP UAS on an outgoing call.
    /// </summary>
    private void CallRinging(ISIPClientUserAgent uac, SIPResponse sipResponse)
    {
        _logger.LogInformation("Call ringing: " + sipResponse.StatusCode + " " + sipResponse.ReasonPhrase + ".");
    }

    /// <summary>
    /// An outgoing call was rejected by the remote SIP UAS on an outgoing call.
    /// </summary>
    private void CallFailed(ISIPClientUserAgent uac, string errorMessage, SIPResponse failureResponse)
    {
        _logger.LogInformation("Call failed: " + errorMessage + ".");
        CallFinished(null);
    }

    /// <summary>
    /// An outgoing call was successfully answered.
    /// </summary>
    /// <param name="uac">The local SIP user agent client that initiated the call.</param>
    /// <param name="sipResponse">The SIP answer response received from the remote party.</param>
    private void CallAnswered(ISIPClientUserAgent uac, SIPResponse sipResponse)
    {
        _logger.LogInformation("Call answered: " + sipResponse.StatusCode + " " + sipResponse.ReasonPhrase + ".");
        CallAnswer?.Invoke(this);
    }

    /// <summary>
    /// Cleans up after a SIP call has completely finished.
    /// </summary>
    private void CallFinished(SIPDialogue dialogue)
    {
        _logger.LogInformation("Call finished: ");
    }

    /// <summary>
    /// An incoming call was cancelled by the caller.
    /// </summary>
    private void IncomingCallCancelled(ISIPServerUserAgent uas)
    {
        //SetText(m_signallingStatus, "incoming call cancelled for: " + uas.CallDestination + ".");
        CallFinished(null);
    }

    /// <summary>	
    /// Event handler that notifies us the remote party has put us on hold.	
    /// </summary>	
    private void OnRemotePutOnHold()
    {
        RemotePutOnHold?.Invoke(this);
    }

    /// <summary>	
    /// Event handler that notifies us the remote party has taken us off hold.	
    /// </summary>	
    private void OnRemoteTookOffHold()
    {
        RemoteTookOffHold?.Invoke(this);
    }

}
