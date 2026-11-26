using GameOptEngine.Service;
using Spectre.Console;

var argsList = args.ToList();
if (argsList.Contains("--help", StringComparer.OrdinalIgnoreCase) || argsList.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    AnsiConsole.MarkupLine("[deepskyblue1]GameOptEngine activity viewer[/]");
    AnsiConsole.WriteLine("Usage: GameOptEngine.Cli [--follow] [--log <path>]");
    AnsiConsole.WriteLine("  --follow     Keep watching for new activity records.");
    AnsiConsole.WriteLine("  --log <path> Read an activity JSONL file other than the default.");
    return;
}

var logOption = argsList.FindIndex(argument => string.Equals(argument, "--log", StringComparison.OrdinalIgnoreCase));
if (logOption >= 0)
{
    if (logOption + 1 >= argsList.Count)
    {
        AnsiConsole.MarkupLine("[red]Missing path after --log.[/]");
        Environment.ExitCode = 2;
        return;
    }

    Environment.SetEnvironmentVariable("GAMEOPTENGINE_ACTIVITY_LOG_PATH", argsList[logOption + 1]);
}

var follow = argsList.Contains("--follow", StringComparer.OrdinalIgnoreCase);
var store = new ActivityLogStore();
var printedThrough = 0L;

AnsiConsole.MarkupLine("[bold deepskyblue1]GameOptEngine[/] [grey]indexed activity log[/]");
AnsiConsole.MarkupLine($"[grey]Source: {Markup.Escape(store.FilePath)}[/]");

while (true)
{
    var records = await store.ReadAllAsync();
    foreach (var record in records.Where(record => record.Index > printedThrough).OrderBy(record => record.Index))
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        table.AddColumn(new TableColumn("[bold deepskyblue1]Index[/]").RightAligned());
        table.AddColumn("[bold deepskyblue1]UTC time[/]");
        table.AddColumn("[bold deepskyblue1]Activity[/]");
        table.AddColumn("[bold deepskyblue1]Details[/]");
        var activityColor = record.Activity.Contains("exited", StringComparison.OrdinalIgnoreCase) ? "grey" : "springgreen2";
        table.AddRow(
            record.Index.ToString(),
            Markup.Escape(record.TimestampUtc.ToString("u")),
            $"[{activityColor}]{Markup.Escape(record.Activity)}[/]",
            Markup.Escape(record.Details));
        AnsiConsole.Write(table);
        printedThrough = record.Index;
    }

    if (!follow)
    {
        break;
    }

    await Task.Delay(TimeSpan.FromSeconds(2));
}
