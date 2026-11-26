using System.Text;
using System.Text.Json;

namespace GameOptEngine.Service;

public sealed record ActivityRecord(long Index, DateTimeOffset TimestampUtc, string Activity, string Details);

public sealed class ActivityLogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private long _nextIndex;
    private bool _indexLoaded;

    public ActivityLogStore(string? filePath = null)
    {
        var configuredPath = filePath ?? Environment.GetEnvironmentVariable("GAMEOPTENGINE_ACTIVITY_LOG_PATH");
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            var stateDirectory = OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetEnvironmentVariable("XDG_STATE_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
            configuredPath = Path.Combine(stateDirectory, "GameOptEngine", "activity.jsonl");
        }

        FilePath = Path.GetFullPath(configuredPath);
    }

    public string FilePath { get; }

    public async Task<ActivityRecord> RecordAsync(string activity, string details, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activity);
        ArgumentNullException.ThrowIfNull(details);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (!_indexLoaded)
            {
                _nextIndex = ReadLastIndex();
                _indexLoaded = true;
            }

            var record = new ActivityRecord(++_nextIndex, DateTimeOffset.UtcNow, activity, details);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var line = JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine;
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(line);
            await using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            await stream.WriteAsync(bytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return record;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<ActivityRecord>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        var records = new List<ActivityRecord>();
        await using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (JsonSerializer.Deserialize<ActivityRecord>(line, JsonOptions) is { } record)
                {
                    records.Add(record);
                }
            }
            catch (JsonException)
            {
                // Ignore an incomplete or malformed line so the viewer can show the remaining log.
            }
        }

        return records;
    }

    private long ReadLastIndex()
    {
        if (!File.Exists(FilePath))
        {
            return 0;
        }

        long lastIndex = 0;
        foreach (var line in File.ReadLines(FilePath))
        {
            try
            {
                if (JsonSerializer.Deserialize<ActivityRecord>(line, JsonOptions) is { } record)
                {
                    lastIndex = Math.Max(lastIndex, record.Index);
                }
            }
            catch (JsonException)
            {
                // Ignore malformed lines while recovering the next sequence number.
            }
        }

        return lastIndex;
    }
}
