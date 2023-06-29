namespace Rosbank.DRPZ.WAppAutomation.Worker
{
    public class WAppClientCallWorker : BackgroundService
    {
        private readonly ILogger<WAppClientCallWorker> _logger;

        public WAppClientCallWorker(ILogger<WAppClientCallWorker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                await Task.Delay(30000, stoppingToken);
                return;
            }
        }
    }
}