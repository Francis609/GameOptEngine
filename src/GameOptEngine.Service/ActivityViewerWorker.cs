using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace GameOptEngine.Service;

public sealed class ActivityViewerWorker : BackgroundService
{
    private readonly ActivityLogStore _store;
    private readonly ILogger<ActivityViewerWorker> _logger;
    private WebApplication? _application;

    public ActivityViewerWorker(ActivityLogStore store, ILogger<ActivityViewerWorker> logger)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        var prefix = Environment.GetEnvironmentVariable("GAMEOPTENGINE_ACTIVITY_VIEWER_PREFIX") ?? "http://127.0.0.1:5088/";
        if (!Uri.TryCreate(prefix, UriKind.Absolute, out var address) || address.Scheme != Uri.UriSchemeHttp)
        {
            throw new InvalidOperationException("GAMEOPTENGINE_ACTIVITY_VIEWER_PREFIX must be a valid HTTP URL.");
        }

        builder.WebHost.UseUrls(prefix);
        _application = builder.Build();
        _application.MapGet("/api/activity", async (CancellationToken cancellationToken) =>
        {
            var records = await _store.ReadAllAsync(cancellationToken);
            return Results.Json(records);
        });
        _application.MapGet("/", async (CancellationToken cancellationToken) =>
        {
            var records = await _store.ReadAllAsync(cancellationToken);
            return Results.Content(ActivityViewer.RenderHtml(records), "text/html; charset=utf-8");
        });
        _application.MapGet("/index.html", async (CancellationToken cancellationToken) =>
        {
            var records = await _store.ReadAllAsync(cancellationToken);
            return Results.Content(ActivityViewer.RenderHtml(records), "text/html; charset=utf-8");
        });

        _logger.LogInformation("Activity viewer listening at {Address}; activity log: {LogPath}", prefix, _store.FilePath);
        await _application.StartAsync(stoppingToken);
        try
        {
            await _application.WaitForShutdownAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_application is not null)
        {
            await _application.StopAsync(cancellationToken);
            await _application.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
