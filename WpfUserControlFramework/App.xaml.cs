using System.Configuration;
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

        if (!LoadBranchCode())
        {
            Shutdown(exitCode: 1);
            return;
        }

        var branchConnection = aps.LocalConnectionstring(aps.propertyBranchCode);
        if (string.IsNullOrWhiteSpace(branchConnection))
        {
            MessageBox.Show(
                "Could not resolve the branch connection string from App.config.\r\n" +
                "Check that 'cnLocal' is defined and contains the literal token 'branch' as the database placeholder.",
                "EnvironmentCheck",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(exitCode: 1);
            return;
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

        aps.RegisterReportingSyncCoordinator();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        aps.CancelReportingSyncCoordinator();
        base.OnExit(e);
    }

    /// <summary>
    /// Reads the branch code from <c>Branch.txt</c> under <c>StaffDocumentsRepositoryRoot</c> (App.config)
    /// and stamps it into <see cref="AppStatus.propertyBranchCode"/>. Shows a blocking error and returns
    /// <c>false</c> when the folder is not configured, the file is missing, or the file is empty — the
    /// caller aborts startup in that case because every downstream subsystem requires a branch code.
    /// </summary>
    private static bool LoadBranchCode()
    {
        var root = ConfigurationManager.AppSettings["StaffDocumentsRepositoryRoot"];
        if (string.IsNullOrWhiteSpace(root))
        {
            MessageBox.Show(
                "StaffDocumentsRepositoryRoot is not configured in App.config.\r\n" +
                "Set it to the folder that contains Branch.txt and restart the application.",
                "EnvironmentCheck",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        var branchFile = Path.Combine(root, "Branch.txt");

        StreamReader? sr = null;
        try
        {
            sr = new StreamReader(branchFile);
            if (sr.Peek() < 0)
            {
                MessageBox.Show(
                    "Branch File not found.",
                    "EnvironmentCheck",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            aps.propertyBranchCode = (sr.ReadLine() ?? string.Empty).Trim();
        }
        catch (FileNotFoundException)
        {
            MessageBox.Show(
                "Branch File not found.\r\n\r\nExpected at: " + branchFile,
                "EnvironmentCheck",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            MessageBox.Show(
                "Branch File not found.\r\n\r\nExpected at: " + branchFile,
                "EnvironmentCheck",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Branch File could not be read.\r\n\r\n" + ex.Message,
                "EnvironmentCheck",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
        finally
        {
            if (sr != null)
            {
                sr.Close();
                sr.Dispose();
                sr = null;
            }
        }

        if (string.IsNullOrWhiteSpace(aps.propertyBranchCode))
        {
            MessageBox.Show(
                "Branch File is empty.\r\n\r\nFile: " + branchFile,
                "EnvironmentCheck",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        return true;
    }
}
