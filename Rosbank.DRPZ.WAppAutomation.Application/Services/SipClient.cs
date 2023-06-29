using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows;

namespace Rosbank.DRPZ.WAppAutomation.Application.Services;

public interface ISipClient
{ 
    /// <summary>
    /// Проверка на активность звонка
    /// </summary>
    public bool IsCallActive { get; }

    /// <summary>
    /// Позвонить
    /// </summary>
    /// <param name="ext">номер</param>
    /// <returns></returns>
    Task<bool> CallAsync(string ext);

    /// <summary>
    /// Повесить трубку
    /// </summary>
    /// <returns></returns>
    Task<bool> HangUp();

    /// <summary>
    /// Позвонить и поставитьна удержание
    /// </summary>
    /// <param name="ext"></param>
    /// <returns></returns>
    Task<bool> CallAndHold(string ext);

    /// <summary>
    /// Возобновить разговор
    /// </summary>
    /// <returns></returns>
    Task<bool> ResumeCall();
}

public class SipClient : ISipClient
{
    private readonly SIPTransport _transport;
    private readonly WindowsAudioEndPoint _winAudio;
    private readonly SIPUserAgent _userAgent;
    private readonly VoIPMediaSession _voIPMediaSession;

    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;

    public SipClient(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<SipClient>();

        //Инициализируем все окружение для звонка

        _transport = new SIPTransport();
        _userAgent = new SIPUserAgent(_transport, null);
        _winAudio = new WindowsAudioEndPoint(new AudioEncoder());
        _voIPMediaSession = new VoIPMediaSession(new MediaEndPoints { AudioSink = _winAudio, AudioSource = _winAudio });    
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

    public async Task<bool> CallAndHold(string ext)
    {
        bool CallResult = await CallAsync(ext);
        _userAgent.PutOnHold();
        return CallResult;
    }

    public async Task<bool> CallAsync(string ext)
    {
        string sipTrankUrl = _configuration["Worker:Sip:Uri"];
        string sipLogin = _configuration["Worker:Sip:UserName"];
        string sipPassword = _configuration["Worker:Sip:Password"];
        return await _userAgent.Call(sipTrankUrl, sipLogin, sipPassword, _voIPMediaSession);
    }

    public async Task<bool> HangUp()
    {
        if (_userAgent.IsCalling || _userAgent.IsCallActive)
        {
            _userAgent.Hangup();
            //_transport.Shutdown();
            return true;
        }
        return false;
    }

    public async Task<bool> ResumeCall()
    {
        if (_userAgent.IsOnLocalHold)
        {
            _userAgent.TakeOffHold();
            return true;
        }
        return false;
    }
}
