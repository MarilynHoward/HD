using System.Configuration;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Windows;

namespace RestaurantPosWpf;

/// <summary>
/// PostgreSQL access via ODBC, following compact connection strings and driver selection patterns
/// used elsewhere in the POS stack (user,password,host,port,database — comma or space separated).
/// Default connection and driver are read from App.config once (lazy) and reused; call
/// <see cref="ReloadConfiguration"/> if the file changes at runtime.
/// </summary>
public sealed class PosDataAccess
{
    private static readonly object SyncRoot = new();
    private static Lazy<PosConnectionSnapshot> _snapshot = CreateSnapshotLazy();

    private static Lazy<PosConnectionSnapshot> CreateSnapshotLazy() =>
        new(LoadSnapshot, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Immutable snapshot of <c>cnLocal</c> and <c>PostgreSqlOdbcDriver</c> from App.config.</summary>
    public sealed record PosConnectionSnapshot(string? CompactConnectionString, PSQLDrivers Driver);

    /// <summary>Current cached defaults (triggers a single config load on first use).</summary>
    public static PosConnectionSnapshot CurrentSnapshot => _snapshot.Value;

    /// <summary>Reloads settings from disk (e.g. after editing App.config). New <see cref="PosDataAccess"/> instances use the new values.</summary>
    public static void ReloadConfiguration()
    {
        lock (SyncRoot)
        {
            _snapshot = CreateSnapshotLazy();
            _ = _snapshot.Value;
        }
    }

    private static PosConnectionSnapshot LoadSnapshot()
    {
        var cn = ConfigurationManager.AppSettings["cnLocal"]?.Trim();
        var driver = ParseDriver(ConfigurationManager.AppSettings["PostgreSqlOdbcDriver"]);
        return new PosConnectionSnapshot(cn, driver);
    }

    private static PSQLDrivers ParseDriver(string? s)
    {
        if (!string.IsNullOrWhiteSpace(s) &&
            Enum.TryParse<PSQLDrivers>(s.Trim(), ignoreCase: true, out var d))
            return d;
        return PSQLDrivers.PostgreSql64Unicode93;
    }

    /// <summary>Maps to installed ODBC driver names for PostgreSQL.</summary>
    public enum PSQLDrivers
    {
        PostgreSQL,
        PostgreSQLUnicode,
        PostgreSQLAnsi,
        PostgreSql64Unicode93,
        PostgreSql64Ansi93
    }

    private readonly string? _compactConnectionString;

    public PSQLDrivers PsqlDriver { get; set; }

    /// <summary>Uses cached <c>cnLocal</c> and driver from App.config.</summary>
    public PosDataAccess()
        : this(compactConnectionString: null, driver: null)
    {
    }

    /// <summary>Override driver only; compact string comes from cached App.config.</summary>
    public PosDataAccess(PSQLDrivers driver)
        : this(compactConnectionString: null, driver)
    {
    }

    /// <summary>Explicit branch / connection (e.g. alternate database); optional driver override. Pass <c>null</c> to use cached <c>cnLocal</c>.</summary>
    public PosDataAccess(string? compactConnectionString, PSQLDrivers? driver = null)
    {
        _compactConnectionString = compactConnectionString;
        PsqlDriver = driver ?? CurrentSnapshot.Driver;
    }

    private string? EffectiveCompact =>
        _compactConnectionString ?? CurrentSnapshot.CompactConnectionString;

    /// <summary>Reads optional <c>PostgreSqlOdbcDriver</c> from cached snapshot (same as App.config at last load).</summary>
    public static PSQLDrivers ReadDefaultDriverFromConfig() => CurrentSnapshot.Driver;

    /// <summary>Compact connection string: uses cached <c>cnLocal</c> when <paramref name="appSettingsKey"/> is <c>cnLocal</c>.</summary>
    public static string? GetConfiguredCompactConnectionString(string appSettingsKey = "cnLocal")
    {
        if (appSettingsKey.Equals("cnLocal", StringComparison.OrdinalIgnoreCase))
            return CurrentSnapshot.CompactConnectionString;
        return ConfigurationManager.AppSettings[appSettingsKey]?.Trim();
    }

    /// <summary>True when the compact string has five parts (user, password, host, port, database).</summary>
    public static bool CheckBranchConnection(string? compactConnectionString)
    {
        if (string.IsNullOrWhiteSpace(compactConnectionString))
            return false;
        var parts = SplitCompactConnectionString(compactConnectionString);
        return parts.Length >= 5;
    }

    /// <inheritdoc cref="CheckBranchConnection(string?)"/>
    public bool CheckCurrentConnection() => CheckBranchConnection(EffectiveCompact);

    /// <summary>
    /// Builds a PostgreSQL ODBC connection string from a compact string
    /// <c>user,password,host,port,database</c> (commas or spaces).
    /// </summary>
    public string BuildPostgresConnectionString(string compactConnectionString) =>
        BuildPostgresConnectionString(compactConnectionString, PsqlDriver);

    /// <summary>Uses this instance's effective compact string (cached default or ctor override).</summary>
    public string BuildPostgresConnectionString() =>
        BuildPostgresConnectionString(EffectiveCompact ?? "", PsqlDriver);

    /// <summary>Static helper for pure composition (e.g. tests) without an instance.</summary>
    public static string BuildPostgresConnectionString(string compactConnectionString, PSQLDrivers driver)
    {
        var sTemp = SplitCompactConnectionString(compactConnectionString);
        if (sTemp.Length < 5)
        {
            Debug.WriteLine("[PosDataAccess] Invalid compactConnectionString (need 5 parts): " + compactConnectionString);
            return "";
        }

        var user = sTemp[0].Trim();
        var password = sTemp[1].Trim();
        var host = sTemp[2].Trim();
        var port = sTemp[3].Trim();
        var database = sTemp[4].Trim();

        return driver switch
        {
            PSQLDrivers.PostgreSQL =>
                "Provider=ODBC;Driver={PostgreSQL};" +
                "UID=" + user + ";" +
                "Server=" + host + ";" +
                "Database=" + database + ";" +
                "Password=" + password + ";" +
                "Port=" + port + ";",
            PSQLDrivers.PostgreSQLUnicode =>
                "Provider=ODBC;Driver={PostgreSQL Unicode};" +
                "UID=" + user + ";" +
                "Server=" + host + ";" +
                "Database=" + database + ";" +
                "Password=" + password + ";" +
                "Port=" + port + ";",
            PSQLDrivers.PostgreSQLAnsi =>
                "Provider=ODBC;Driver={PostgreSQL ANSI};" +
                "UID=" + user + ";" +
                "Server=" + host + ";" +
                "Database=" + database + ";" +
                "Password=" + password + ";" +
                "Port=" + port + ";",
            PSQLDrivers.PostgreSql64Ansi93 =>
                "Provider=ODBC;Driver={PostgreSQL ODBC Driver(ANSI)};" +
                "UID=" + user + ";" +
                "Server=" + host + ";" +
                "Database=" + database + ";" +
                "Password=" + password + ";" +
                "Port=" + port + ";",
            PSQLDrivers.PostgreSql64Unicode93 =>
                "Provider=ODBC;Driver={PostgreSQL ODBC Driver(UNICODE)};" +
                "UID=" + user + ";" +
                "Server=" + host + ";" +
                "Database=" + database + ";" +
                "Password=" + password + ";" +
                "Port=" + port + ";"
        };
    }

    /// <summary>Uses cached default compact connection from App.config.</summary>
    public DataRow? GetDataRow(string sql, int commandTimeoutSeconds) =>
        GetDataRow(EffectiveCompact, sql, commandTimeoutSeconds);

    /// <summary>First row or null when no rows / branch check fails.</summary>
    public DataRow? GetDataRow(string? compactConnectionString, string sql, int commandTimeoutSeconds)
    {
        var dt = QueryToDataTable(compactConnectionString, sql, commandTimeoutSeconds, out var error);
        if (error != null)
            throw new InvalidOperationException("GetDataRow error: " + error.Message, error);
        return dt.Rows.Count == 0 ? null : dt.Rows[0];
    }

    /// <summary>Uses cached default compact connection from App.config.</summary>
    public DataTable GetDataTable(string sql, int commandTimeoutSeconds) =>
        GetDataTable(EffectiveCompact, sql, commandTimeoutSeconds);

    /// <summary>All rows (may be empty). Returns empty <see cref="DataTable"/> if branch check fails.</summary>
    public DataTable GetDataTable(string? compactConnectionString, string sql, int commandTimeoutSeconds)
    {
        var dt = QueryToDataTable(compactConnectionString, sql, commandTimeoutSeconds, out var error);
        if (error != null)
            throw new InvalidOperationException("GetDataTable error: " + error.Message, error);
        return dt;
    }

    private DataTable QueryToDataTable(string? compactConnectionString, string sql, int commandTimeoutSeconds,
        out Exception? error)
    {
        error = null;
        var dt = new DataTable();

        if (!CheckBranchConnection(compactConnectionString))
            return dt;

        try
        {
            var fullConnection = BuildPostgresConnectionString(compactConnectionString!, PsqlDriver);
            if (string.IsNullOrEmpty(fullConnection))
                return dt;

            FillDataTable(dt, fullConnection, sql, commandTimeoutSeconds);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        return dt;
    }

    private static void FillDataTable(DataTable dt, string fullConnection, string sql, int commandTimeoutSeconds)
    {
        using var conn = new OdbcConnection(fullConnection);
        conn.Open();
        using var cmd = new OdbcCommand(sql, conn) { CommandTimeout = commandTimeoutSeconds };
        using var da = new OdbcDataAdapter(cmd);
        da.Fill(dt);
    }

    /// <summary>Uses cached default compact connection from App.config.</summary>
    public void SetSql(bool showError, string sql, bool transactional) =>
        SetSql(showError, sql, transactional, EffectiveCompact);

    /// <summary>
    /// Non-query execution that <b>rethrows</b> instead of swallowing ODBC exceptions. Intended
    /// for callers (like audit writes) that need to know when the INSERT failed so they can log
    /// the real root cause instead of silently dropping the row. Transactional: commits on
    /// success, rolls back and throws on failure.
    /// </summary>
    public void SetSqlStrict(string sql, bool transactional) =>
        SetSqlStrict(sql, transactional, EffectiveCompact);

    /// <inheritdoc cref="SetSqlStrict(string, bool)"/>
    public void SetSqlStrict(string sql, bool transactional, string? compactConnectionString)
    {
        var fullConnection = BuildPostgresConnectionString(compactConnectionString ?? "", PsqlDriver);
        if (string.IsNullOrEmpty(fullConnection))
            throw new InvalidOperationException("SetSqlStrict: connection string is not configured (cnLocal missing).");

        using var conn = new OdbcConnection(fullConnection);
        conn.Open();
        using var cmd = new OdbcCommand(sql, conn) { CommandTimeout = 600 };

        if (transactional)
        {
            using var trans = conn.BeginTransaction();
            cmd.Transaction = trans;
            try
            {
                cmd.ExecuteNonQuery();
                trans.Commit();
            }
            catch
            {
                try { trans.Rollback(); } catch { /* ignored */ }
                throw;
            }
        }
        else
        {
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Non-query execution; optionally wraps in a transaction (commit on success, rollback on failure).</summary>
    public void SetSql(bool showError, string sql, bool transactional, string? compactConnectionString)
    {
        var fullConnection = BuildPostgresConnectionString(compactConnectionString ?? "", PsqlDriver);
        if (string.IsNullOrEmpty(fullConnection))
        {
            Debug.WriteLine("[PosDataAccess] SetSql: empty connection string.");
            return;
        }

        try
        {
            using var conn = new OdbcConnection(fullConnection);
            conn.Open();
            using var cmd = new OdbcCommand(sql, conn) { CommandTimeout = 600 };

            if (transactional)
            {
                using var trans = conn.BeginTransaction();
                cmd.Transaction = trans;
                try
                {
                    cmd.ExecuteNonQuery();
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
            else
            {
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            if (showError)
                MessageBox.Show("SetSql: " + ex.Message, "SQL Error", MessageBoxButton.OK, MessageBoxImage.Error);
            else
                Debug.WriteLine("[PosDataAccess] SetSql error: " + ex.Message);
        }
    }

    private static string[] SplitCompactConnectionString(string compactConnectionString) =>
        compactConnectionString
            .Replace(",", " ")
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
}
