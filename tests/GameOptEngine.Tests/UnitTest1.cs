using GameOptEngine.Service;

namespace GameOptEngine.Tests;

public sealed class GameProcessMonitorTests
{
    [Fact]
    public void IsGameProcess_MatchesConfiguredGameExecutableName()
    {
        var games = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "RainbowSix" };

        Assert.True(GameProcessMonitor.IsGameProcess("RainbowSix", games));
        Assert.True(GameProcessMonitor.IsGameProcess("RainbowSix.exe", games));
        Assert.False(GameProcessMonitor.IsGameProcess("steam", games));
    }

    [Fact]
    public void DiscoverSteamGameProcessNames_FindsInstalledGamesEvenWhenManifestIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var gameFolder = Path.Combine(root, "steamapps", "common", "Example Game", "bin");
        Directory.CreateDirectory(gameFolder);
        File.WriteAllBytes(Path.Combine(gameFolder, "ExampleGame.exe"), []);
        var previousSteamDirectory = Environment.GetEnvironmentVariable("STEAM_DIR");

        try
        {
            Environment.SetEnvironmentVariable("STEAM_DIR", root);

            var discovered = GameProcessMonitor.DiscoverSteamGameProcessNames();

            Assert.Contains("ExampleGame", discovered);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STEAM_DIR", previousSteamDirectory);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ParseManifest_ReadsInstalledGameFields()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "\"appid\" \"359550\"\n\"StateFlags\" \"4\"\n\"installdir\" \"Rainbow Six Siege\"");

            var values = GameProcessMonitor.ParseManifest(path);

            Assert.Equal("359550", values["appid"]);
            Assert.Equal("4", values["StateFlags"]);
            Assert.Equal("Rainbow Six Siege", values["installdir"]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public sealed class MemoryManagerTests
{
    [Fact]
    public void TrimWorkingSet_DoesNotThrowOnCurrentPlatform()
    {
        var exception = Record.Exception(MemoryManager.TrimWorkingSet);

        Assert.Null(exception);
    }

    [Fact]
    public void TerminateBackgroundHogs_IsSafeNoOp()
    {
        var exception = Record.Exception(() => MemoryManager.TerminateBackgroundHogs(["nonexistent-process-for-test"]));

        Assert.Null(exception);
    }
}
