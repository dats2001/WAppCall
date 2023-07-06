using Rosbank.DRPZ.WAppAutomation.Application.Services;

namespace Rosbank.DRPZ.WAppAutomation.Worker
{
    public class WAppClientCallWorker : BackgroundService
    {
        private readonly ILogger<WAppClientCallWorker> _logger;
        private readonly ICallBroker _callBroker;
        private readonly ISipClient _sipClient;

        public WAppClientCallWorker(ICallBroker callBroker, ISipClient sipClient, ILogger<WAppClientCallWorker> logger)
        {
            _callBroker = callBroker;
            _sipClient = sipClient;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                //await _callBroker.Call("+79119115650");
                await _sipClient.CallAsync("7777");

                await Task.Delay(30000, stoppingToken);
                return;
            }
        }
    }
}