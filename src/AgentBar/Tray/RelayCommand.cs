using System;
using System.Windows.Input;

namespace AgentBar.Tray;

/// Minimal always-executable ICommand for wiring popover buttons to a handler.
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;

    public RelayCommand(Action<object?> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
