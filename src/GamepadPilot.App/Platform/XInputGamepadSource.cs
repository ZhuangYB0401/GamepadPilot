using System.Runtime.InteropServices;
using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Services;

namespace GamepadPilot.App.Platform;

public sealed class XInputGamepadSource : IGamepadSource
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellation;
    private Task? _worker;
    private int _controllerIndex = -1;

    public event EventHandler<GamepadSnapshot>? SnapshotReceived;
    public GamepadSnapshot Current { get; private set; } = GamepadSnapshot.Disconnected(DateTimeOffset.Now);
    public bool IsRunning => _worker is { IsCompleted: false };

    public void Start()
    {
        lock (_gate)
        {
            if (_worker is { IsCompleted: false })
            {
                return;
            }

            _cancellation = new CancellationTokenSource();
            _worker = Task.Run(() => PollAsync(_cancellation.Token));
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _worker = null;
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        var highResolutionTimer = false;
        try
        {
            try { Thread.CurrentThread.Priority = ThreadPriority.AboveNormal; } catch { }
            highResolutionTimer = TimeBeginPeriod(1) == 0;
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(4));
            while (!cancellationToken.IsCancellationRequested)
            {
                GamepadSnapshot snapshot;
                if (!TryRead(out snapshot))
                {
                    _controllerIndex = -1;
                    snapshot = GamepadSnapshot.Disconnected(DateTimeOffset.Now);
                }

                Current = snapshot;
                try
                {
                    SnapshotReceived?.Invoke(this, snapshot);
                }
                catch
                {
                    // A UI exception must not stop controller polling.
                }

                try
                {
                    await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            if (highResolutionTimer)
            {
                TimeEndPeriod(1);
            }
        }
    }

    private bool TryRead(out GamepadSnapshot snapshot)
    {
        if (_controllerIndex >= 0 && TryReadSlot(_controllerIndex, out snapshot))
        {
            return true;
        }

        for (var index = 0; index < 4; index++)
        {
            if (TryReadSlot(index, out snapshot))
            {
                _controllerIndex = index;
                return true;
            }
        }

        snapshot = GamepadSnapshot.Disconnected(DateTimeOffset.Now);
        return false;
    }

    private static bool TryReadSlot(int index, out GamepadSnapshot snapshot)
    {
        if (XInputGetState(index, out var state) != 0)
        {
            snapshot = GamepadSnapshot.Disconnected(DateTimeOffset.Now);
            return false;
        }

        var gamepad = state.Gamepad;
        snapshot = new GamepadSnapshot(
            true,
            index,
            state.PacketNumber,
            (GamepadButtons)gamepad.Buttons,
            gamepad.LeftTrigger,
            gamepad.RightTrigger,
            gamepad.LeftThumbX,
            gamepad.LeftThumbY,
            gamepad.RightThumbX,
            gamepad.RightThumbY,
            DateTimeOffset.Now);
        return true;
    }

    public void Dispose() => Stop();

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint period);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint period);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState(int userIndex, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;
    }
}
