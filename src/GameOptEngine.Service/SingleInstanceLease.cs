namespace GameOptEngine.Service;

public sealed class SingleInstanceLease : IDisposable
{
    private readonly FileStream _lockFile;

    private SingleInstanceLease(FileStream lockFile) => _lockFile = lockFile;

    public static SingleInstanceLease? TryAcquire(string lockFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);

        var fullPath = Path.GetFullPath(lockFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        try
        {
            return new SingleInstanceLease(new FileStream(
                fullPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None));
        }
        catch (IOException exception) when (IsLockContention(exception))
        {
            return null;
        }
    }

    public void Dispose() => _lockFile.Dispose();

    private static bool IsLockContention(IOException exception)
    {
        var errorCode = exception.HResult & 0xFFFF;
        return OperatingSystem.IsWindows()
            ? errorCode is 32 or 33
            : errorCode is 11 or 16;
    }
}
