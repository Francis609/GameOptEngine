using GameOptEngine.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ActivityLogStore>();
builder.Services.AddSingleton<GameProcessMonitor>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<ActivityViewerWorker>();

var host = builder.Build();
await host.Services.GetRequiredService<ActivityLogStore>()
    .RecordAsync("service.started", "GameOptEngine service started");
await host.RunAsync();
