using Rosbank.DRPZ.WAppAutomation.Application.Services;
using Rosbank.DRPZ.WAppAutomation.Domain.Interfaces;

namespace Rosbank.DRPZ.WAppAutomation.Worker
{
    public class WAppClientCallWorker : BackgroundService
    {
        private readonly ILogger<WAppClientCallWorker> _logger;
        private readonly ICallBroker _callBroker;

        public WAppClientCallWorker(ICallBroker callBroker, ILogger<WAppClientCallWorker> logger)
        {
            _callBroker = callBroker;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            while (!stoppingToken.IsCancellationRequested)
            {
                // Coonect to servers with calling tasks
                // Report broker status // free, in a call, not logged in

                // check for call queue, get number and make WhatsApp call
                await _callBroker.CallWhatsAppNumber("+79119115650");
                // Report call status

                await Task.Delay(35000, stoppingToken);
                //return;
            }
        }
    }
}