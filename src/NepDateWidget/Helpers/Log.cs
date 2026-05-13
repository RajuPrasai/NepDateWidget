using NepDateWidget.Services;

namespace NepDateWidget.Helpers;

/// <summary>
/// Static thin façade over <see cref="ILogService"/> so any class can call
/// <c>Log.Action("...")</c> without constructor injection.
/// Call <see cref="Initialize"/> once at startup before emitting any entries.
/// Call <see cref="Shutdown"/> in <c>App.OnExit</c> to drain pending entries.
/// </summary>
public static class Log
{
    private static ILogService? _svc;

    /// <summary>Must be called once in App.OnStartup before any logging.</summary>
    public static void Initialize(ILogService service) => _svc = service;

    /// <summary>
    /// Drains the log queue and disposes the underlying service.
    /// Must be called in App.OnExit before process terminates.
    /// </summary>
    public static void Shutdown() => (_svc as IDisposable)?.Dispose();

    public static void Info(string message) => _svc?.Info(message);
    public static void Action(string message) => _svc?.Action(message);
    public static void Warn(string message) => _svc?.Warn(message);

    public static void Error(string message, Exception? ex = null) => _svc?.Error(message, ex);
    public static void Fatal(string message, Exception? ex = null) => _svc?.Fatal(message, ex);

    /// <summary>Forwards a cap change from Settings to the underlying service.</summary>
    public static void UpdateMaxSize(int maxSizeMb) => _svc?.UpdateMaxSize(maxSizeMb);
}
