using System.IO.Pipes;

namespace GamepadPilot.App.Platform;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Local\\GamepadPilot-1.7.3-SingleInstance";
    private const string PipeName = "GamepadPilot-1.7.3-Activate";
    private readonly Mutex _mutex;
    private CancellationTokenSource? _cancellation;

    public SingleInstanceService()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
    }

    public bool IsFirstInstance { get; }

    public void StartListener(Action activate)
    {
        if (!IsFirstInstance)
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        _ = Task.Run(() => ListenAsync(activate, _cancellation.Token));
    }

    public static void ActivateExisting()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(800);
            client.WriteByte(1);
        }
        catch
        {
        }
    }

    private static async Task ListenAsync(Action activate, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                if (server.ReadByte() >= 0)
                {
                    activate();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        if (IsFirstInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
            }
        }
        _mutex.Dispose();
    }
}
