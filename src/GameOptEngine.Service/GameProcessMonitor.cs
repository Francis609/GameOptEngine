using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GameOptEngine.Service;

public sealed class GameProcessMonitor
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly Regex VdfValue = new("^\\s*\"(?<key>[^\"]+)\"\\s+\"(?<value>[^\"]*)\"", RegexOptions.Compiled);
    private static readonly string[] IgnoredExecutables =
    [
        "crashreporter", "crashhandler", "unins", "setup", "installer",
        "vc_redist", "dxsetup", "webview2runtime", "eaclauncher"
    ];

    private readonly ILogger<GameProcessMonitor> _logger;
    private readonly ActivityLogStore _activityLog;
    private readonly HashSet<string> _gameNames;

    public GameProcessMonitor(ILogger<GameProcessMonitor> logger, ActivityLogStore activityLog)
    {
        _logger = logger;
        _activityLog = activityLog;
        _gameNames = DiscoverSteamGameProcessNames();
        foreach (var name in (Environment.GetEnvironmentVariable("GAMEOPTENGINE_GAME_PROCESS_NAMES") ?? string.Empty)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _gameNames.Add(Path.GetFileNameWithoutExtension(name));
        }

        _logger.LogInformation("Game monitor ready; watching {Count} executable names: {ProcessNames}.",
            _gameNames.Count,
            string.Join(", ", _gameNames.Order(StringComparer.OrdinalIgnoreCase)));
    }

    public static HashSet<string> DiscoverSteamGameProcessNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var steamRoot in FindSteamRoots())
        {
            var steamApps = Path.Combine(steamRoot, "steamapps");
            var common = Path.Combine(steamApps, "common");
            if (!Directory.Exists(steamApps) || !Directory.Exists(common))
            {
                continue;
            }

            var installedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
            {
                var values = ParseManifest(manifest);
                if (!values.TryGetValue("StateFlags", out var flags) || !int.TryParse(flags, out var state) || (state & 4) == 0 ||
                    !values.TryGetValue("installdir", out var installDirectory))
                {
                    continue;
                }

                installedDirectories.Add(installDirectory);
            }

            // Steam may leave stale/missing manifests while a library still contains installed game files.
            foreach (var gameDirectory in Directory.EnumerateDirectories(common))
            {
                if (installedDirectories.Count > 0 && !installedDirectories.Contains(Path.GetFileName(gameDirectory)) &&
                    !Directory.Exists(Path.Combine(gameDirectory, "multiexe")))
                {
                    continue;
                }

                try
                {
                    foreach (var executable in Directory.EnumerateFiles(gameDirectory, "*.exe", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(executable);
                        if (!IgnoredExecutables.Any(ignored => name.Contains(ignored, StringComparison.OrdinalIgnoreCase)))
                        {
                            names.Add(name);
                        }
                    }
                }
                catch (IOException)
                {
                    // Skip libraries or game directories unavailable during enumeration.
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip files that the current user cannot inspect.
                }
            }
        }

        return names;
    }

    public static Dictionary<string, string> ParseManifest(string manifestPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(manifestPath))
        {
            var match = VdfValue.Match(line);
            if (match.Success)
            {
                result[match.Groups["key"].Value] = match.Groups["value"].Value;
            }
        }

        return result;
    }

    public static bool IsGameProcess(string processName, ISet<string> gameNames) =>
        gameNames.Contains(Path.GetFileNameWithoutExtension(processName));

    public async Task MonitorAsync(CancellationToken cancellationToken)
    {
        var activeProcesses = new Dictionary<int, string>();
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            var currentProcesses = new Dictionary<int, string>();
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        var processName = process.ProcessName;
                        if (!IsGameProcess(processName, _gameNames))
                        {
                            continue;
                        }

                        currentProcesses[process.Id] = processName;
                        if (!activeProcesses.ContainsKey(process.Id))
                        {
                            var workingSetMb = process.WorkingSet64 / 1024d / 1024d;
                            var availableMemoryMb = GetAvailablePhysicalMemoryMb();
                            var details = $"PID {process.Id}; working set {workingSetMb:F0} MB; available system RAM {availableMemoryMb?.ToString("F0") ?? "unknown"} MB";
                            _logger.LogInformation("Game process detected: {ProcessName} ({ProcessDetails}).", processName, details);
                            await _activityLog.RecordAsync("game.process.started", $"{processName}; {details}", cancellationToken);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // The process exited while being inspected.
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Process metadata is not accessible to this user.
                    }
                }
            }

            foreach (var previous in activeProcesses.Where(item => !currentProcesses.ContainsKey(item.Key)))
            {
                _logger.LogInformation("Game process exited: {ProcessName} (PID {ProcessId}).", previous.Value, previous.Key);
                await _activityLog.RecordAsync("game.process.exited", $"{previous.Value}; PID {previous.Key}", cancellationToken);
            }

            activeProcesses = currentProcesses;
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private static double? GetAvailablePhysicalMemoryMb()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var memoryStatus = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref memoryStatus)
            ? memoryStatus.AvailablePhysical / 1024d / 1024d
            : null;
    }

    private static IEnumerable<string> FindSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var environmentRoot = Environment.GetEnvironmentVariable("STEAM_DIR");
        if (!string.IsNullOrWhiteSpace(environmentRoot))
        {
            roots.Add(environmentRoot);
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                foreach (var keyPath in new[] { @"Software\Valve\Steam", @"Software\WOW6432Node\Valve\Steam" })
                {
                    try
                    {
                        var path = hive.OpenSubKey(keyPath)?.GetValue("SteamPath") as string;
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            roots.Add(path.Replace('/', Path.DirectorySeparatorChar));
                        }
                    }
                    catch (System.Security.SecurityException)
                    {
                        // Registry discovery is optional; environment and standard paths remain available.
                    }
                }
            }
        }

        foreach (var candidate in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
                 })
        {
            if (Directory.Exists(candidate))
            {
                roots.Add(candidate);
            }
        }

        foreach (var root in roots.ToArray())
        {
            var libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile))
            {
                continue;
            }

            foreach (var line in File.ReadLines(libraryFile))
            {
                var match = VdfValue.Match(line);
                if (match.Success && string.Equals(match.Groups["key"].Value, "path", StringComparison.OrdinalIgnoreCase))
                {
                    roots.Add(match.Groups["value"].Value.Replace('/', Path.DirectorySeparatorChar));
                }
            }
        }

        return roots;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
