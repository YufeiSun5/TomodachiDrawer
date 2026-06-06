using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TomodachiDrawerCn.Worker;

public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TomodachiDrawer-CN worker started. Queue integration is pending.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            logger.LogInformation("TomodachiDrawer-CN worker heartbeat at {CheckedAt:O}", DateTimeOffset.UtcNow);
        }
    }
}
