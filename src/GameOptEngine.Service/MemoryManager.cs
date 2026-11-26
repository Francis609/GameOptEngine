using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace GameOptEngine.Service;

public static class MemoryManager
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(
        IntPtr process,
        IntPtr minimumWorkingSetSize,
        IntPtr maximumWorkingSetSize);

    public static void TrimWorkingSet()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false, compacting: false);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        if (!SetProcessWorkingSetSize(process.Handle, new IntPtr(-1), new IntPtr(-1)))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static void TerminateBackgroundHogs(IEnumerable<string> targetNames)
    {
        ArgumentNullException.ThrowIfNull(targetNames);
        // Intentionally no-op. Do not terminate user, development, cloud, system, or other processes.
    }
}
