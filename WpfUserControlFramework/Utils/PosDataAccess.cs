using System.Configuration;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Windows;

namespace RestaurantPosWpf;

/// <summary>
/// PostgreSQL access via ODBC. Every CRUD method on this class <b>requires</b> the compact
/// connection string (<c>user,password,host,port,database</c>) as an explicit argument — by
/// client decree there are no overloads that fall back to App.config or to an instance-stored
/// default. Callers obtain the branch-qualified compact from
/// <see cref="AppStatus.LocalConnectionstring(string)"/>, and <b>must always pass
/// <see cref="AppStatus.propertyBranchCode"/> explicitly</b>
/// (e.g. <c>App.aps.LocalConnectionstring(App.aps.propertyBranchCode)</c>) to disambiguate from
/// other <c>LocalConnectionstring</c> overloads elsewhere in the client's codebase.
/// The ODBC driver is read from App.config (<c>PostgreSqlOdbcDriver</c>) and may be overridden
/// via <see cref="PsqlDriver"/>.
/// </summary>
public sealed class PosDataAccess
{
    /// <summary>Reads <c>PostgreSqlOdbcDriver</c> from App.config, falling back to <see cref="PSQLDrivers.PostgreSql64Unicode93"/>.</summary>
    public static PSQLDrivers DefaultDriver =>
        ParseDriver(ConfigurationManager.AppSettings["PostgreSqlOdbcDriver"]);

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

    /// <summary>Driver used when this instance builds full ODBC strings. Defaults to <see cref="DefaultDriver"/>.</summary>
    public PSQLDrivers PsqlDriver { get; set; }

    public PosDataAccess()
    {
        PsqlDriver = DefaultDriver;
    }

    public PosDataAccess(PSQLDrivers driver)
    {
        PsqlDriver = driver;
    }

    /// <summary>Reads <c>PostgreSqlOdbcDriver</c> from App.config.</summary>
    public static PSQLDrivers ReadDefaultDriverFromConfig() => DefaultDriver;

    /// <summary>True when the compact string has five parts (user, password, host, port, database).</summary>
    public static bool CheckBranchConnection(string? compactConnectionString)
    {
        if (string.IsNullOrWhiteSpace(compactConnectionString))
            return false;
        var parts = SplitCompactConnectionString(compactConnectionString);
        return parts.Length >= 5;
    }

    /// <summary>
    /// Builds a PostgreSQL ODBC connection string from a compact string
    /// <c>user,password,host,port,database</c> (commas or spaces) using this instance's <see cref="PsqlDriver"/>.
    /// </summary>
    public string BuildPostgresConnectionString(string compactConnectionString) =>
        BuildPostgresConnectionString(compactConnectionString, PsqlDriver);

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

    /// <summary>First row or null when no rows / branch check fails. Caller supplies the compact string.</summary>
    public DataRow? GetDataRow(string compactConnectionString, string sql, int commandTimeoutSeconds)
    {
        var dt = QueryToDataTable(compactConnectionString, sql, commandTimeoutSeconds, out var error);
        if (error != null)
            throw new InvalidOperationException("GetDataRow error: " + error.Message, error);
        return dt.Rows.Count == 0 ? null : dt.Rows[0];
    }

    /// <summary>All rows (may be empty). Returns empty <see cref="DataTable"/> when the branch check fails.</summary>
    public DataTable GetDataTable(string compactConnectionString, string sql, int commandTimeoutSeconds)
    {
        var dt = QueryToDataTable(compactConnectionString, sql, commandTimeoutSeconds, out var error);
        if (error != null)
            throw new InvalidOperationException("GetDataTable error: " + error.Message, error);
        return dt;
    }

    private DataTable QueryToDataTable(string compactConnectionString, string sql, int commandTimeoutSeconds,
        out Exception? error)
    {
        error = null;
        var dt = new DataTable();

        if (!CheckBranchConnection(compactConnectionString))
            return dt;

        try
        {
            var fullConnection = BuildPostgresConnectionString(compactConnectionString, PsqlDriver);
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

    /// <summary>
    /// Non-query execution that <b>rethrows</b> instead of swallowing ODBC exceptions. Intended
    /// for callers (like audit writes) that need to know when the INSERT failed so they can log
    /// the real root cause instead of silently dropping the row. Transactional: commits on
    /// success, rolls back and throws on failure. Caller supplies the compact string.
    /// </summary>
    public void SetSqlStrict(string compactConnectionString, string sql, bool transactional)
    {
        var fullConnection = BuildPostgresConnectionString(compactConnectionString, PsqlDriver);
        if (string.IsNullOrEmpty(fullConnection))
            throw new InvalidOperationException("SetSqlStrict: connection string is not configured (compact string is empty or malformed).");

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

    /// <summary>Non-query execution; optionally wraps in a transaction (commit on success, rollback on failure). Caller supplies the compact string.</summary>
    public void SetSql(string compactConnectionString, bool showError, string sql, bool transactional)
    {
        var fullConnection = BuildPostgresConnectionString(compactConnectionString, PsqlDriver);
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
