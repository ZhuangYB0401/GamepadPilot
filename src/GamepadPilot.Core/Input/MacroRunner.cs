using System.Collections.Concurrent;
using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Services;

namespace GamepadPilot.Core.Input;

public sealed class MacroRunner : IDisposable
{
    private readonly IMacroOutput _output;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

    public MacroRunner(IMacroOutput output) => _output = output;

    public bool IsRunning(Guid macroId) => _running.ContainsKey(macroId);

    public void Start(MacroDefinition macro)
    {
        Stop(macro.Id);
        var cancellation = new CancellationTokenSource();
        if (!_running.TryAdd(macro.Id, cancellation))
        {
            cancellation.Dispose();
            return;
        }

        _ = Task.Run(() => RunAsync(macro, cancellation.Token));
    }

    public void Stop(Guid macroId)
    {
        if (_running.TryRemove(macroId, out var cancellation))
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    public void StopAll()
    {
        foreach (var pair in _running.ToArray())
        {
            Stop(pair.Key);
        }
    }

    private async Task RunAsync(MacroDefinition macro, CancellationToken cancellationToken)
    {
        var heldKeys = new HashSet<uint>();
        var heldButtons = new HashSet<MouseButtonKind>();
        try
        {
            var iterations = macro.RepeatMode switch
            {
                MacroRepeatMode.Once => 1,
                MacroRepeatMode.Count => Math.Max(1, macro.RepeatCount),
                MacroRepeatMode.WhileHeld or MacroRepeatMode.Toggle => int.MaxValue,
                _ => 1
            };
            var speed = Math.Clamp(macro.Speed, 0.1d, 10d);

            for (var iteration = 0; iteration < iterations && !cancellationToken.IsCancellationRequested; iteration++)
            {
                foreach (var step in macro.Steps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    switch (step.Type)
                    {
                        case MacroStepType.Delay:
                            await Task.Delay(Math.Max(0, (int)(step.DelayMs / speed)), cancellationToken).ConfigureAwait(false);
                            break;
                        case MacroStepType.KeyDown:
                            _output.KeyDown(step.VirtualKey);
                            heldKeys.Add(step.VirtualKey);
                            break;
                        case MacroStepType.KeyUp:
                            _output.KeyUp(step.VirtualKey);
                            heldKeys.Remove(step.VirtualKey);
                            break;
                        case MacroStepType.MouseButtonDown:
                            _output.MouseButtonDown(step.MouseButton);
                            heldButtons.Add(step.MouseButton);
                            break;
                        case MacroStepType.MouseButtonUp:
                            _output.MouseButtonUp(step.MouseButton);
                            heldButtons.Remove(step.MouseButton);
                            break;
                        case MacroStepType.MouseWheel:
                            _output.MouseWheel(step.WheelDelta, 0);
                            break;
                        case MacroStepType.MouseMove:
                            _output.MouseMove(step.DeltaX, step.DeltaY);
                            break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            foreach (var key in heldKeys)
            {
                _output.KeyUp(key);
            }

            foreach (var button in heldButtons)
            {
                _output.MouseButtonUp(button);
            }

            if (_running.TryRemove(macro.Id, out var cancellation))
            {
                cancellation.Dispose();
            }
        }
    }

    public void Dispose() => StopAll();
}
