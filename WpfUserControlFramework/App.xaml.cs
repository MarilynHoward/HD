using System.Diagnostics;
using System.IO;
using System.Windows;

namespace RestaurantPosWpf;

public partial class App : Application
{
    /// <summary>
    /// Application-wide ambient context. All data access goes through <c>App.aps.pda</c> + <c>App.aps.sql</c>.
    /// See the workspace rule <c>wpf-postgresql-appstatus-pattern</c>.
    /// </summary>
    public static AppStatus aps { get; } = new AppStatus();

    /// <summary>
    /// Set environment variable <c>PEOPLEPOS_OPS_TRACE=1</c> before launch to append to
    /// <c>%TEMP%\PeoplePosOpsTrace.log</c> (Operations / reservations modal diagnostics).
    /// </summary>
    internal static bool OpsTraceEnabled { get; private set; }

    internal static void OpsTrace(string message)
    {
        if (!OpsTraceEnabled)
            return;
        Trace.WriteLine($"[{DateTime.UtcNow:O}] {message}");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("PEOPLEPOS_OPS_TRACE"), "1",
                StringComparison.Ordinal))
        {
            var path = Path.Combine(Path.GetTempPath(), "PeoplePosOpsTrace.log");
            Trace.Listeners.Add(new TextWriterTraceListener(path));
            Trace.AutoFlush = true;
            OpsTraceEnabled = true;
            OpsTrace($"Trace listener attached: {path}");
        }

        try
        {
            aps.EnsureRolesAndBootstrapUser();
            aps.ReseedDummyDataIfEnabled();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[App.OnStartup] AppStatus bootstrap failed: " + ex.Message);
        }

        base.OnStartup(e);
    }
}
