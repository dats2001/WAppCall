using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rosbank.DRPZ.WAutomation.Client.Sip;
using SIPSorcery.SIP.App;
using SIPSorcery.SIP;
using Serilog.Extensions.Logging;
using Serilog;
using SIPSorcery.Sys;
using SIPSorceryMedia.Abstractions;
using System.Net;
using SIPSorcery.Net;

using IHost host = Host.CreateDefaultBuilder(args).Build();
IConfiguration config = host.Services.GetRequiredService<IConfiguration>();

AddDebugLogger();

Console.WriteLine(GetIp());

const int REGISTRATION_EXPIRY = 180;

// Read configuration file
string server = config["Worker:Sip:Server"];
string user = config["Worker:Sip:User"];
string password = config["Worker:Sip:Password"];
string stunServer = config["Worker:Sip:STUN"];

// declare sip objects
SIPTransportManager _sipTransportManager;
SIPRegistrationUserAgent _sipRegistrationClient;    // Can be used to register with an external SIP provider if incoming calls are required.
SIPClient _sipClient = null;
SoftphoneSTUNClient _stunClient;                    // STUN client to periodically check the public IP address.

_sipTransportManager = new SIPTransportManager();
_sipTransportManager.IncomingCall += SIPCallIncoming;

bool SIPCallIncoming(SIPRequest sipRequest)
{
    Console.WriteLine($"Incoming call from {sipRequest.Header.From.FriendlyDescription()}.");

    if (!_sipClient.IsCallActive)
    {
        _sipClient.Accept(sipRequest);

        return true;
    }
    else
    {
        return false;
    }
}

// If a STUN server hostname has been specified start the STUN client to lookup and periodically 
// update the public IP address of the host machine.
if (!string.IsNullOrEmpty(stunServer))
{
    _stunClient = new SoftphoneSTUNClient(stunServer);
    _stunClient.PublicIPAddressDetected += (ip) =>
    {
        Console.WriteLine($"Public ip address: {ip}");
    };
    _stunClient.Run();
}

//Init SIP
await Initialize();


Console.WriteLine("Press any key to stop...");
Console.ReadLine();


#region "SIP Events"
/// <summary>
/// Answer an incoming call on the SipClient
/// </summary>
/// <param name="client"></param>
/// <returns></returns>
async Task AnswerCallAsync(SIPClient client)
{
    bool result = await client.Answer();

    if (result)
    {
        Console.WriteLine("Call is unswered. Conversation is in progress.");
    }
    else
    {
        Console.WriteLine("Ready");
    }
}

static void AddDebugLogger()
{
    var serilogLogger = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
        .WriteTo.Debug()
        .WriteTo.Console()
        .CreateLogger();
    SIPSorcery.LogFactory.Set(new SerilogLoggerFactory(serilogLogger));
}

async void CallAnswered(SIPClient obj)
{
    Console.WriteLine("Call is unswered");
};

async Task Initialize()
{
    await _sipTransportManager.InitialiseSIP();

    _sipTransportManager.SIPTransport.EnableTraceLogs();
    _sipClient = new SIPClient(_sipTransportManager.SIPTransport, server, user, password);

    _sipClient.CallAnswer += CallAnswered; ;
    _sipClient.StatusMessage += (client, message) => { Console.WriteLine(message); };

    string listeningEndPoints = null;

    foreach (var sipChannel in _sipTransportManager.SIPTransport.GetSIPChannels())
    {
        SIPEndPoint sipChannelEP = sipChannel.ListeningSIPEndPoint.CopyOf();
        sipChannelEP.ChannelID = null;
        listeningEndPoints += (listeningEndPoints == null) ? sipChannelEP.ToString() : $", {sipChannelEP}";
    }

    Console.WriteLine($"Listening on: {listeningEndPoints}");

    var sipTransport = new SIPTransport();
    sipTransport.EnableTraceLogs();

    _sipRegistrationClient = new SIPRegistrationUserAgent(
        sipTransport,
        user,
        password,
        server,
        180);

    _sipRegistrationClient.Start();

    
}

string GetIp()  {
    string IPAddress = "";
    IPHostEntry Host = default(IPHostEntry);
    string Hostname = null;
    Hostname = System.Environment.MachineName;
    Host = Dns.GetHostEntry(Hostname);
    foreach (IPAddress IP in Host.AddressList)
    {
        if (IP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            IPAddress = Convert.ToString(IP);
        }
    }

    return IPAddress;
}


#endregion