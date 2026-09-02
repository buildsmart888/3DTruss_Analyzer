namespace TrussAnalyzer.Core.Application;

using System.Text.Json;

public sealed record ApplicationSettings
{
    public string Language { get; init; } = "en";
    public int AutosaveMinutes { get; init; } = 5;
    public bool RestoreLastProject { get; init; } = true;
    public string? LastProjectPath { get; init; }
    public IReadOnlyList<string> RecentProjectPaths { get; init; } = Array.Empty<string>();
    public string WorkspaceLayout { get; init; } = "Default";
    public string ActiveStage { get; init; } = "Physical";
    public double WindowWidth { get; init; } = 1440;
    public double WindowHeight { get; init; } = 900;
    public double LeftPaneWidth { get; init; } = 220;
    public double RightPaneWidth { get; init; } = 280;
}

public sealed class ApplicationSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public ApplicationSettings Load(string path)
    {
        if (!File.Exists(path)) return new ApplicationSettings();
        return JsonSerializer.Deserialize<ApplicationSettings>(File.ReadAllText(path), Options) ?? new ApplicationSettings();
    }
    public void Save(string path, ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
    }
}

public sealed class BackgroundTaskService
{
    public Task<T> RunAsync<T>(Func<CancellationToken, T> action, IProgress<double>? progress = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => { cancellationToken.ThrowIfCancellationRequested(); progress?.Report(0); var value = action(cancellationToken); progress?.Report(1); return value; }, cancellationToken);
}

/// <summary>Schedules persistence work without coupling document state to a UI dispatcher.</summary>
public sealed class AutosaveScheduler : IDisposable
{
    private Timer? _timer;
    private Action? _action;
    public bool IsRunning => _timer is not null;

    public void Start(TimeSpan interval, Action action)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _timer?.Dispose();
        _timer = new Timer(_ => Trigger(), null, interval, interval);
    }

    public void Trigger() => _action?.Invoke();
    public void Stop() { _timer?.Dispose(); _timer = null; }
    public void Dispose() => Stop();
}

public static class RecentProjectList
{
    public static IReadOnlyList<string> Add(IReadOnlyList<string> existing, string path, int capacity = 10)
    {
        ArgumentNullException.ThrowIfNull(existing);
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        string fullPath = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        return new[] { fullPath }.Concat(existing.Where(item => !string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase))).Take(capacity).ToArray();
    }
}

public sealed class CrashLogWriter
{
    public void Write(string path, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.AppendAllText(path, $"[{DateTimeOffset.UtcNow:O}] {exception}{Environment.NewLine}");
    }
}
