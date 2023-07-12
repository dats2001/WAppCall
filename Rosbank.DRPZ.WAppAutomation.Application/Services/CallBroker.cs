using Microsoft.Extensions.Logging;
using Rosbank.DRPZ.WAppAutomation.Application.Sip;
using Rosbank.DRPZ.WAppAutomation.Domain.Enums;
using Rosbank.DRPZ.WAppAutomation.Domain.EventArgs;
using Rosbank.DRPZ.WAppAutomation.Domain.Interfaces;
using Serilog.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Protection.PlayReady;
using Microsoft.Extensions.Configuration;

namespace Rosbank.DRPZ.WAppAutomation.Application.Services
{
    public interface ICallBroker
    {
        Task WAppCallStatusChanged(WAppCallEventArgs args);

        Task CallSipNumber(string phoneNumber);

        Task CallWhatsAppNumber(string phoneNumber);
    
    }
    public class CallBroker : ICallBroker
    {
        private readonly IConfiguration _configuration;

        private SIPTransportManager _sipTransportManager;
        private SIPClient _sipClient;
        private readonly IWAppDesktopClient _desktopClient;

        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        private WAppCallStatus callStatus = WAppCallStatus.Free; 

        public CallBroker(ILoggerFactory loggerFactory, IWAppDesktopClient desktopClient, IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<CallBroker>();

            _desktopClient = desktopClient;
            _desktopClient.CallStatusChanged += WAppCallStatusChanged;

            AddConsoleLogger();

            _sipTransportManager = new SIPTransportManager();

        }

        /// <summary>
        /// Настрвиваем лог событий сип в дебаг
        /// </summary>
        private static void AddDebugLogger()
        {
            var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
                .WriteTo.Debug()
                .CreateLogger();
            SIPSorcery.LogFactory.Set(new SerilogLoggerFactory(serilogLogger));
        }

        /// <summary>
        /// Настраиваем лог событий сип в консоль
        /// </summary>
        private static void AddConsoleLogger()
        {
            var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
                .WriteTo.Console()
                .CreateLogger();
            var factory = new SerilogLoggerFactory(serilogLogger);
            SIPSorcery.LogFactory.Set(factory);
        }

        /// <summary>
        /// Производим вызов по указзанному адресу или адресу из конфигурации
        /// </summary>
        /// <param name="sipAddress"></param>
        /// <returns></returns>
        public async Task CallSipNumber(string sipAddress)
        {
            //if (string.IsNullOrEmpty(sipAddress))
            //{
            //    sipAddress = _configuration["Worker:Sip:Uri"];
            //}
            await _sipTransportManager.InitialiseSIP();

            _sipClient = new SIPClient(_sipTransportManager.SIPTransport);

            _sipClient.StatusMessage += (client, message) => { Console.WriteLine(message); };
            _sipClient.CallAnswer += (client) => { Console.WriteLine("SIP Call answered"); };
            _sipClient.CallEnded += (client) => { Console.WriteLine("SIP Call ended"); };

            // Start SIP call.
            await _sipClient.Call(sipAddress);            
        }

        /// <summary>
        /// Вызываем номер WhatsApp
        /// </summary>
        /// <param name="phoneNumber"></param>
        /// <returns></returns>
        public async Task CallWhatsAppNumber(string phoneNumber)
        {
            await _desktopClient.Call(phoneNumber);
            while (callStatus != WAppCallStatus.CallFinished)
            {
                await Task.Delay(100);
            }
            callStatus = WAppCallStatus.Free;
        }

        /// <summary>
        /// Обработка события изменения статуса звонка WhatsApp
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        public async Task WAppCallStatusChanged(WAppCallEventArgs arg)
        {
            if (arg.Status == Domain.Enums.WAppCallStatus.Calling)
            {
                // Производим звонок по адресу СИП транка или указанного номера
                await CallSipNumber(string.Empty);
                callStatus = WAppCallStatus.Calling;
            }
            else if (arg.Status == Domain.Enums.WAppCallStatus.CallFinished)
            {
                // Повесить трубку в активном SIP вызове
                if (_sipClient.IsCallActive)
                {
                    _sipClient.Hangup();
                }
                callStatus = WAppCallStatus.CallFinished;
            }
        }
    }
}
