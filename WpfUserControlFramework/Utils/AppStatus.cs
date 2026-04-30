using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

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
/// <item><description>If seeding is enabled, <see cref="ReseedDummyDataIfEnabled"/> wipes demo users/ops (<c>is_seed = true</c>), <c>TRUNCATE</c>s <c>rpt_report_access_log</c>, and reinserts the demo sets.</description></item>
/// <item><description>Best-effort async <see cref="StartRemoteControlLookupSync"/> runs one pipeline: when <see cref="SeedDummyDataOnStartup"/> is true, first dev-only <c>TRUNCATE</c>/reload of <c>public.rpt_daily_sales</c> on each remote branch DB (via <see cref="ServerConnectionstring(string)"/>); then pulls <c>rpt_*</c> lookups (branches, channels, user roles, report categories, reports) from <c>POS_CONTROL</c> into local; then consolidates peers&apos; <c>rpt_daily_sales</c> into local (home <see cref="propertyBranchCode"/> excluded). Failures are logged only.</description></item>
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
    private string sServerConnectionstring  = string.Empty;

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

    internal string ServerConnectionstring()
    {

        if (sServerConnectionstring.Equals(""))
        {
            sServerConnectionstring = crypt.DoDecrypt(ConfigurationManager.AppSettings["cnCloud"].Trim());
        }
        return sServerConnectionstring;

    }    

    internal string ServerConnectionstring(string branchCode = "")
    {
        if (sServerConnectionstring.Equals(""))
        {
            sServerConnectionstring = crypt.DoDecrypt(ConfigurationManager.AppSettings["cnCloud"].Trim());
        }
        return sServerConnectionstring.Replace("POS_CONTROL", branchCode);
    }        

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
                var raw = crypt.DoDecrypt((ConfigurationManager.AppSettings["cnLocal"] ?? "").Trim());
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
    /// Guarantees the bootstrap system user (<c>user_id = SystemBootstrapUserId</c>) and the six fixed
    /// roles (Developer, Admin, Manager, Supervisor, User, System) exist. Safe to call on every startup:
    /// every emitted <c>INSERT</c> is guarded by <c>WHERE NOT EXISTS</c> (PostgreSQL 9.3 has no
    /// <c>ON CONFLICT</c>) so live rows keep their own descriptions / audit ids. The bootstrap user is
    /// inserted FIRST because <c>public.roles.auth_user_id</c> is a FK to <c>public.users.user_id</c>;
    /// the user's own <c>auth_user_id</c> is a self-reference, which Postgres accepts on a single INSERT.
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
    /// When <see cref="SeedDummyDataOnStartup"/> is <c>true</c>, runs the dev reset: delete/reinsert
    /// seed-only <c>users</c>; <c>TRUNCATE</c> <c>rpt_report_access_log</c> then insert demo access rows;
    /// delete/reinsert Operations <c>is_seed</c> demo data. Live users stay untouched. This method
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

        RunTransactionalLogged(
            "TruncateAndSeedRptReportAccessLog",
            sql.TruncateRptReportAccessLog() + sql.InsertSeedRptReportAccessLog(signedOnUserId, signedOnUserId),
            cn);

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

        // Operations and Services reseed. Same contract as the user seed: wipe is_seed=TRUE rows
        // from every ops_* table, then insert the canned demo set. Lives alongside the user seed
        // so a single SeedDummyDataOnStartup=true pass refreshes both modules.
        Log("ReseedDummyDataIfEnabled: opsSeedBefore rows = " + CountSeedOpsRows(cn));
        RunTransactionalLogged("DeleteAllOpsSeedRows", sql.DeleteAllOpsSeedRows(), cn);
        var opsSeedSql = sql.InsertSeedOpsServices();
        Log("ReseedDummyDataIfEnabled: InsertSeedOpsServices SQL length = " + opsSeedSql.Length);
        RunTransactionalLogged("InsertSeedOpsServices", opsSeedSql, cn);
        Log("ReseedDummyDataIfEnabled: opsSeedAfter rows = " + CountSeedOpsRows(cn));
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

    /// <summary>
    /// Same transactional execution as <see cref="RunTransactionalLogged"/> but logs failures only
    /// (no <c>MessageBox</c>). Used for background remote→local RPT lookup sync and dev-only branch seeds.
    /// </summary>
    /// <returns><c>true</c> when the batch commits successfully.</returns>
    private bool RunTransactionalSilent(string label, string sqlText, string compactConnectionString)
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
                return true;
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
            return false;
        }
    }

    /// <summary>
    /// Starts a background sync of <c>public.rpt_branches</c>, <c>public.rpt_channels</c>, and
    /// <c>public.rpt_user_roles</c> from the central control database into the local branch DB.
    /// Reads use <see cref="ServerConnectionstring()"/> (no branch substitution). Writes use
    /// <see cref="LocalConnectionstring(string)"/> with <see cref="propertyBranchCode"/>.
    /// Pipeline order: optional dev remote <c>rpt_daily_sales</c> seed when <see cref="SeedDummyDataOnStartup"/>;
    /// then POS_CONTROL lookup sync to local; then peer <c>rpt_daily_sales</c> into local (home branch excluded).
    /// Safe to call on every startup: failures are logged only.
    /// </summary>
    public void StartRemoteControlLookupSync()
    {
        _ = Task.Run(() =>
        {
            try
            {
                SyncRemoteControlLookupsCore();
            }
            catch (Exception ex)
            {
                Log("StartRemoteControlLookupSync: unhandled - " + ex.GetType().Name + " - " + ex.Message);
            }
        });
    }

    /// <summary>
    /// Background pipeline: optional dev seed on remote branch DBs, then POS_CONTROL→local <c>rpt_*</c> lookups
    /// (branches, channels, roles, report catalog), then peer <c>rpt_daily_sales</c> consolidation into local.
    /// Report sync order: upsert categories, upsert reports, delete orphan reports, delete orphan categories (FK-safe).
    /// </summary>
    private void SyncRemoteControlLookupsCore()
    {
        Log("SyncRemoteControlLookupsCore: start");
        if (string.IsNullOrWhiteSpace(propertyBranchCode))
        {
            Log("SyncRemoteControlLookupsCore: skip — propertyBranchCode empty");
            return;
        }

        string serverCn;
        try
        {
            serverCn = ServerConnectionstring();
        }
        catch (Exception ex)
        {
            Log("SyncRemoteControlLookupsCore: ServerConnectionstring failed - " + ex.GetType().Name + " - " + ex.Message);
            return;
        }

        if (!PosDataAccess.CheckBranchConnection(serverCn))
        {
            Log("SyncRemoteControlLookupsCore: skip — remote compact invalid or empty");
            return;
        }

        var localCn = LocalConnectionstring(propertyBranchCode);
        if (!PosDataAccess.CheckBranchConnection(localCn))
        {
            Log("SyncRemoteControlLookupsCore: skip — local compact invalid or empty");
            return;
        }

        if (SeedDummyDataOnStartup)
            SeedRemoteBranchRptDailySalesCore(localCn);

        const int readTimeout = 60;
        DataTable dtBranches;
        DataTable dtChannels;
        DataTable dtRoles;
        DataTable dtRptCategories;
        DataTable dtRptReports;
        try
        {
            dtBranches = pda.GetDataTable(serverCn, sql.SelectRemoteBranchesForBranchGroup(propertyBranchCode.Trim()), readTimeout);
            dtChannels = pda.GetDataTable(serverCn, sql.SelectRemoteRptChannels(), readTimeout);
            dtRoles = pda.GetDataTable(serverCn, sql.SelectRemoteRptUserRoles(), readTimeout);
            dtRptCategories = pda.GetDataTable(serverCn, sql.SelectRemoteRptReportCategories(), readTimeout);
            dtRptReports = pda.GetDataTable(serverCn, sql.SelectRemoteRptReports(), readTimeout);
        }
        catch (Exception ex)
        {
            Log("SyncRemoteControlLookupsCore: remote read failed - " + ex.GetType().Name + " - " + ex.Message);
            return;
        }

        var branchKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var channelKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rptCategoryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rptReportKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var batch = new StringBuilder();
        foreach (DataRow r in dtBranches.Rows)
        {
            var code = RowString(r, "branch_code");
            if (string.IsNullOrWhiteSpace(code))
                continue;
            var trimmed = code.Trim();
            branchKeys.Add(trimmed);
            batch.Append(sql.UpsertLocalRptBranch(
                trimmed,
                RowString(r, "descr"),
                RowAsBool(r, "active", fallback: true),
                RowInt32(r, "auth_user_id", SystemBootstrapUserId)));
        }

        foreach (DataRow r in dtChannels.Rows)
        {
            var code = RowString(r, "channel_code");
            if (string.IsNullOrWhiteSpace(code))
                continue;
            var trimmed = code.Trim();
            channelKeys.Add(trimmed);
            batch.Append(sql.UpsertLocalRptChannel(
                trimmed,
                RowString(r, "descr"),
                RowAsBool(r, "active", fallback: true),
                RowInt32(r, "auth_user_id", SystemBootstrapUserId)));
        }

        foreach (DataRow r in dtRoles.Rows)
        {
            var code = RowString(r, "userrole_code");
            if (string.IsNullOrWhiteSpace(code))
                continue;
            var trimmed = code.Trim();
            roleKeys.Add(trimmed);
            batch.Append(sql.UpsertLocalRptUserRole(
                trimmed,
                RowString(r, "descr"),
                RowAsBool(r, "active", fallback: true),
                RowInt32(r, "auth_user_id", SystemBootstrapUserId)));
        }

        foreach (DataRow r in dtRptCategories.Rows)
        {
            var code = RowString(r, "category_code");
            if (string.IsNullOrWhiteSpace(code))
                continue;
            var trimmed = code.Trim();
            rptCategoryKeys.Add(trimmed);
            batch.Append(sql.UpsertLocalRptReportCategory(
                trimmed,
                RowString(r, "descr"),
                RowAsBool(r, "active", fallback: true),
                RowInt32(r, "auth_user_id", SystemBootstrapUserId),
                RowOptString(r, "browse_panel_descr"),
                RowOptString(r, "browse_icon_glyph_id"),
                RowAsBool(r, "browse_show_chevron", fallback: false),
                RowInt32(r, "browse_tile_report_count", 0),
                RowNullableInt32(r, "dashboard_browse_row"),
                RowNullableInt32(r, "dashboard_browse_sort_order"),
                RowOptString(r, "ui_icon_backdrop_hex"),
                RowOptString(r, "ui_icon_foreground_hex"),
                RowOptString(r, "ui_hover_border_hex"),
                RowOptString(r, "ui_hover_surface_hex"),
                RowOptString(r, "ui_chevron_hot_hex")));
        }

        foreach (DataRow r in dtRptReports.Rows)
        {
            var code = RowString(r, "report_code");
            if (string.IsNullOrWhiteSpace(code))
                continue;
            var catRaw = RowString(r, "category_code");
            if (string.IsNullOrWhiteSpace(catRaw))
                continue;
            var trimmed = code.Trim();
            rptReportKeys.Add(trimmed);
            var longDescr = RowString(r, "long_descr");
            var iconGlyph = RowString(r, "icon_glyph_id");
            batch.Append(sql.UpsertLocalRptReport(
                trimmed,
                catRaw.Trim(),
                RowString(r, "descr"),
                string.IsNullOrWhiteSpace(longDescr) ? null : longDescr,
                string.IsNullOrWhiteSpace(iconGlyph) ? null : iconGlyph.Trim(),
                RowAsBool(r, "active", fallback: true),
                RowInt32(r, "auth_user_id", SystemBootstrapUserId),
                RowOptString(r, "ui_icon_backdrop_hex"),
                RowOptString(r, "ui_icon_foreground_hex"),
                RowOptString(r, "ui_hover_border_hex"),
                RowOptString(r, "ui_hover_surface_hex"),
                RowOptString(r, "ui_chevron_hot_hex"),
                RowOptString(r, "ui_badge_icon_backdrop_hex"),
                RowOptString(r, "ui_badge_icon_foreground_hex"),
                RowOptString(r, "ui_badge_hover_border_hex"),
                RowOptString(r, "ui_badge_hover_surface_hex"),
                RowOptString(r, "ui_badge_chevron_hot_hex"),
                RowNullableInt32(r, "dashboard_recent_sort_order"),
                RowOptString(r, "recent_last_run_display"),
                RowNullableInt32(r, "recent_last_accessed_offset_hours"),
                RowAsBool(r, "recent_last_accessed_start_of_today_utc", fallback: false),
                RowNullableInt32(r, "dashboard_attention_sort_order"),
                RowNullableInt32(r, "dashboard_attention_count"),
                RowNullableInt32(r, "dashboard_browse_in_group_sort_order")));
        }

        var delRptReports = sql.DeleteLocalRptReportsNotInRemoteKeys(rptReportKeys);
        if (delRptReports.Length != 0)
            batch.Append(delRptReports);
        var delRptCategories = sql.DeleteLocalRptReportCategoriesNotInRemoteKeys(rptCategoryKeys);
        if (delRptCategories.Length != 0)
            batch.Append(delRptCategories);

        var delBranches = sql.DeleteLocalRptBranchesNotInRemoteKeys(branchKeys);
        if (delBranches.Length != 0)
            batch.Append(delBranches);
        var delChannels = sql.DeleteLocalRptChannelsNotInRemoteKeys(channelKeys);
        if (delChannels.Length != 0)
            batch.Append(delChannels);
        var delRoles = sql.DeleteLocalRptUserRolesNotInRemoteKeys(roleKeys);
        if (delRoles.Length != 0)
            batch.Append(delRoles);

        if (batch.Length == 0)
            Log("SyncRemoteControlLookupsCore: no rows to apply (remote empty or all keys blank)");
        else
            RunTransactionalSilent("SyncRemoteControlLookups", batch.ToString(), localCn);

        Log("SyncRemoteControlLookupsCore: done");

        SyncRemoteRptDailySalesIntoLocalCore(localCn);
    }

    /// <summary>
    /// Replaces local <c>public.rpt_daily_sales</c> rows for each peer branch with data read from that
    /// branch&apos;s database (<see cref="ServerConnectionstring(string)"/>). Skips <see cref="propertyBranchCode"/>
    /// (local is authoritative for home slice). Remote query filters <c>WHERE branch_code = peer</c>.
    /// </summary>
    private void SyncRemoteRptDailySalesIntoLocalCore(string localCn)
    {
        Log("SyncRemoteRptDailySalesIntoLocalCore: start");
        var home = (propertyBranchCode ?? string.Empty).Trim();
        if (home.Length == 0)
        {
            Log("SyncRemoteRptDailySalesIntoLocalCore: skip — propertyBranchCode empty");
            return;
        }

        DataTable dtBranches;
        try
        {
            dtBranches = pda.GetDataTable(localCn, sql.SelectLocalRptBranchCodesActive(), 60);
        }
        catch (Exception ex)
        {
            Log("SyncRemoteRptDailySalesIntoLocalCore: local branch list failed - " + ex.GetType().Name + " - " + ex.Message);
            return;
        }

        var peerCodes = new List<string>();
        foreach (DataRow r in dtBranches.Rows)
        {
            var code = RowString(r, "branch_code");
            if (string.IsNullOrWhiteSpace(code))
                continue;
            var trimmed = code.Trim();
            if (string.Equals(trimmed, home, StringComparison.OrdinalIgnoreCase))
                continue;
            peerCodes.Add(trimmed);
        }

        if (peerCodes.Count == 0)
        {
            Log("SyncRemoteRptDailySalesIntoLocalCore: no peer branches to sync");
            Log("SyncRemoteRptDailySalesIntoLocalCore: done");
            return;
        }

        const int readTimeout = 240;
        const int rowsPerInsert = 120;

        foreach (var b in peerCodes)
        {
            string branchCn;
            try
            {
                branchCn = ServerConnectionstring(b);
            }
            catch (Exception ex)
            {
                Log("SyncRemoteRptDailySalesIntoLocalCore: ServerConnectionstring(" + b + ") - " + ex.GetType().Name + " - " + ex.Message);
                continue;
            }

            if (!PosDataAccess.CheckBranchConnection(branchCn))
            {
                Log("SyncRemoteRptDailySalesIntoLocalCore: skip peer " + b + " — invalid compact");
                continue;
            }

            DataTable dtSales;
            try
            {
                dtSales = pda.GetDataTable(branchCn, sql.SelectRemoteRptDailySalesForBranch(b), readTimeout);
            }
            catch (Exception ex)
            {
                Log("SyncRemoteRptDailySalesIntoLocalCore: remote read failed " + b + " - " + ex.GetType().Name + " - " + ex.Message);
                continue;
            }

            var batch = new StringBuilder();
            batch.Append(sql.DeleteLocalRptDailySalesForBranch(b));

            var rowChunk = new List<string>(rowsPerInsert);

            void FlushChunk()
            {
                if (rowChunk.Count == 0)
                    return;
                batch.Append(sql.InsertRptDailySalesBatchPrefix());
                batch.Append(string.Join(", ", rowChunk));
                batch.Append("; ");
                rowChunk.Clear();
            }

            foreach (DataRow row in dtSales.Rows)
            {
                if (!TryRowReportDate(row, out var reportDate))
                    continue;
                var branchCode = RowString(row, "branch_code").Trim();
                if (branchCode.Length == 0)
                    branchCode = b;
                var channelCode = RowString(row, "channel_code").Trim();
                var userroleCode = RowString(row, "userrole_code").Trim();
                if (channelCode.Length == 0 || userroleCode.Length == 0)
                    continue;
                if (!TryRowDecimal(row, "sales", out var sales))
                    continue;
                var nr = RowInt32(row, "nr_transactions", 0);
                if (nr < 0)
                    nr = 0;

                rowChunk.Add(Sql.InsertRptDailySalesValuesRow(reportDate, branchCode, channelCode, userroleCode, sales, nr));
                if (rowChunk.Count >= rowsPerInsert)
                    FlushChunk();
            }

            FlushChunk();

            RunTransactionalSilent("SyncRptDailySalesIntoLocal:" + b, batch.ToString(), localCn);
        }

        Log("SyncRemoteRptDailySalesIntoLocalCore: done");
    }

    /// <summary>
    /// Dev-only: before POS_CONTROL lookup sync in the same pipeline, truncates and repopulates
    /// <c>public.rpt_daily_sales</c> on each branch database listed in local <c>rpt_branches</c>.
    /// Requires <see cref="SeedDummyDataOnStartup"/>.
    /// </summary>
    private void SeedRemoteBranchRptDailySalesCore(string localCn)
    {
        Log("SeedRemoteBranchRptDailySalesCore: start");
        DataTable dtBranches;
        try
        {
            dtBranches = pda.GetDataTable(localCn, sql.SelectLocalRptBranchCodesActive(), 60);
        }
        catch (Exception ex)
        {
            Log("SeedRemoteBranchRptDailySalesCore: local branch list failed - " + ex.GetType().Name + " - " + ex.Message);
            return;
        }

        var branchCodes = new List<string>();
        foreach (DataRow r in dtBranches.Rows)
        {
            var code = RowString(r, "branch_code");
            if (!string.IsNullOrWhiteSpace(code))
                branchCodes.Add(code.Trim());
        }

        if (branchCodes.Count == 0)
        {
            Log("SeedRemoteBranchRptDailySalesCore: no active branches in local rpt_branches");
            return;
        }

        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var startDate = yesterday.AddMonths(-3);
        if (startDate > yesterday)
        {
            Log("SeedRemoteBranchRptDailySalesCore: skip — invalid date range");
            return;
        }

        const int rowsPerInsert = 120;

        foreach (var branchCode in branchCodes)
        {
            string branchCn;
            try
            {
                branchCn = ServerConnectionstring(branchCode);
            }
            catch (Exception ex)
            {
                Log("SeedRemoteBranchRptDailySalesCore: ServerConnectionstring(" + branchCode + ") - " + ex.GetType().Name + " - " + ex.Message);
                continue;
            }

            if (!PosDataAccess.CheckBranchConnection(branchCn))
            {
                Log("SeedRemoteBranchRptDailySalesCore: skip branch " + branchCode + " — invalid compact");
                continue;
            }

            List<string> channels;
            List<string> roles;
            try
            {
                var dtCh = pda.GetDataTable(branchCn, sql.SelectBranchDbActiveChannelCodes(), 120);
                var dtR = pda.GetDataTable(branchCn, sql.SelectBranchDbActiveUserRoleCodes(), 120);
                channels = CollectCodesFromColumn(dtCh, "channel_code");
                roles = CollectCodesFromColumn(dtR, "userrole_code");
            }
            catch (Exception ex)
            {
                Log("SeedRemoteBranchRptDailySalesCore: dimension read failed " + branchCode + " - " + ex.GetType().Name + " - " + ex.Message);
                continue;
            }

            if (channels.Count == 0 || roles.Count == 0)
            {
                Log("SeedRemoteBranchRptDailySalesCore: skip branch " + branchCode + " — no active channels or roles");
                continue;
            }

            var insertsOnly = new StringBuilder();
            var rowChunk = new List<string>(rowsPerInsert);

            void FlushChunk()
            {
                if (rowChunk.Count == 0)
                    return;
                insertsOnly.Append(sql.InsertRptDailySalesBatchPrefix());
                insertsOnly.Append(string.Join(", ", rowChunk));
                insertsOnly.Append("; ");
                rowChunk.Clear();
            }

            for (var d = startDate; d <= yesterday; d = d.AddDays(1))
            {
                foreach (var ch in channels)
                {
                    foreach (var role in roles)
                    {
                        var sales = ComputeDemoDailySales(d, branchCode, ch, role);
                        var nr = ComputeDemoDailyTransactions(sales, d, branchCode, ch, role);
                        rowChunk.Add(Sql.InsertRptDailySalesValuesRow(d, branchCode, ch, role, sales, nr));
                        if (rowChunk.Count >= rowsPerInsert)
                            FlushChunk();
                    }
                }
            }

            FlushChunk();

            if (insertsOnly.Length == 0)
            {
                Log("SeedRemoteBranchRptDailySalesCore: skip branch " + branchCode + " — no INSERT rows generated");
                continue;
            }

            var truncateThenInsert = sql.TruncateRptDailySales() + insertsOnly;
            if (RunTransactionalSilent("SeedRptDailySales:" + branchCode, truncateThenInsert, branchCn))
                continue;

            Log("SeedRemoteBranchRptDailySalesCore: TRUNCATE path failed for " + branchCode + ", retrying with DELETE wipe");
            var deleteThenInsert = sql.DeleteAllRptDailySales() + insertsOnly;
            RunTransactionalSilent("SeedRptDailySalesDelete:" + branchCode, deleteThenInsert, branchCn);
        }

        Log("SeedRemoteBranchRptDailySalesCore: done");
    }

    private static List<string> CollectCodesFromColumn(DataTable dt, string columnLogicalName)
    {
        var list = new List<string>();
        foreach (DataRow r in dt.Rows)
        {
            var c = RowString(r, columnLogicalName);
            if (!string.IsNullOrWhiteSpace(c))
                list.Add(c.Trim());
        }
        return list;
    }

    private static decimal ComputeDemoDailySales(DateOnly day, string branchCode, string channelCode, string userroleCode)
    {
        var mix = (StringComparer.Ordinal.GetHashCode(branchCode) ^ StringComparer.Ordinal.GetHashCode(channelCode + "|" + userroleCode)) & 0x7FFFFFFF;
        var dayOfYear = day.DayOfYear;
        var dow = (int)day.DayOfWeek;
        var weekend = dow == 0 || dow == 6 ? 1.28 : 1.0;
        var wave = 1.0 + 0.11 * Math.Sin(dayOfYear * (Math.PI * 2 / 366.0));
        var micro = 1.0 + 0.06 * Math.Sin((dayOfYear + mix % 17) * (Math.PI * 2 / 17.0));
        var channelBoost = 1.0 + (channelCode.Length % 6) * 0.035;
        var roleBoost = 1.0 + (userroleCode.Length % 5) * 0.042;
        var baseAmt = 650m + (mix % 520);
        var factor = (decimal)(weekend * wave * micro * channelBoost * roleBoost);
        return Math.Round(baseAmt * factor, 2, MidpointRounding.AwayFromZero);
    }

    private static int ComputeDemoDailyTransactions(decimal sales, DateOnly day, string branchCode, string channelCode, string userroleCode)
    {
        var mix = StringComparer.Ordinal.GetHashCode(branchCode + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + channelCode + userroleCode) & 0x7FFFFFFF;
        var baseTx = (int)(sales / 42m);
        return Math.Max(4, baseTx + (mix % 18));
    }

    private static DataColumn? FindColumnIgnoreCase(DataTable table, string logicalName)
    {
        foreach (DataColumn c in table.Columns)
        {
            if (string.Equals(c.ColumnName, logicalName, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    private static string RowString(DataRow r, string logicalName)
    {
        var c = FindColumnIgnoreCase(r.Table, logicalName);
        if (c == null)
            return "";
        var v = r[c];
        return v == DBNull.Value || v == null
            ? ""
            : Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
    }

    /// <summary>Same boolean coercion pattern as Staff and Access (<c>AsBool</c>) for ODBC driver variance.</summary>
    private static bool RowAsBool(DataRow r, string logicalName, bool fallback)
    {
        var c = FindColumnIgnoreCase(r.Table, logicalName);
        if (c == null)
            return fallback;
        var v = r[c];
        if (v == DBNull.Value || v == null)
            return fallback;
        if (v is bool b)
            return b;
        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        if (s.Length == 0)
            return fallback;
        if (bool.TryParse(s, out var parsed))
            return parsed;
        if (s == "1" || s.Equals("t", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("y", StringComparison.OrdinalIgnoreCase))
            return true;
        if (s == "0" || s.Equals("f", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("n", StringComparison.OrdinalIgnoreCase))
            return false;
        return fallback;
    }

    private static int RowInt32(DataRow r, string logicalName, int fallback)
    {
        var c = FindColumnIgnoreCase(r.Table, logicalName);
        if (c == null)
            return fallback;
        var v = r[c];
        if (v == DBNull.Value || v == null)
            return fallback;
        if (v is int i)
            return i;
        if (v is long l)
        {
            try
            {
                return checked((int)l);
            }
            catch
            {
                return fallback;
            }
        }

        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    private static int? RowNullableInt32(DataRow r, string logicalName)
    {
        var c = FindColumnIgnoreCase(r.Table, logicalName);
        if (c == null)
            return null;
        var v = r[c];
        if (v == DBNull.Value || v == null)
            return null;
        if (v is int i)
            return i;
        if (v is long l)
        {
            try
            {
                return checked((int)l);
            }
            catch
            {
                return null;
            }
        }

        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static string? RowOptString(DataRow r, string logicalName)
    {
        var s = RowString(r, logicalName);
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static bool TryRowReportDate(DataRow r, out DateOnly date)
    {
        date = default;
        var c = FindColumnIgnoreCase(r.Table, "report_date");
        if (c == null)
            return false;
        var v = r[c];
        if (v == DBNull.Value || v == null)
            return false;
        if (v is DateOnly d)
        {
            date = d;
            return true;
        }

        if (v is DateTime dt)
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        return DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryRowDecimal(DataRow r, string logicalName, out decimal value)
    {
        value = default;
        var c = FindColumnIgnoreCase(r.Table, logicalName);
        if (c == null)
            return false;
        var v = r[c];
        if (v == DBNull.Value || v == null)
            return false;
        if (v is decimal dec)
        {
            value = dec;
            return true;
        }

        if (v is double dbl)
        {
            value = (decimal)dbl;
            return true;
        }

        if (v is float fl)
        {
            value = (decimal)fl;
            return true;
        }

        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
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

    /// <summary>
    /// Diagnostic helper: sum of is_seed = TRUE rows across the Operations and Services tables.
    /// Surfaced only in the startup log; a zero result after <c>InsertSeedOpsServices</c> is the
    /// canonical sign the ops migration (<c>2026-04-23_ops_services_init.sql</c>) has not been
    /// applied yet, mirroring the <see cref="CountSeedUsers"/> diagnostic for Staff and Access.
    /// </summary>
    private int CountSeedOpsRows(string compactConnectionString)
    {
        try
        {
            var dt = pda.GetDataTable(
                compactConnectionString,
                "SELECT " +
                "(SELECT COUNT(*) FROM public.ops_floors WHERE is_seed = TRUE) + " +
                "(SELECT COUNT(*) FROM public.ops_tables WHERE is_seed = TRUE) + " +
                "(SELECT COUNT(*) FROM public.ops_shifts WHERE is_seed = TRUE) + " +
                "(SELECT COUNT(*) FROM public.ops_shift_tables WHERE is_seed = TRUE) + " +
                "(SELECT COUNT(*) FROM public.ops_reservations WHERE is_seed = TRUE) + " +
                "(SELECT COUNT(*) FROM public.ops_floor_plan_layouts WHERE is_seed = TRUE) AS n",
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
            Log("CountSeedOpsRows failed: " + ex.GetType().Name + " - " + ex.Message);
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
