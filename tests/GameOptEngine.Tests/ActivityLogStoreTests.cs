using GameOptEngine.Service;

namespace GameOptEngine.Tests;

public sealed class ActivityLogStoreTests
{
    [Fact]
    public async Task RecordAsync_AssignsIncreasingIndexesAndPersistsAcrossInstances()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(directory, "activity.jsonl");
        try
        {
            var firstStore = new ActivityLogStore(logPath);
            var first = await firstStore.RecordAsync("test.start", "first");
            var second = await firstStore.RecordAsync("test.finish", "second");
            var restartedStore = new ActivityLogStore(logPath);
            var third = await restartedStore.RecordAsync("test.next", "third");
            var records = await restartedStore.ReadAllAsync();

            Assert.Equal(new long[] { 1, 2, 3 }, records.Select(record => record.Index));
            Assert.Equal("test.next", third.Activity);
            Assert.Contains("\"index\":3", File.ReadLines(logPath).Last());
            Assert.Equal(1, first.Index);
            Assert.Equal(2, second.Index);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void RenderHtml_EncodesActivityFieldsAndShowsIndexedRows()
    {
        var records = new[]
        {
            new ActivityRecord(4, DateTimeOffset.UnixEpoch, "test <script>", "detail & value")
        };

        var html = ActivityViewer.RenderHtml(records);

        Assert.Contains("<th>Index</th>", html);
        Assert.Contains("<td>4</td>", html);
        Assert.Contains("test &lt;script&gt;", html);
        Assert.Contains("detail &amp; value", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public async Task ReadAllAsync_SkipsMalformedLines()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(directory, "activity.jsonl");
        try
        {
            var store = new ActivityLogStore(logPath);
            await store.RecordAsync("test.valid", "valid");
            await File.AppendAllTextAsync(logPath, "{invalid json}" + Environment.NewLine);

            var records = await store.ReadAllAsync();

            Assert.Single(records);
            Assert.Equal("test.valid", records[0].Activity);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
