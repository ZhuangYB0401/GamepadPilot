using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Input;
using GamepadPilot.Core.Services;

namespace GamepadPilot.App.Platform;

public sealed class InputDispatcher : IActionDispatcher, IDisposable
{
    private readonly WindowsInputSink _sink;
    private readonly MacroRunner _macroRunner;
    private IReadOnlyDictionary<Guid, MacroDefinition> _macros = new Dictionary<Guid, MacroDefinition>();

    public InputDispatcher(WindowsInputSink sink)
    {
        _sink = sink;
        _macroRunner = new MacroRunner(sink);
    }

    public void SetMacros(IEnumerable<MacroDefinition> macros)
    {
        _macroRunner.StopAll();
        _macros = macros.ToDictionary(x => x.Id);
    }

    public void Execute(ActionDefinition action)
    {
        if (action.Type == ActionType.Macro)
        {
            var macro = FindMacro(action);
            if (macro is null)
            {
                return;
            }

            if (macro.RepeatMode == MacroRepeatMode.Toggle && _macroRunner.IsRunning(macro.Id))
            {
                _macroRunner.Stop(macro.Id);
            }
            else
            {
                _macroRunner.Start(macro);
            }
            return;
        }

        _sink.Execute(action);
    }

    public void Press(ActionDefinition action)
    {
        if (action.Type == ActionType.Macro)
        {
            var macro = FindMacro(action);
            if (macro is not null)
            {
                _macroRunner.Start(macro);
            }
            return;
        }

        _sink.Press(action);
    }

    public void Release(ActionDefinition action)
    {
        if (action.Type == ActionType.Macro)
        {
            if (action.MacroId is { } id)
            {
                _macroRunner.Stop(id);
            }
            return;
        }

        _sink.Release(action);
    }

    public void Continuous(ActionDefinition action, double x, double y, double seconds) => _sink.Continuous(action, x, y);

    public void ReleaseAll()
    {
        _macroRunner.StopAll();
        _sink.ReleaseAll();
    }

    private MacroDefinition? FindMacro(ActionDefinition action) =>
        action.MacroId is { } id && _macros.TryGetValue(id, out var macro) ? macro : null;

    public void Dispose()
    {
        _macroRunner.Dispose();
        _sink.ReleaseAll();
    }
}
