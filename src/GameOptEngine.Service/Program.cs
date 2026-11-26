using System.Diagnostics;
using GameOptEngine.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ActivityLogStore>();
builder.Services.AddSingleton<GameProcessMonitor>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<ActivityViewerWorker>();

var host = builder.Build();
var activityLog = host.Services.GetRequiredService<ActivityLogStore>();
var lockPath = Path.Combine(
    Path.GetTempPath(),
    "GameOptEngine",
    $"{Path.GetFileName(Environment.UserName)}.lock");
using var instanceLease = SingleInstanceLease.TryAcquire(lockPath);
if (instanceLease is null)
{
    if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
    {
        Console.Write("GameOptEngine is already running. Open its activity viewer? [y/N]: ");
        var answer = Console.ReadLine();
        if (string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = Environment.GetEnvironmentVariable("GAMEOPTENGINE_ACTIVITY_VIEWER_PREFIX")
                ?? "http://127.0.0.1:5088/";
            if (!Uri.TryCreate(prefix, UriKind.Absolute, out var address) || address.Scheme != Uri.UriSchemeHttp)
            {
                throw new InvalidOperationException("GAMEOPTENGINE_ACTIVITY_VIEWER_PREFIX must be a valid HTTP URL.");
            }

            Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
        }
    }
    else
    {
        Console.Error.WriteLine("GameOptEngine is already running; exiting this duplicate launch.");
    }

    return;
}

await activityLog.RecordAsync("service.started", "GameOptEngine service started");
await host.RunAsync();
