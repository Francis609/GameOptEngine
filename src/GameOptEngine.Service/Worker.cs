using Microsoft.Extensions.Hosting;

namespace GameOptEngine.Service;

public sealed class Worker : BackgroundService
{
    private readonly GameProcessMonitor _gameProcessMonitor;

    public Worker(GameProcessMonitor gameProcessMonitor) => _gameProcessMonitor = gameProcessMonitor;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _gameProcessMonitor.MonitorAsync(stoppingToken);
}
