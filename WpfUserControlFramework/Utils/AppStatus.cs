using System.Configuration;
using System.Data;
using System.IO;

namespace RestaurantPosWpf;

/// <summary>
/// Application-wide ambient context: single <see cref="PosDataAccess"/> (ODBC executor)
/// and single <see cref="Sql"/> (query catalogue). Usage matches the client-preferred pattern:
/// <code>
/// var dt = App.aps.pda.GetDataTable(App.aps.sql.SelectAllUsers(includeInactive: false), 60);
/// </code>
/// Construction order in <see cref="App.OnStartup"/>:
/// <list type="number">
/// <item><description>Read <c>SeedDummyDataOnStartup</c> from App.config.</description></item>
/// <item><description><see cref="EnsureRolesAndBootstrapUser"/> to guarantee the five fixed roles and the bootstrap <c>user_id = 1</c> exist.</description></item>
/// <item><description>If seeding is enabled, <see cref="ReseedDummyDataIfEnabled"/> wipes <c>is_seed = true</c> rows and reinserts the demo set.</description></item>
/// </list>
/// </summary>
public sealed class AppStatus
{
    internal Sql sql { get; } = new Sql();
    internal PosDataAccess pda { get; } = new PosDataAccess();

    /// <summary>
    /// <c>users.user_id</c> stamped into <c>auth_user_id</c> / <c>deleted_user_id</c> on writes.
    /// Defaults to the bootstrap system account (<c>1</c>) until a real sign-in flow lands.
    /// </summary>
    public int CurrentUserId { get; set; } = SystemBootstrapUserId;

    /// <summary>Bootstrap system <c>users.user_id</c> created by the migration SQL.</summary>
    public const int SystemBootstrapUserId = 1;

    /// <summary><c>roles.role_id</c> values fixed by the migration (Admin, Manager, Supervisor, User, System).</summary>
    public const int RoleIdAdmin = 1;
    public const int RoleIdManager = 2;
    public const int RoleIdSupervisor = 3;
    public const int RoleIdUser = 4;
    public const int RoleIdSystem = 5;

    /// <summary><c>SeedDummyDataOnStartup</c> from App.config; controls demo reseed on start.</summary>
    public bool SeedDummyDataOnStartup { get; }

    /// <summary>
    /// <c>DiagnosticLogging</c> from App.config. When <c>true</c>, <see cref="StartupLogPath"/> captures
    /// startup, seed, and <c>StaffAccessStore.LoadFromDb</c> traces. Default: <c>false</c> on live installs.
    /// </summary>
    public static bool DiagnosticLoggingEnabled { get; private set; }

    /// <summary>
    /// Primary IPv4 address of the terminal (e.g. <c>"10.1.2.17"</c>), stamped into
    /// <c>public.audit_trail.ip_address</c>. Cached briefly so Wi-Fi reconnects are eventually
    /// picked up without hammering the NIC table. Returns an empty string when no usable adapter
    /// is found; <see cref="Sql.Nullable"/> then writes <c>NULL</c>.
    /// </summary>
    public string LocalIpAddress => NetworkIdentity.GetLocalIpv4();

    public string DefaultConnectionString => PosDataAccess.CurrentSnapshot.CompactConnectionString ?? "";

    public AppStatus()
    {
        SeedDummyDataOnStartup = ReadBool("SeedDummyDataOnStartup", defaultValue: false);
        DiagnosticLoggingEnabled = ReadBool("DiagnosticLogging", defaultValue: false);
        if (DiagnosticLoggingEnabled)
        {
            TruncateStartupLog();
            Log("AppStatus ctor: SeedDummyDataOnStartup=" + SeedDummyDataOnStartup +
                ", DiagnosticLogging=true, driver=" + PosDataAccess.CurrentSnapshot.Driver +
                ", LocalIpAddress=" + (string.IsNullOrEmpty(LocalIpAddress) ? "<none>" : LocalIpAddress));
        }
    }

    private static void TruncateStartupLog()
    {
        try
        {
            lock (LogLock)
                File.WriteAllText(StartupLogPath, string.Empty);
        }
        catch
        {
            // Ignored: diagnostic log must never break startup.
        }
    }

    private static bool ReadBool(string key, bool defaultValue)
    {
        var v = ConfigurationManager.AppSettings[key];
        if (string.IsNullOrWhiteSpace(v))
            return defaultValue;
        return bool.TryParse(v.Trim(), out var b) ? b : defaultValue;
    }

    /// <summary>
    /// Guarantees the bootstrap system user (<c>user_id = 1</c>) and the five fixed roles exist.
    /// Safe to call on every startup; uses <c>ON CONFLICT DO NOTHING</c> so live rows keep their
    /// own descriptions/audit ids. The bootstrap user is inserted FIRST because
    /// <c>public.roles.auth_user_id</c> is a FK to <c>public.users.user_id</c>; the user's own
    /// <c>auth_user_id = 1</c> is a self-reference, which Postgres accepts on a single INSERT.
    /// </summary>
    public void EnsureRolesAndBootstrapUser()
    {
        Log("EnsureRolesAndBootstrapUser: start");
        if (!pda.CheckCurrentConnection())
        {
            Log("EnsureRolesAndBootstrapUser: CheckCurrentConnection=false (compact cnLocal missing / malformed)");
            return;
        }

        RunTransactionalLogged("EnsureBootstrapSystemUser", sql.EnsureBootstrapSystemUser());
        RunTransactionalLogged("EnsureFixedRoles", sql.EnsureFixedRoles());
        Log("EnsureRolesAndBootstrapUser: done");
    }

    /// <summary>
    /// When <see cref="SeedDummyDataOnStartup"/> is <c>true</c>, delete every <c>is_seed = true</c>
    /// row from <c>users</c> (leaving live data untouched) and reinsert the demo set. This method
    /// writes everything it does to the startup log (see <see cref="StartupLogPath"/>) and shows a
    /// MessageBox on any failure, because seed mode is a development-only path.
    /// </summary>
    public void ReseedDummyDataIfEnabled()
    {
        Log("ReseedDummyDataIfEnabled: SeedDummyDataOnStartup=" + SeedDummyDataOnStartup);
        if (!SeedDummyDataOnStartup)
            return;

        var compact = PosDataAccess.CurrentSnapshot.CompactConnectionString ?? "<null>";
        Log("ReseedDummyDataIfEnabled: cnLocal=" + MaskPassword(compact));

        if (!pda.CheckCurrentConnection())
        {
            Log("ReseedDummyDataIfEnabled: CheckCurrentConnection returned false; seed skipped.");
            System.Windows.MessageBox.Show(
                "SeedDummyDataOnStartup=true but PostgreSQL is not reachable.\n\nCheck App.config cnLocal and PostgreSqlOdbcDriver.\n\nSee " + StartupLogPath + " for details.",
                "Seed skipped",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var before = CountSeedUsers();
        Log("ReseedDummyDataIfEnabled: is_seed row count before delete = " + before);

        RunTransactionalLogged("DeleteAllSeedRows", sql.DeleteAllSeedRows());

        var insertSql = sql.InsertSeedUsers();
        Log("ReseedDummyDataIfEnabled: InsertSeedUsers SQL length = " + insertSql.Length);
        RunTransactionalLogged("InsertSeedUsers", insertSql);

        var after = CountSeedUsers();
        Log("ReseedDummyDataIfEnabled: is_seed row count after insert = " + after);

        if (after == 0)
        {
            System.Windows.MessageBox.Show(
                "SeedDummyDataOnStartup=true but 0 seed users were written to public.users.\n\n" +
                "Open " + StartupLogPath + " for the full SQL error.\n\n" +
                "Most common causes:\n" +
                " - The 2026-04-21_staff_access_init.sql migration has not been applied (missing columns).\n" +
                " - A CHECK/UNIQUE constraint rejected the demo rows.\n" +
                " - cnLocal in App.config points at the wrong database.",
                "Seed produced no rows",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Executes <paramref name="sqlText"/> transactionally using a local try/catch so the exception
    /// is captured, logged, and surfaced — instead of being swallowed by
    /// <see cref="PosDataAccess.SetSql(bool,string,bool)"/> when <c>showError</c> is false.
    /// </summary>
    private void RunTransactionalLogged(string label, string sqlText)
    {
        try
        {
            using var conn = new System.Data.Odbc.OdbcConnection(pda.BuildPostgresConnectionString());
            conn.Open();
            using var cmd = new System.Data.Odbc.OdbcCommand(sqlText, conn) { CommandTimeout = 600 };
            using var tx = conn.BeginTransaction();
            cmd.Transaction = tx;
            try
            {
                var n = cmd.ExecuteNonQuery();
                tx.Commit();
                Log(label + ": OK (rows affected=" + n + ")");
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            Log(label + ": FAILED - " + ex.GetType().Name + " - " + ex.Message);
            System.Windows.MessageBox.Show(
                label + " failed:\n\n" + ex.Message + "\n\nSee " + StartupLogPath,
                "Seed SQL error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private int CountSeedUsers()
    {
        try
        {
            var dt = pda.GetDataTable(
                "SELECT COUNT(*)::int AS n FROM public.users WHERE is_seed = TRUE",
                15);
            if (dt.Rows.Count > 0)
            {
                var raw = dt.Rows[0]["n"];
                if (raw != null && raw != DBNull.Value && int.TryParse(raw.ToString(), out var n))
                    return n;
            }
        }
        catch (Exception ex)
        {
            Log("CountSeedUsers failed: " + ex.GetType().Name + " - " + ex.Message);
        }
        return 0;
    }

    // region: Startup diagnostic log -----------------------------------------------------------------
    //
    // Writes a small, persistent log to %TEMP%\PeoplePosStartup.log on every app start. This is the
    // authoritative trace when the seed path misbehaves (silent MessageBox dismissals, unattended
    // launches, etc.). Safe to delete; regenerated on next launch.

    /// <summary>Absolute path of the diagnostic log written during startup and seed operations.</summary>
    public static string StartupLogPath { get; } =
        Path.Combine(Path.GetTempPath(), "PeoplePosStartup.log");

    private static readonly object LogLock = new();

    private static void Log(string line)
    {
        System.Diagnostics.Debug.WriteLine("[AppStatus] " + line);
        if (!DiagnosticLoggingEnabled)
            return;
        var stamped = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + line;
        try
        {
            lock (LogLock)
                File.AppendAllText(StartupLogPath, stamped + Environment.NewLine);
        }
        catch
        {
            // Ignored: diagnostic log must never break startup.
        }
    }

    private static string MaskPassword(string compact)
    {
        // Compact string format: user,password,host,port,database (commas or spaces).
        var parts = compact.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return compact;
        parts[1] = "***";
        return string.Join(",", parts);
    }

    /// <summary>Convenience wrapper to run a query with a 60s timeout on the default connection.</summary>
    public DataTable Query(string sqlText, int commandTimeoutSeconds = 60) =>
        pda.GetDataTable(sqlText, commandTimeoutSeconds);

    /// <summary>Convenience wrapper for transactional non-queries on the default connection.</summary>
    public void Execute(string sqlText, bool showError = false) =>
        pda.SetSql(showError, sqlText, transactional: true);

    /// <summary>
    /// Transactional execute that <b>rethrows</b> on failure. Use this for writes where the
    /// caller must know if the row actually landed (e.g. audit inserts), so that silent ODBC
    /// failures cannot make data vanish without a trace.
    /// </summary>
    public void ExecuteStrict(string sqlText) =>
        pda.SetSqlStrict(sqlText, transactional: true);
}
