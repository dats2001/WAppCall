using Microsoft.Extensions.Logging;
using Rosbank.DRPZ.WAppAutomation.Domain.Enums;
using Rosbank.DRPZ.WAppAutomation.Domain.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rosbank.DRPZ.WAppAutomation.Application.Services
{
    public interface ICallBroker
    {
        Task WAppCallStatusChanged(WAppCallEventArgs args);

        Task Call(string phoneNumber);
    
    }
    public class CallBroker : ICallBroker
    {
        private readonly ILogger _logger;
        private readonly ISipClient _sipClient;
        private readonly IWAppDesktopClient _desktopClient;

        private WAppCallStatus callStatus = WAppCallStatus.Free; 

        public CallBroker(ILoggerFactory loggerFactory, ISipClient sipClient, IWAppDesktopClient desktopClient)
        {
            _logger = loggerFactory.CreateLogger<CallBroker>();
            _sipClient = sipClient;
            _desktopClient = desktopClient;

            _desktopClient.CallStatusChanged += WAppCallStatusChanged;
        }

        public async Task Call(string phoneNumber)
        {
            await _desktopClient.Call(phoneNumber);
            while (callStatus != WAppCallStatus.CallFinished)
            {
                await Task.Delay(1000);
            }
            callStatus = WAppCallStatus.Free;
        }

        public async Task WAppCallStatusChanged(WAppCallEventArgs arg)
        {
            if (arg.Status == Domain.Enums.WAppCallStatus.Calling)
            {
                await _sipClient.CallAsync("7777");
                callStatus = WAppCallStatus.Calling;
            }
            else if (arg.Status == Domain.Enums.WAppCallStatus.CallFinished)
            {
                await _sipClient.HangUp();
                callStatus = WAppCallStatus.CallFinished;
            }
        }
    }
}
