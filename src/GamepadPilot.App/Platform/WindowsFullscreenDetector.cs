using System.Runtime.InteropServices;
using System.Text;
using GamepadPilot.Core.Services;

namespace GamepadPilot.App.Platform;

public sealed class WindowsFullscreenDetector : IFullscreenDetector
{
    public FullscreenState Detect()
    {
        if (SHQueryUserNotificationState(out var state) == 0 && state == QunsRunningD3DFullscreen)
        {
            return new FullscreenState(true, "检测到独占全屏程序");
        }

        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || !IsWindowVisible(window) || IsIconic(window) || IsShellWindow(window) || !GetWindowRect(window, out var windowRect))
        {
            return new FullscreenState(false, string.Empty);
        }

        var monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return new FullscreenState(false, string.Empty);
        }

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return new FullscreenState(false, string.Empty);
        }

        var style = GetWindowLongPtr(window, GwlStyle).ToInt64();
        var hasFrame = (style & (WsCaption | WsThickFrame)) != 0;
        var coversWidth = windowRect.Width >= monitorInfo.Monitor.Width - 2;
        var coversHeight = windowRect.Height >= monitorInfo.Monitor.Height - 2;
        if (!hasFrame && coversWidth && coversHeight)
        {
            return new FullscreenState(true, "检测到无边框全屏窗口");
        }

        return new FullscreenState(false, string.Empty);
    }

    private const int GwlStyle = -16;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const uint MonitorDefaultToNearest = 2;
    private const int QunsRunningD3DFullscreen = 3;

    private static bool IsShellWindow(IntPtr window)
    {
        var className = new StringBuilder(256);
        if (GetClassName(window, className, className.Capacity) == 0)
        {
            return false;
        }

        return className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private static IntPtr GetWindowLongPtr(IntPtr window, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(window, index) : new IntPtr(GetWindowLong32(window, index));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr window, int index);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
