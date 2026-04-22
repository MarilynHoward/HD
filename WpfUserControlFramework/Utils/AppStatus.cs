using System.Configuration;
using System.Data;
using System.IO;

namespace RestaurantPosWpf;

/// <summary>
/// Application-wide ambient context: single <see cref="PosDataAccess"/> (ODBC executor)
/// and single <see cref="Sql"/> (query catalogue). Every CRUD call explicitly passes the
/// branch-qualified compact connection string obtained from
/// <see cref="LocalConnectionstring(string)"/> — by client decree there is no implicit default.
/// <para>
/// External callers (anything outside <see cref="AppStatus"/>) MUST always pass
/// <see cref="propertyBranchCode"/> as the argument. The client's wider codebase has other
/// <c>LocalConnectionstring</c> overloads and relying on the parameterless form here creates
/// cross-assembly ambiguity, so the one-argument call is the canonical form.
/// </para>
/// <code>
/// var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
/// var dt = App.aps.pda.GetDataTable(cn, App.aps.sql.SelectAllUsers(includeInactive: false), 60);
/// </code>
/// Construction order in <see cref="App.OnStartup"/>:
/// <list type="number">
/// <item><description>Read <c>Branch.txt</c> into <see cref="propertyBranchCode"/>.</description></item>
/// <item><description><see cref="LocalConnectionstring(string)"/> resolves and caches the branch-qualified compact.</description></item>
/// <item><description>Read <c>SeedDummyDataOnStartup</c> from App.config.</description></item>
/// <item><description><see cref="EnsureRolesAndBootstrapUser"/> to guarantee the five fixed roles and the bootstrap <c>user_id = 1</c> exist.</description></item>
/// <item><description>If seeding is enabled, <see cref="ReseedDummyDataIfEnabled"/> wipes <c>is_seed = true</c> rows and reinserts the demo set.</description></item>
/// </list>
/// </summary>
public sealed class AppStatus
{
    internal Sql sql { get; } = new Sql();

    /// <summary>
    /// Data-access singleton. Holds the driver but not the compact connection string — by client
    /// decree every CRUD call passes the compact explicitly. Callers obtain it via
    /// <see cref="LocalConnectionstring(string)"/> and thread it through, e.g.
    /// <c>App.aps.pda.GetDataTable(App.aps.LocalConnectionstring(App.aps.propertyBranchCode), sql, timeout)</c>.
    /// </summary>
    internal PosDataAccess pda { get; } = new PosDataAccess();

    /// <summary>
    /// Password cipher used to encrypt/decrypt <c>users.password</c> on every save, display, seed,
    /// and reseed path. By client decree this replaces any hashing scheme (notably the removed
    /// <c>PasswordHasher</c>); call <c>App.aps.crypt.DoEncrypt(plain)</c> before writing the column
    /// and <c>App.aps.crypt.DoDecrypt(stored)</c> when reading it back for display.
    /// </summary>
    internal Crypt crypt = new Crypt();

    /// <summary>
    /// Backing store for <see cref="LocalConnectionstring(string)"/>. Cached after first successful
    /// resolution so every CRUD call reuses the same branch-qualified compact connection string.
    /// </summary>
    private string sConnectionstring = string.Empty;

    /// <summary>
    /// <c>users.user_id</c> stamped into <c>auth_user_id</c> / <c>deleted_user_id</c> on writes.
    /// Defaults to the bootstrap system account (<c>1</c>) until a real sign-in flow lands.
    /// </summary>
    public int signedOnUserId { get; set; } = SystemBootstrapUserId;

    internal DataRow userInfo = null;
    internal string signedOnUserName = "";
    internal string signedOnUserRole = "";
    internal Int32 signedOnUserRoleId = -1;
    internal Int32 systemUserId = -1;
    internal string systemUserName = "";
    internal string signedOnUserImagePath = "";
    internal AppVersion apv = new AppVersion();

    /// <summary>
    /// Branch identifier for this terminal, read at startup from <c>Branch.txt</c> located in the
    /// folder configured by <c>StaffDocumentsRepositoryRoot</c> in App.config (see
    /// <c>App.OnStartup</c>). Empty until startup succeeds; startup aborts with an error message
    /// when the file is missing or blank.
    /// </summary>
    public string propertyBranchCode { get; set; } = string.Empty;

    /// <summary>Bootstrap system <c>users.user_id</c> created by the migration SQL.</summary>
    public const int SystemBootstrapUserId = 0;

    /// <summary>
    /// <c>roles.role_id</c> values fixed by the migration (Developer, Admin, Manager, Supervisor,
    /// User, System). <c>Developer</c> (<c>0</c>) is a selectable engineering role — it is shown in
    /// the UI role picker alongside Admin/Manager/Supervisor/User and is protected from soft-delete;
    /// only <c>System</c> remains hidden from the picker.
    /// </summary>
    public const int RoleIdDeveloper = 0;
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

    /// <summary>
    /// Resolves the per-branch compact connection string every CRUD operation must use. Reads
    /// <c>cnLocal</c> from App.config on first call and substitutes the literal token <c>branch</c>
    /// (the DB-name placeholder) with the effective branch code — either <paramref name="branchCode"/>
    /// when supplied, or <see cref="propertyBranchCode"/> when it was populated at startup from
    /// <c>Branch.txt</c>. The resolved string is cached in <see cref="sConnectionstring"/> and the
    /// <see cref="pda"/> singleton is rebuilt so callers (<c>App.aps.pda.GetDataTable</c>, etc.) route
    /// through the branch-qualified connection without ever reaching back to App.config.
    /// <para>
    /// Returns an empty string when no branch code is known and no prior string was cached — the
    /// caller should surface this as a bootstrap error (startup enforces this via
    /// <c>App.OnStartup</c> / <c>Branch.txt</c>).
    /// </para>
    /// <para>
    /// <b>External calling convention:</b> any code outside <see cref="AppStatus"/> must always pass
    /// <see cref="propertyBranchCode"/> explicitly, i.e.
    /// <c>App.aps.LocalConnectionstring(App.aps.propertyBranchCode)</c>. The client's wider codebase
    /// has other <c>LocalConnectionstring</c> methods, and the parameterless form here makes call
    /// sites ambiguous when read against that ecosystem. Internal calls inside this class are allowed
    /// to use the default (<c>LocalConnectionstring()</c>) because the member-lookup is unambiguous.
    /// </para>
    /// </summary>
    internal string LocalConnectionstring(string branchCode = "")
    {
        branchCode = string.IsNullOrWhiteSpace(branchCode) ? "" : branchCode.Trim();
        propertyBranchCode = string.IsNullOrWhiteSpace(propertyBranchCode) ? "" : propertyBranchCode.Trim();

        if (!branchCode.Equals(""))
        {
            propertyBranchCode = branchCode;
        }
        else if (!propertyBranchCode.Equals(""))
        {
            branchCode = propertyBranchCode;
        }

        try
        {
            if (branchCode.Equals("") && propertyBranchCode.Equals("") && sConnectionstring.Trim().Equals(""))
            {
                return "";
            }

            if (string.IsNullOrEmpty(sConnectionstring))
            {
                var raw = (ConfigurationManager.AppSettings["cnLocal"] ?? "").Trim();
                sConnectionstring = raw.Replace("branch", branchCode);
            }
        }
        catch (Exception ex)
        {
            sConnectionstring = "";
            Log("LocalConnectionstring error: " + ex.GetType().Name + " - " + ex.Message);
        }

        return sConnectionstring;
    }

    public AppStatus()
    {
        SeedDummyDataOnStartup = ReadBool("SeedDummyDataOnStartup", defaultValue: false);
        DiagnosticLoggingEnabled = ReadBool("DiagnosticLogging", defaultValue: false);
        if (DiagnosticLoggingEnabled)
        {
            TruncateStartupLog();
            Log("AppStatus ctor: SeedDummyDataOnStartup=" + SeedDummyDataOnStartup +
                ", DiagnosticLogging=true, driver=" + PosDataAccess.DefaultDriver +
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
        var cn = LocalConnectionstring();
        if (!PosDataAccess.CheckBranchConnection(cn))
        {
            Log("EnsureRolesAndBootstrapUser: CheckBranchConnection=false (branch connection missing / malformed)");
            return;
        }

        RunTransactionalLogged("EnsureBootstrapSystemUser", sql.EnsureBootstrapSystemUser(), cn);
        RunTransactionalLogged("EnsureFixedRoles", sql.EnsureFixedRoles(), cn);
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

        var cn = LocalConnectionstring();
        Log("ReseedDummyDataIfEnabled: branchConnection=" + MaskPassword(string.IsNullOrEmpty(cn) ? "<null>" : cn));

        if (!PosDataAccess.CheckBranchConnection(cn))
        {
            Log("ReseedDummyDataIfEnabled: CheckBranchConnection returned false; seed skipped.");
            System.Windows.MessageBox.Show(
                "SeedDummyDataOnStartup=true but PostgreSQL is not reachable.\n\nCheck App.config cnLocal and PostgreSqlOdbcDriver.\n\nSee " + StartupLogPath + " for details.",
                "Seed skipped",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var before = CountSeedUsers(cn);
        Log("ReseedDummyDataIfEnabled: is_seed row count before delete = " + before);

        RunTransactionalLogged("DeleteAllSeedRows", sql.DeleteAllSeedRows(), cn);

        var insertSql = sql.InsertSeedUsers();
        Log("ReseedDummyDataIfEnabled: InsertSeedUsers SQL length = " + insertSql.Length);
        RunTransactionalLogged("InsertSeedUsers", insertSql, cn);

        var after = CountSeedUsers(cn);
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
    /// <see cref="PosDataAccess.SetSql(string,bool,string,bool)"/> when <c>showError</c> is false.
    /// </summary>
    private void RunTransactionalLogged(string label, string sqlText, string compactConnectionString)
    {
        try
        {
            using var conn = new System.Data.Odbc.OdbcConnection(pda.BuildPostgresConnectionString(compactConnectionString));
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

    private int CountSeedUsers(string compactConnectionString)
    {
        try
        {
            var dt = pda.GetDataTable(
                compactConnectionString,
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

    /// <summary>
    /// Convenience wrapper to run a query. Caller must pass the branch-qualified compact connection
    /// string obtained from <c>App.aps.LocalConnectionstring(App.aps.propertyBranchCode)</c> —
    /// there is no hidden default, and external callers must always pass <see cref="propertyBranchCode"/>
    /// to disambiguate from other <c>LocalConnectionstring</c> overloads in the client's wider codebase.
    /// </summary>
    public DataTable Query(string compactConnectionString, string sqlText, int commandTimeoutSeconds = 60) =>
        pda.GetDataTable(compactConnectionString, sqlText, commandTimeoutSeconds);

    /// <summary>
    /// Convenience wrapper for transactional non-queries. Caller must pass the branch-qualified
    /// compact connection string obtained from
    /// <c>App.aps.LocalConnectionstring(App.aps.propertyBranchCode)</c>.
    /// </summary>
    public void Execute(string compactConnectionString, string sqlText, bool showError = false) =>
        pda.SetSql(compactConnectionString, showError, sqlText, transactional: true);

    /// <summary>
    /// Transactional execute that <b>rethrows</b> on failure. Use this for writes where the caller
    /// must know if the row actually landed (e.g. audit inserts), so that silent ODBC failures
    /// cannot make data vanish without a trace. Caller must pass the branch-qualified compact
    /// connection string obtained from
    /// <c>App.aps.LocalConnectionstring(App.aps.propertyBranchCode)</c>.
    /// </summary>
    public void ExecuteStrict(string compactConnectionString, string sqlText) =>
        pda.SetSqlStrict(compactConnectionString, sqlText, transactional: true);
}
