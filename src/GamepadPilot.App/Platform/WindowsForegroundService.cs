using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GamepadPilot.Core.Services;

namespace GamepadPilot.App.Platform;

public sealed class WindowsForegroundService : IForegroundAppService
{
    public ForegroundAppInfo? GetForegroundApp()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        var length = GetWindowTextLength(handle);
        var builder = new StringBuilder(Math.Max(1, length + 1));
        _ = GetWindowText(handle, builder, builder.Capacity);

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var executable = TryGetExecutable(process);
            return new ForegroundAppInfo((int)processId, process.ProcessName, builder.ToString(), executable);
        }
        catch
        {
            return new ForegroundAppInfo((int)processId, string.Empty, builder.ToString(), null);
        }
    }

    private static string? TryGetExecutable(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr window);
}
