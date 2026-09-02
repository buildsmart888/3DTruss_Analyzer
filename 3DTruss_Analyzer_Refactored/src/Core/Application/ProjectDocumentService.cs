namespace TrussAnalyzer.Core.Application;

using TrussAnalyzer.Core.Domain.V1;
using TrussAnalyzer.Core.IO.Projects;

/// <summary>Owns the active versioned project and its save/dirty lifecycle without depending on any UI toolkit.</summary>
public sealed class ProjectDocumentService
{
    private readonly IProjectFileStore _store;

    public ProjectDocumentService(IProjectFileStore? store = null) => _store = store ?? new GosaProjectStore();

    public ProjectDocument? Current { get; private set; }
    public string? CurrentPath { get; private set; }
    public bool IsDirty { get; private set; }
    public event EventHandler? Changed;

    public ProjectDocument CreateNew(ProjectInfo? info = null)
    {
        Current = new ProjectDocument { ProjectInfo = info ?? new ProjectInfo() };
        CurrentPath = null;
        IsDirty = false;
        NotifyChanged();
        return Current;
    }

    public ProjectDocument Open(string path)
    {
        Current = _store.Load(path);
        CurrentPath = Path.GetFullPath(path);
        IsDirty = false;
        NotifyChanged();
        return Current;
    }

    public void Replace(ProjectDocument document, bool dirty = true)
    {
        Current = document ?? throw new ArgumentNullException(nameof(document));
        IsDirty = dirty;
        NotifyChanged();
    }

    public void MarkDirty()
    {
        EnsureCurrent();
        if (!IsDirty)
        {
            IsDirty = true;
            NotifyChanged();
        }
    }

    public void Save() => SaveCore(RequirePath());

    public void SaveAs(string path)
    {
        CurrentPath = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        SaveCore(CurrentPath);
    }

    public void Autosave()
    {
        EnsureCurrent();
        if (CurrentPath is not null)
            _store.SaveAutosave(CurrentPath, Current!);
    }

    public ProjectRecoveryResult Recover(string primaryPath)
    {
        var result = _store.RecoverLatest(primaryPath);
        Current = result.Document;
        CurrentPath = result.SourcePath;
        IsDirty = false;
        NotifyChanged();
        return result;
    }

    private void SaveCore(string path)
    {
        EnsureCurrent();
        _store.SaveAtomic(path, Current!);
        CurrentPath = path;
        IsDirty = false;
        NotifyChanged();
    }

    private string RequirePath() => CurrentPath ?? throw new InvalidOperationException("The project has no path. Use SaveAs first.");
    private void EnsureCurrent() { if (Current is null) throw new InvalidOperationException("No active project document."); }
    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
