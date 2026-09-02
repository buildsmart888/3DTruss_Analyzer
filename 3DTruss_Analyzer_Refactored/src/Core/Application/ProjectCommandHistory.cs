namespace TrussAnalyzer.Core.Application;

/// <summary>A toolkit-independent undo/redo contract for engineering-model edits.</summary>
public interface IProjectCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

public sealed class DelegateProjectCommand : IProjectCommand
{
    private readonly Action _execute;
    private readonly Action _undo;
    public DelegateProjectCommand(string description, Action execute, Action undo)
    {
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("A command description is required.", nameof(description)) : description;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
    }
    public string Description { get; }
    public void Execute() => _execute();
    public void Undo() => _undo();
}

public sealed class ProjectCommandHistory
{
    private readonly Stack<IProjectCommand> _undo = new();
    private readonly Stack<IProjectCommand> _redo = new();
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoDescription => _undo.TryPeek(out var command) ? command.Description : null;
    public string? RedoDescription => _redo.TryPeek(out var command) ? command.Description : null;
    public event EventHandler? Changed;

    public void Execute(IProjectCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!_undo.TryPop(out var command)) throw new InvalidOperationException("There is no command to undo.");
        command.Undo();
        _redo.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!_redo.TryPop(out var command)) throw new InvalidOperationException("There is no command to redo.");
        command.Execute();
        _undo.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
