using System.Globalization;
using System.Text;

namespace RestaurantPosWpf;

/// <summary>
/// Central PostgreSQL query catalogue. Every SQL string used by the UI lives here; controls, view-models,
/// and store classes must compose queries by calling a method on <c>App.aps.sql</c> rather than inlining SQL.
/// <para>
/// <see cref="PosDataAccess"/> executes raw <see cref="System.Data.Odbc.OdbcCommand"/>s without parameter
/// substitution, so this class is also responsible for safe literal composition via <see cref="Quote"/>,
/// <see cref="Int"/>, <see cref="Bool"/>, <see cref="Ts"/>, <see cref="Nullable(string?)"/>.
/// </para>
/// <para>
/// <b>PostgreSQL 9.3 compatibility</b> — the client runs PostgreSQL 9.3 in production. All SQL emitted
/// here must work on 9.3, which means:
/// <list type="bullet">
/// <item><description>NO <c>ON CONFLICT (...) DO NOTHING/UPDATE</c> (added in 9.5). Emulate with
///   <c>INSERT INTO t (...) SELECT ... WHERE NOT EXISTS (SELECT 1 FROM t WHERE pk = ...)</c>.</description></item>
/// <item><description>NO <c>ALTER TABLE ... ADD COLUMN IF NOT EXISTS</c> (9.6+) — runtime code never emits DDL
///   anyway; migration files emulate it with <c>DO $$...$$</c> blocks against <c>information_schema.columns</c>.</description></item>
/// <item><description>NO <c>CREATE INDEX IF NOT EXISTS</c> (9.5+) — migration-only concern; use DO blocks against
///   <c>pg_indexes</c>.</description></item>
/// <item><description>NO <c>FILTER</c> clause on aggregates (9.4+), no <c>GROUPING SETS/CUBE/ROLLUP</c> (9.5+),
///   no <c>TABLESAMPLE</c> (9.5+), no <c>JSONB</c> (9.4+). Plain <c>json</c> is fine for 9.3.</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class Sql
{
    // region: Literal composition helpers -----------------------------------------------------------

    /// <summary>Safely quotes a text value (doubles embedded single quotes). Returns <c>NULL</c> for null.</summary>
    public static string Quote(string? value) =>
        value == null ? "NULL" : "'" + value.Replace("'", "''") + "'";

    /// <summary>Quoted text or <c>NULL</c> when null/empty is preferred.</summary>
    public static string Nullable(string? value) =>
        string.IsNullOrEmpty(value) ? "NULL" : Quote(value!);

    public static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Int(int? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";

    public static string Bool(bool value) => value ? "TRUE" : "FALSE";

    /// <summary>UTC timestamp in PostgreSQL literal form: <c>'yyyy-MM-dd HH:mm:ss.fff'</c>.</summary>
    public static string Ts(DateTime utc) =>
        "'" + utc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'";

    public static string Ts(DateTime? utc) => utc.HasValue ? Ts(utc.Value) : "NULL";

    /// <summary>
    /// <c>timestamp without time zone</c> literal (no UTC conversion). Use for wall-clock columns
    /// such as <c>rpt_daily_sales.modified_ts</c> on branch databases.
    /// </summary>
    public static string TsUnspecified(DateTime value) =>
        "'" + value.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'";

    /// <summary>PostgreSQL DATE literal: <c>'yyyy-MM-dd'</c>.</summary>
    public static string Date(DateOnly d) =>
        "'" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "'";

    /// <summary>PostgreSQL TIME literal: <c>'HH:mm:ss'</c>.</summary>
    public static string Time(TimeOnly t) =>
        "'" + t.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "'";

    /// <summary>
    /// PostgreSQL <c>uuid</c> literal, explicitly cast with <c>::uuid</c> so the ODBC driver routes
    /// the value into the <c>uuid</c> column type (ANSI driver versions otherwise send it as text
    /// and PostgreSQL 9.3 rejects the implicit cast for column inserts).
    /// </summary>
    public static string Uuid(Guid id) =>
        "'" + id.ToString("D", CultureInfo.InvariantCulture) + "'::uuid";

    /// <summary>
    /// Double-precision literal that always uses the invariant culture and never contains the
    /// comma decimal separator. Used for x/y positions on the floor plan canvas.
    /// </summary>
    public static string Dbl(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>PostgreSQL <c>numeric</c> literal using invariant formatting (no comma decimals).</summary>
    public static string Num(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);

    // region: Roles ----------------------------------------------------------------------------------

    /// <summary>Active, non-deleted roles ordered by id. Columns: role_id, descr, active.</summary>
    public string SelectActiveRoles() =>
        "SELECT role_id, descr, active " +
        "FROM public.roles " +
        "WHERE active = TRUE AND deleted = FALSE " +
        "ORDER BY role_id";

    /// <summary>
    /// Guarantee the six fixed roles (Developer, Admin, Manager, Supervisor, User, System) exist.
    /// Idempotent on PostgreSQL 9.3: each row is inserted via
    /// <c>INSERT INTO ... SELECT ... WHERE NOT EXISTS (SELECT 1 FROM public.roles WHERE role_id = ...)</c>
    /// because <c>ON CONFLICT</c> is a 9.5+ feature the client's database does not support.
    /// </summary>
    public string EnsureFixedRoles()
    {
        var b = new StringBuilder();
        AppendEnsureRole(b, AppStatus.RoleIdDeveloper, "Developer");
        AppendEnsureRole(b, AppStatus.RoleIdAdmin, "Admin");
        AppendEnsureRole(b, AppStatus.RoleIdManager, "Manager");
        AppendEnsureRole(b, AppStatus.RoleIdSupervisor, "Supervisor");
        AppendEnsureRole(b, AppStatus.RoleIdUser, "User");
        AppendEnsureRole(b, AppStatus.RoleIdSystem, "System");
        return b.ToString();
    }

    private static void AppendEnsureRole(StringBuilder b, int roleId, string descr)
    {
        // PostgreSQL 9.3: emulate ON CONFLICT (role_id) DO NOTHING with WHERE NOT EXISTS.
        b.Append("INSERT INTO public.roles (role_id, descr, auth_user_id, active, deleted, affected_ts, is_seed) ")
         .Append("SELECT ").Append(Int(roleId)).Append(", ").Append(Quote(descr))
         .Append(", ").Append(Int(AppStatus.SystemBootstrapUserId))
         .Append(", TRUE, FALSE, now(), FALSE ")
         .Append("WHERE NOT EXISTS (SELECT 1 FROM public.roles WHERE role_id = ").Append(Int(roleId)).Append("); ");
    }

    /// <summary>
    /// Guarantee bootstrap <c>user_id = SystemBootstrapUserId</c> ("system") exists so <c>auth_user_id</c>
    /// has a valid FK target before any real login exists. Self-references via <c>auth_user_id</c> pointing
    /// at its own <c>user_id</c>. PostgreSQL 9.3-safe: uses <c>INSERT ... WHERE NOT EXISTS</c> instead of
    /// <c>ON CONFLICT</c>.
    /// </summary>
    public string EnsureBootstrapSystemUser()
    {
        var sysId = Int(AppStatus.SystemBootstrapUserId);
        return "INSERT INTO public.users " +
               "(user_id, user_name, password, role_id, auth_user_id, first_name, surname, active, affected_ts, is_seed) " +
               "SELECT " +
               sysId + ", 'system', '', " +
               Int(AppStatus.RoleIdSystem) + ", " +
               sysId + ", 'System', 'Account', TRUE, now(), FALSE " +
               "WHERE NOT EXISTS (SELECT 1 FROM public.users WHERE user_id = " + sysId + ");";
    }

    // region: Users (read) ---------------------------------------------------------------------------

    /// <summary>All user columns consumed by the UI. Kept in one place so readers stay consistent.</summary>
    private const string UserColumns =
        "user_id, user_name, password, role_id, image_path, card_number, first_name, second_name, surname, " +
        "id_doc_path, active, affected_ts, audit_id, finger_print, password_changed_ts, " +
        "COALESCE(job_title, '') AS job_title, " +
        "COALESCE(middle_name, '') AS middle_name, " +
        "COALESCE(accent_color_hex, '') AS accent_color_hex, " +
        "COALESCE(biometric_enrolled, FALSE) AS biometric_enrolled, " +
        "COALESCE(id_doc_file_name, '') AS id_doc_file_name, " +
        "COALESCE(profile_image_rel_path, '') AS profile_image_rel_path, " +
        "COALESCE(id_doc_sync_status, 'Synced') AS id_doc_sync_status, " +
        "COALESCE(profile_image_sync_status, 'Synced') AS profile_image_sync_status, " +
        "COALESCE(is_seed, FALSE) AS is_seed";

    /// <summary>
    /// All non-system users, excluding soft-deleted rows. When <paramref name="includeInactive"/> is
    /// <c>false</c>, only <c>active = TRUE</c> users are returned. The bootstrap system account
    /// (<see cref="AppStatus.SystemBootstrapUserId"/>) is always hidden.
    /// </summary>
    public string SelectAllUsers(bool includeInactive)
    {
        var where = "WHERE COALESCE(deleted, FALSE) = FALSE";
        if (!includeInactive)
            where += " AND active = TRUE";
        return "SELECT " + UserColumns + " FROM public.users " + where + " ORDER BY user_name";
    }

    /// <summary>Single row by primary key, excluding soft-deleted users.</summary>
    public string SelectUserById(int userId) =>
        "SELECT " + UserColumns + " FROM public.users WHERE user_id = " + Int(userId) +
        " AND COALESCE(deleted, FALSE) = FALSE";

    /// <summary>Single row by unique <c>user_name</c>, excluding soft-deleted users.</summary>
    public string SelectUserByUserName(string userName) =>
        "SELECT " + UserColumns + " FROM public.users WHERE user_name = " + Quote(userName) +
        " AND COALESCE(deleted, FALSE) = FALSE";

    /// <summary>
    /// Next integer <c>user_id</c> above the current max (seed pool starts at 1001). Includes
    /// soft-deleted rows because their user_id values must not be recycled (audit stability).
    /// </summary>
    public string SelectNextUserId() =>
        "SELECT COALESCE(MAX(user_id), 1000) + 1 AS next_id FROM public.users WHERE user_id >= 1001";

    // region: Users (write) --------------------------------------------------------------------------

    /// <summary>
    /// Data shape for INSERT/UPDATE so callers don't build their own SQL. All optional fields are
    /// <c>NULL</c> when empty; disk paths stored as-is (already normalised to forward slashes).
    /// </summary>
    public sealed class UserWrite
    {
        public int UserId { get; init; }
        public string UserName { get; init; } = "";
        public int RoleId { get; init; }
        public string FirstName { get; init; } = "";
        public string MiddleName { get; init; } = "";
        public string Surname { get; init; } = "";
        public string CardNumber { get; init; } = "";
        public string JobTitle { get; init; } = "";
        public string AccentColorHex { get; init; } = "";
        public string? ImagePath { get; init; }
        public string? IdDocPath { get; init; }
        public string? IdDocFileName { get; init; }
        public string? ProfileImageRelPath { get; init; }
        public bool BiometricEnrolled { get; init; }
        public string IdDocSyncStatus { get; init; } = "Synced";
        public string ProfileImageSyncStatus { get; init; } = "Synced";
        public bool IsActive { get; init; } = true;
        public bool IsSeed { get; init; }
    }

    public string InsertUser(UserWrite u, int authUserId, string encryptedPassword)
    {
        return "INSERT INTO public.users " +
               "(user_id, user_name, password, role_id, auth_user_id, image_path, card_number, " +
               " first_name, second_name, surname, id_doc_path, active, affected_ts, " +
               " job_title, middle_name, accent_color_hex, biometric_enrolled, " +
               " id_doc_file_name, profile_image_rel_path, id_doc_sync_status, profile_image_sync_status, is_seed) " +
               "VALUES (" +
               Int(u.UserId) + ", " +
               Quote(u.UserName) + ", " +
               Quote(encryptedPassword) + ", " +
               Int(u.RoleId) + ", " +
               Int(authUserId) + ", " +
               Nullable(u.ImagePath) + ", " +
               Nullable(u.CardNumber) + ", " +
               Nullable(u.FirstName) + ", " +
               Nullable(u.MiddleName) + ", " +
               Nullable(u.Surname) + ", " +
               Nullable(u.IdDocPath) + ", " +
               Bool(u.IsActive) + ", now(), " +
               Nullable(u.JobTitle) + ", " +
               Nullable(u.MiddleName) + ", " +
               Nullable(u.AccentColorHex) + ", " +
               Bool(u.BiometricEnrolled) + ", " +
               Nullable(u.IdDocFileName) + ", " +
               Nullable(u.ProfileImageRelPath) + ", " +
               Quote(u.IdDocSyncStatus) + ", " +
               Quote(u.ProfileImageSyncStatus) + ", " +
               Bool(u.IsSeed) + ");";
    }

    /// <summary>Update every column except password (use <see cref="UpdateUserPassword"/>).</summary>
    public string UpdateUser(UserWrite u, int authUserId)
    {
        return "UPDATE public.users SET " +
               "user_name = " + Quote(u.UserName) + ", " +
               "role_id = " + Int(u.RoleId) + ", " +
               "auth_user_id = " + Int(authUserId) + ", " +
               "image_path = " + Nullable(u.ImagePath) + ", " +
               "card_number = " + Nullable(u.CardNumber) + ", " +
               "first_name = " + Nullable(u.FirstName) + ", " +
               "second_name = " + Nullable(u.MiddleName) + ", " +
               "surname = " + Nullable(u.Surname) + ", " +
               "id_doc_path = " + Nullable(u.IdDocPath) + ", " +
               "active = " + Bool(u.IsActive) + ", " +
               "affected_ts = now(), " +
               "job_title = " + Nullable(u.JobTitle) + ", " +
               "middle_name = " + Nullable(u.MiddleName) + ", " +
               "accent_color_hex = " + Nullable(u.AccentColorHex) + ", " +
               "biometric_enrolled = " + Bool(u.BiometricEnrolled) + ", " +
               "id_doc_file_name = " + Nullable(u.IdDocFileName) + ", " +
               "profile_image_rel_path = " + Nullable(u.ProfileImageRelPath) + ", " +
               "id_doc_sync_status = " + Quote(u.IdDocSyncStatus) + ", " +
               "profile_image_sync_status = " + Quote(u.ProfileImageSyncStatus) + " " +
               "WHERE user_id = " + Int(u.UserId) + ";";
    }

    public string UpdateUserPassword(int userId, string encryptedPassword, int authUserId) =>
        "UPDATE public.users SET " +
        "password = " + Quote(encryptedPassword) + ", " +
        "password_changed_ts = now(), " +
        "auth_user_id = " + Int(authUserId) + ", " +
        "affected_ts = now() " +
        "WHERE user_id = " + Int(userId) + ";";

    public string SetUserActive(int userId, bool active, int authUserId) =>
        "UPDATE public.users SET " +
        "active = " + Bool(active) + ", " +
        "auth_user_id = " + Int(authUserId) + ", " +
        "affected_ts = now() " +
        "WHERE user_id = " + Int(userId) + ";";

    public string SetBiometricEnrolled(int userId, bool enrolled, int authUserId) =>
        "UPDATE public.users SET " +
        "biometric_enrolled = " + Bool(enrolled) + ", " +
        "auth_user_id = " + Int(authUserId) + ", " +
        "affected_ts = now() " +
        "WHERE user_id = " + Int(userId) + ";";

    public string UpdateUserDocumentPaths(
        int userId,
        string? profileImageRelPath,
        string? idDocPath,
        string? idDocFileName,
        string profileImageSyncStatus,
        string idDocSyncStatus,
        int authUserId) =>
        "UPDATE public.users SET " +
        "profile_image_rel_path = " + Nullable(profileImageRelPath) + ", " +
        "image_path = " + Nullable(profileImageRelPath) + ", " +
        "id_doc_path = " + Nullable(idDocPath) + ", " +
        "id_doc_file_name = " + Nullable(idDocFileName) + ", " +
        "profile_image_sync_status = " + Quote(profileImageSyncStatus) + ", " +
        "id_doc_sync_status = " + Quote(idDocSyncStatus) + ", " +
        "auth_user_id = " + Int(authUserId) + ", " +
        "affected_ts = now() " +
        "WHERE user_id = " + Int(userId) + ";";

    /// <summary>
    /// Soft delete a user. Keeps the row for audit / Operations history (scheduled shifts, table
    /// assignments still resolve their historical <c>user_id</c>) and stamps who / when.
    /// Also flips <c>active = FALSE</c> so sign-in and roster queries reject the account immediately.
    /// </summary>
    public string SoftDeleteUser(int userId, int actorUserId) =>
        "UPDATE public.users SET " +
        "deleted = TRUE, " +
        "deleted_ts = now(), " +
        "deleted_user_id = " + Int(actorUserId) + ", " +
        "active = FALSE, " +
        "auth_user_id = " + Int(actorUserId) + ", " +
        "affected_ts = now() " +
        "WHERE user_id = " + Int(userId) + " AND COALESCE(deleted, FALSE) = FALSE;";

    /// <summary>
    /// Restore a previously soft-deleted user. Leaves <c>active</c> FALSE — re-activation is a
    /// separate, deliberate step so a revived account never signs in immediately.
    /// </summary>
    public string UndoSoftDeleteUser(int userId, int actorUserId) =>
        "UPDATE public.users SET " +
        "deleted = FALSE, " +
        "deleted_ts = NULL, " +
        "deleted_user_id = NULL, " +
        "auth_user_id = " + Int(actorUserId) + ", " +
        "affected_ts = now() " +
        "WHERE user_id = " + Int(userId) + ";";

    /// <summary>
    /// Soft delete a role. The six fixed roles (0..5) are protected — the WHERE clause prevents
    /// the client from accidentally retiring Developer / Admin / Manager / Supervisor / User / System.
    /// </summary>
    public string SoftDeleteRole(int roleId, int actorUserId) =>
        "UPDATE public.roles SET " +
        "deleted = TRUE, " +
        "deleted_ts = now(), " +
        "deleted_user_id = " + Int(actorUserId) + ", " +
        "active = FALSE, " +
        "auth_user_id = " + Int(actorUserId) + ", " +
        "affected_ts = now() " +
        "WHERE role_id = " + Int(roleId) +
        " AND role_id NOT IN (" +
        Int(AppStatus.RoleIdDeveloper) + ", " +
        Int(AppStatus.RoleIdAdmin) + ", " +
        Int(AppStatus.RoleIdManager) + ", " +
        Int(AppStatus.RoleIdSupervisor) + ", " +
        Int(AppStatus.RoleIdUser) + ", " +
        Int(AppStatus.RoleIdSystem) + ")" +
        " AND COALESCE(deleted, FALSE) = FALSE;";

    /// <summary>Restore a previously soft-deleted role.</summary>
    public string UndoSoftDeleteRole(int roleId, int actorUserId) =>
        "UPDATE public.roles SET " +
        "deleted = FALSE, " +
        "deleted_ts = NULL, " +
        "deleted_user_id = NULL, " +
        "auth_user_id = " + Int(actorUserId) + ", " +
        "affected_ts = now() " +
        "WHERE role_id = " + Int(roleId) + ";";

    // region: Seed / Dummy data ----------------------------------------------------------------------

    /// <summary>Remove every <c>is_seed = TRUE</c> row from users. Live rows are untouched.</summary>
    public string DeleteAllSeedRows() =>
        "DELETE FROM public.users WHERE is_seed = TRUE;";

    /// <summary>
    /// Insert the eight demo users (matches the in-memory seed set the UI previously used) with
    /// <c>is_seed = TRUE</c> so <see cref="DeleteAllSeedRows"/> can wipe them deterministically.
    /// Passwords are the same plain-text value ("password") encrypted via <see cref="Crypt"/>
    /// on <see cref="AppStatus.crypt"/> — by client decree <c>PasswordHasher</c> is not used.
    /// </summary>
    public string InsertSeedUsers()
    {
        var pw = App.aps.crypt.DoEncrypt("password");
        var auth = AppStatus.SystemBootstrapUserId;
        var sys = AppStatus.SystemBootstrapUserId;
        var palette = new[]
        {
            "#2563EB", "#16A34A", "#7C3AED", "#DB2777",
            "#EA580C", "#0D9488", "#CA8A04", "#4F46E5"
        };
        var rows = new (int Id, string User, string First, string Last, int RoleId, string Job, string Card)[]
        {
            (1001, "john.smith",    "John",    "Smith",   AppStatus.RoleIdUser,       "Server",    "CARD-001"),
            (1002, "sarah.johnson", "Sarah",   "Johnson", AppStatus.RoleIdUser,       "Server",    "CARD-002"),
            (1003, "mike.brown",    "Mike",    "Brown",   AppStatus.RoleIdSupervisor, "Bartender", "CARD-003"),
            (1004, "emily.davis",   "Emily",   "Davis",   AppStatus.RoleIdManager,    "Host",      "CARD-004"),
            (1005, "alex.lee",      "Alex",    "Lee",     AppStatus.RoleIdUser,       "Server",    "CARD-005"),
            (1006, "jordan.taylor", "Jordan",  "Taylor",  AppStatus.RoleIdUser,       "Server",    "CARD-006"),
            (1007, "casey.morgan",  "Casey",   "Morgan",  AppStatus.RoleIdSupervisor, "Bartender", "CARD-007"),
            (1008, "riley.chen",    "Riley",   "Chen",    AppStatus.RoleIdUser,       "Host",      "CARD-008")
        };
        _ = sys;

        var b = new StringBuilder();
        for (var i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            // PostgreSQL 9.3: emulate ON CONFLICT (user_id) DO NOTHING with WHERE NOT EXISTS.
            b.Append("INSERT INTO public.users ")
             .Append("(user_id, user_name, password, role_id, auth_user_id, card_number, ")
             .Append(" first_name, surname, active, affected_ts, ")
             .Append(" job_title, accent_color_hex, is_seed) ")
             .Append("SELECT ")
             .Append(Int(r.Id)).Append(", ")
             .Append(Quote(r.User)).Append(", ")
             .Append(Quote(pw)).Append(", ")
             .Append(Int(r.RoleId)).Append(", ")
             .Append(Int(auth)).Append(", ")
             .Append(Quote(r.Card)).Append(", ")
             .Append(Quote(r.First)).Append(", ")
             .Append(Quote(r.Last)).Append(", TRUE, now(), ")
             .Append(Quote(r.Job)).Append(", ")
             .Append(Quote(palette[i % palette.Length])).Append(", TRUE ")
             .Append("WHERE NOT EXISTS (SELECT 1 FROM public.users WHERE user_id = ")
             .Append(Int(r.Id)).Append("); ");
        }

        return b.ToString();
    }

    // region: Audit trail ---------------------------------------------------------------------------

    /// <summary>
    /// Columns returned by every audit-trail read. Kept in one place so UI mappers stay stable.
    /// Matches the client's <c>public.audit_trail</c> schema (see 2026-04-21_audit_trail_init.sql).
    /// </summary>
    private const string AuditTrailColumns =
        "audit_id, phase, event, inserted_ts, auth_user_id, role_id, " +
        "control_id_descr, control_id, invalid_password, phase_id, ip_address, machine_name";

    /// <summary>
    /// Append a single event row to <c>public.audit_trail</c> (client's canonical audit schema).
    /// Caller-supplied <paramref name="phase"/> is the <c>"&lt;Category&gt;: &lt;EventType&gt;"</c> string
    /// (e.g. <c>"Staff and Access: PasswordChanged"</c>); <paramref name="phaseId"/> comes from the
    /// lookup in <c>public.database_update_phase</c> — use <see cref="EnsurePhaseIdUpsert"/> +
    /// <see cref="SelectPhaseIdByDescr"/> before this call.
    /// <para>
    /// <c>audit_id</c> is generated as <c>COALESCE(MAX, 0) + 1</c> inside the statement because the
    /// client's DDL has no sequence. <c>role_id</c> is resolved from <c>public.users</c> for the
    /// acting user; if the lookup fails it defaults to <c>RoleIdUser</c> so the NOT NULL constraint
    /// still passes. Audit writes are best-effort — callers must catch exceptions.
    /// </para>
    /// </summary>
    public string InsertAuditTrail(
        string phase,
        string? eventPayload,
        int authUserId,
        string controlIdDescr,
        long? controlId,
        string? invalidPasswordHashed,
        int? phaseId,
        string? ipAddress,
        string? machineName)
    {
        // inserted_ts deliberately stores UTC (naive), since the column is timestamp WITHOUT TIME ZONE.
        return "INSERT INTO public.audit_trail " +
               "(audit_id, phase, event, inserted_ts, auth_user_id, role_id, " +
               " control_id_descr, control_id, invalid_password, phase_id, ip_address, machine_name) " +
               "VALUES (" +
               "(SELECT COALESCE(MAX(audit_id), 0) + 1 FROM public.audit_trail), " +
               Quote(phase) + ", " +
               Nullable(eventPayload) + ", " +
               "(now() AT TIME ZONE 'UTC'), " +
               Int(authUserId) + ", " +
               "COALESCE((SELECT role_id FROM public.users WHERE user_id = " + Int(authUserId) + "), " +
                   Int(AppStatus.RoleIdUser) + "), " +
               Quote(controlIdDescr) + ", " +
               (controlId.HasValue ? controlId.Value.ToString(CultureInfo.InvariantCulture) : "NULL") + ", " +
               Nullable(invalidPasswordHashed) + ", " +
               Int(phaseId) + ", " +
               Nullable(ipAddress) + ", " +
               Nullable(machineName) + ");";
    }

    /// <summary>
    /// Insert a new <c>public.database_update_phase</c> row if <paramref name="descr"/> is not yet
    /// known. PostgreSQL 9.3-safe: the insert is guarded by <c>WHERE NOT EXISTS</c> on the
    /// <c>UNIQUE (descr)</c> column (the 9.5+ <c>ON CONFLICT (descr) DO NOTHING</c> form is unavailable
    /// on the client's 9.3 server). Pair with <see cref="SelectPhaseIdByDescr"/> to get the id.
    /// </summary>
    public string EnsurePhaseIdUpsert(string descr) =>
        "INSERT INTO public.database_update_phase (phase_id, descr) " +
        "SELECT COALESCE((SELECT MAX(phase_id) FROM public.database_update_phase), 0) + 1, " +
        Quote(descr) + " " +
        "WHERE NOT EXISTS (SELECT 1 FROM public.database_update_phase WHERE descr = " + Quote(descr) + ");";

    /// <summary>Look up an existing <c>phase_id</c> by its description. Returns 0 rows if unknown.</summary>
    public string SelectPhaseIdByDescr(string descr) =>
        "SELECT phase_id FROM public.database_update_phase WHERE descr = " + Quote(descr);

    /// <summary>
    /// All audit rows for one staff user, newest first, capped at <paramref name="maxRows"/>.
    /// Filters on <c>control_id_descr = 'user'</c> so other modules can reuse the table without
    /// leaking into the Staff and Access audit tab.
    /// </summary>
    public string SelectAuditTrailForUser(int subjectUserId, int maxRows = 500)
    {
        var cap = maxRows <= 0 ? 500 : maxRows;
        return "SELECT " + AuditTrailColumns + " FROM public.audit_trail " +
               "WHERE control_id_descr = 'user' AND control_id = " + Int(subjectUserId) + " " +
               "ORDER BY inserted_ts DESC, audit_id DESC " +
               "LIMIT " + Int(cap);
    }

    /// <summary>
    /// Global audit feed (reserved for a future admin viewer). Optional UTC window, newest first.
    /// </summary>
    public string SelectAuditTrailAll(DateTime? fromUtc, DateTime? toUtc, int maxRows = 1000)
    {
        var cap = maxRows <= 0 ? 1000 : maxRows;
        var where = new StringBuilder();
        if (fromUtc.HasValue)
            where.Append("inserted_ts >= ").Append(Ts(fromUtc.Value));
        if (toUtc.HasValue)
        {
            if (where.Length > 0)
                where.Append(" AND ");
            where.Append("inserted_ts <= ").Append(Ts(toUtc.Value));
        }

        var whereClause = where.Length == 0 ? "" : " WHERE " + where;
        return "SELECT " + AuditTrailColumns + " FROM public.audit_trail" + whereClause +
               " ORDER BY inserted_ts DESC, audit_id DESC LIMIT " + Int(cap);
    }

    // region: Operations and Services — floors -------------------------------------------------------
    //
    // Backed by 2026-04-23_ops_services_init.sql. Reads exclude soft-deleted rows via
    // COALESCE(deleted, FALSE) = FALSE so the UI never surfaces a floor the client removed; writes
    // stamp auth_user_id + affected_ts through <see cref="AppStatus.signedOnUserId"/>.

    private const string OpsFloorColumns =
        "floor_id, name, active, COALESCE(deleted, FALSE) AS deleted, affected_ts, " +
        "COALESCE(is_seed, FALSE) AS is_seed";

    /// <summary>All floor names ordered by name (case-insensitive), excluding soft-deleted rows.</summary>
    public string SelectAllOpsFloors() =>
        "SELECT " + OpsFloorColumns + " FROM public.ops_floors " +
        "WHERE COALESCE(deleted, FALSE) = FALSE ORDER BY LOWER(name)";

    /// <summary>
    /// Next floor_id above the current max. Starts at 1 when the table is empty; includes soft-
    /// deleted rows so their floor_id values are never recycled (history stays stable).
    /// </summary>
    public string SelectNextOpsFloorId() =>
        "SELECT COALESCE(MAX(floor_id), 0) + 1 AS next_id FROM public.ops_floors";

    public string InsertOpsFloor(int floorId, string name, int authUserId, bool isSeed) =>
        "INSERT INTO public.ops_floors " +
        "(floor_id, name, auth_user_id, active, deleted, affected_ts, is_seed) " +
        "SELECT " + Int(floorId) + ", " + Quote(name) + ", " + Int(authUserId) +
        ", TRUE, FALSE, now(), " + Bool(isSeed) + " " +
        "WHERE NOT EXISTS (SELECT 1 FROM public.ops_floors WHERE floor_id = " + Int(floorId) + ");";

    public string RenameOpsFloor(int floorId, string newName, int authUserId) =>
        "UPDATE public.ops_floors SET " +
        "name = " + Quote(newName) + ", " +
        "auth_user_id = " + Int(authUserId) + ", " +
        "affected_ts = now() " +
        "WHERE floor_id = " + Int(floorId) + ";";

    /// <summary>
    /// Soft delete a floor. UI enforces "no tables on this floor" before calling, so there is no
    /// referential guard here — the store checks the in-memory cache first.
    /// </summary>
    public string SoftDeleteOpsFloor(int floorId, int actorUserId) =>
        "UPDATE public.ops_floors SET " +
        "deleted = TRUE, deleted_ts = now(), deleted_user_id = " + Int(actorUserId) + ", " +
        "active = FALSE, auth_user_id = " + Int(actorUserId) + ", affected_ts = now() " +
        "WHERE floor_id = " + Int(floorId) + " AND COALESCE(deleted, FALSE) = FALSE;";

    /// <summary>Rename cascades to tables / reservations / layouts that carry the denormalized name.</summary>
    public string RenameFloorNameInOpsTables(string oldName, string newName, int authUserId) =>
        "UPDATE public.ops_tables SET " +
        "location_name = " + Quote(newName) + ", " +
        "auth_user_id = " + Int(authUserId) + ", affected_ts = now() " +
        "WHERE location_name = " + Quote(oldName) + ";";

    public string RenameFloorNameInOpsReservations(string oldName, string newName, int authUserId) =>
        "UPDATE public.ops_reservations SET " +
        "floor_name = " + Quote(newName) + ", " +
        "auth_user_id = " + Int(authUserId) + ", affected_ts = now() " +
        "WHERE floor_name = " + Quote(oldName) + ";";

    public string RenameFloorNameInOpsLayouts(string oldName, string newName, int authUserId) =>
        "UPDATE public.ops_floor_plan_layouts SET " +
        "floor_name = " + Quote(newName) + ", " +
        "auth_user_id = " + Int(authUserId) + ", affected_ts = now() " +
        "WHERE floor_name = " + Quote(oldName) + ";";

    // region: Operations and Services — tables -------------------------------------------------------

    private const string OpsTableColumns =
        "table_id, name, location_name, seat_count, shape, is_active, assigned_waiter_id, " +
        "zone, station, turn_time_minutes, status, COALESCE(notes, '') AS notes, " +
        "accessible, vip_priority, can_merge, created_ts, modified_ts, " +
        "ops_status, ops_server_id, seated_at_ts, party_size, " +
        "active, COALESCE(deleted, FALSE) AS deleted, affected_ts, " +
        "COALESCE(is_seed, FALSE) AS is_seed";

    public string SelectAllOpsTables() =>
        "SELECT " + OpsTableColumns + " FROM public.ops_tables " +
        "WHERE COALESCE(deleted, FALSE) = FALSE ORDER BY location_name, name";

    /// <summary>Full row on insert. Stamps auth_user_id + affected_ts; lets created/modified mirror the app-supplied UTCs.</summary>
    public sealed class OpsTableWrite
    {
        public Guid TableId { get; init; }
        public string Name { get; init; } = "";
        public string LocationName { get; init; } = "Main Floor";
        public int SeatCount { get; init; } = 4;
        public string Shape { get; init; } = "Square";
        public bool IsActive { get; init; } = true;
        public int? AssignedWaiterId { get; init; }
        public int Zone { get; init; } = 1;
        public int Station { get; init; } = 1;
        public int TurnTimeMinutes { get; init; } = 60;
        public string Status { get; init; } = "Available";
        public string Notes { get; init; } = "";
        public bool Accessible { get; init; }
        public bool VipPriority { get; init; }
        public bool CanMerge { get; init; } = true;
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
        public DateTime ModifiedUtc { get; init; } = DateTime.UtcNow;
        public string OpsStatus { get; init; } = "Available";
        public int? OpsServerId { get; init; }
        public DateTime? SeatedAtUtc { get; init; }
        public int? PartySize { get; init; }
        public bool IsSeed { get; init; }
    }

    public string InsertOpsTable(OpsTableWrite t, int authUserId) =>
        "INSERT INTO public.ops_tables " +
        "(table_id, name, location_name, seat_count, shape, is_active, assigned_waiter_id, " +
        " zone, station, turn_time_minutes, status, notes, " +
        " accessible, vip_priority, can_merge, created_ts, modified_ts, " +
        " ops_status, ops_server_id, seated_at_ts, party_size, " +
        " auth_user_id, affected_ts, active, is_seed) " +
        "VALUES (" +
        Uuid(t.TableId) + ", " + Quote(t.Name) + ", " + Quote(t.LocationName) + ", " +
        Int(t.SeatCount) + ", " + Quote(t.Shape) + ", " + Bool(t.IsActive) + ", " +
        Int(t.AssignedWaiterId) + ", " +
        Int(t.Zone) + ", " + Int(t.Station) + ", " + Int(t.TurnTimeMinutes) + ", " +
        Quote(t.Status) + ", " + Nullable(t.Notes) + ", " +
        Bool(t.Accessible) + ", " + Bool(t.VipPriority) + ", " + Bool(t.CanMerge) + ", " +
        Ts(t.CreatedUtc) + ", " + Ts(t.ModifiedUtc) + ", " +
        Quote(t.OpsStatus) + ", " + Int(t.OpsServerId) + ", " + Ts(t.SeatedAtUtc) + ", " +
        Int(t.PartySize) + ", " +
        Int(authUserId) + ", now(), TRUE, " + Bool(t.IsSeed) + ");";

    public string UpdateOpsTable(OpsTableWrite t, int authUserId) =>
        "UPDATE public.ops_tables SET " +
        "name = " + Quote(t.Name) + ", " +
        "location_name = " + Quote(t.LocationName) + ", " +
        "seat_count = " + Int(t.SeatCount) + ", " +
        "shape = " + Quote(t.Shape) + ", " +
        "is_active = " + Bool(t.IsActive) + ", " +
        "assigned_waiter_id = " + Int(t.AssignedWaiterId) + ", " +
        "zone = " + Int(t.Zone) + ", " +
        "station = " + Int(t.Station) + ", " +
        "turn_time_minutes = " + Int(t.TurnTimeMinutes) + ", " +
        "status = " + Quote(t.Status) + ", " +
        "notes = " + Nullable(t.Notes) + ", " +
        "accessible = " + Bool(t.Accessible) + ", " +
        "vip_priority = " + Bool(t.VipPriority) + ", " +
        "can_merge = " + Bool(t.CanMerge) + ", " +
        "modified_ts = " + Ts(t.ModifiedUtc) + ", " +
        "ops_status = " + Quote(t.OpsStatus) + ", " +
        "ops_server_id = " + Int(t.OpsServerId) + ", " +
        "seated_at_ts = " + Ts(t.SeatedAtUtc) + ", " +
        "party_size = " + Int(t.PartySize) + ", " +
        "auth_user_id = " + Int(authUserId) + ", " +
        "affected_ts = now() " +
        "WHERE table_id = " + Uuid(t.TableId) + ";";

    /// <summary>
    /// Soft delete a table. The store guards this against tables still referenced by shifts /
    /// reservations before calling — shift_tables and reservations still carry the table_id for
    /// history, but filtered reads (<c>COALESCE(deleted, FALSE) = FALSE</c>) hide it from the UI.
    /// </summary>
    public string SoftDeleteOpsTable(Guid tableId, int actorUserId) =>
        "UPDATE public.ops_tables SET " +
        "deleted = TRUE, deleted_ts = now(), deleted_user_id = " + Int(actorUserId) + ", " +
        "active = FALSE, is_active = FALSE, " +
        "auth_user_id = " + Int(actorUserId) + ", affected_ts = now() " +
        "WHERE table_id = " + Uuid(tableId) + " AND COALESCE(deleted, FALSE) = FALSE;";

    // region: Operations and Services — shifts -------------------------------------------------------

    private const string OpsShiftColumns =
        "shift_id, employee_id, shift_date, start_time, end_time, source_kind, " +
        "active, COALESCE(deleted, FALSE) AS deleted, affected_ts, " +
        "COALESCE(is_seed, FALSE) AS is_seed";

    public string SelectAllOpsShifts() =>
        "SELECT " + OpsShiftColumns + " FROM public.ops_shifts " +
        "WHERE COALESCE(deleted, FALSE) = FALSE ORDER BY shift_date, start_time";

    public string SelectAllOpsShiftTables() =>
        "SELECT shift_id, table_id FROM public.ops_shift_tables";

    public string InsertOpsShift(
        Guid shiftId, int employeeId, DateOnly date, TimeOnly start, TimeOnly end,
        string sourceKind, int authUserId, bool isSeed) =>
        "INSERT INTO public.ops_shifts " +
        "(shift_id, employee_id, shift_date, start_time, end_time, source_kind, " +
        " auth_user_id, affected_ts, active, is_seed) " +
        "VALUES (" +
        Uuid(shiftId) + ", " + Int(employeeId) + ", " + Date(date) + ", " +
        Time(start) + ", " + Time(end) + ", " + Quote(sourceKind) + ", " +
        Int(authUserId) + ", now(), TRUE, " + Bool(isSeed) + ");";

    public string InsertOpsShiftTableLink(Guid shiftId, Guid tableId, bool isSeed) =>
        "INSERT INTO public.ops_shift_tables (shift_id, table_id, is_seed) " +
        "SELECT " + Uuid(shiftId) + ", " + Uuid(tableId) + ", " + Bool(isSeed) + " " +
        "WHERE NOT EXISTS (SELECT 1 FROM public.ops_shift_tables " +
        "WHERE shift_id = " + Uuid(shiftId) + " AND table_id = " + Uuid(tableId) + ");";

    // region: Operations and Services — reservations --------------------------------------------------

    private const string OpsReservationColumns =
        "reservation_id, table_id, floor_name, res_date, customer_name, " +
        "COALESCE(phone, '') AS phone, email, party_size, res_time, status, " +
        "COALESCE(notes, '') AS notes, COALESCE(reference, '') AS reference, " +
        "active, COALESCE(deleted, FALSE) AS deleted, affected_ts, " +
        "COALESCE(is_seed, FALSE) AS is_seed";

    public string SelectAllOpsReservations() =>
        "SELECT " + OpsReservationColumns + " FROM public.ops_reservations " +
        "WHERE COALESCE(deleted, FALSE) = FALSE ORDER BY res_date, res_time";

    public sealed class OpsReservationWrite
    {
        public Guid ReservationId { get; init; }
        public Guid TableId { get; init; }
        public string FloorName { get; init; } = "Main Floor";
        public DateOnly Date { get; init; }
        public string CustomerName { get; init; } = "";
        public string Phone { get; init; } = "";
        public string? Email { get; init; }
        public int PartySize { get; init; } = 2;
        public TimeOnly Time { get; init; }
        /// <summary>Name of the OpsReservationStatus enum value (Pending, Confirmed, …).</summary>
        public string Status { get; init; } = "Pending";
        public string Notes { get; init; } = "";
        public string Reference { get; init; } = "";
        public bool IsSeed { get; init; }
    }

    public string InsertOpsReservation(OpsReservationWrite r, int authUserId) =>
        "INSERT INTO public.ops_reservations " +
        "(reservation_id, table_id, floor_name, res_date, customer_name, phone, email, " +
        " party_size, res_time, status, notes, reference, " +
        " auth_user_id, affected_ts, active, is_seed) " +
        "VALUES (" +
        Uuid(r.ReservationId) + ", " + Uuid(r.TableId) + ", " + Quote(r.FloorName) + ", " +
        Date(r.Date) + ", " + Quote(r.CustomerName) + ", " + Nullable(r.Phone) + ", " +
        Nullable(r.Email) + ", " + Int(r.PartySize) + ", " + Time(r.Time) + ", " +
        Quote(r.Status) + ", " + Nullable(r.Notes) + ", " + Nullable(r.Reference) + ", " +
        Int(authUserId) + ", now(), TRUE, " + Bool(r.IsSeed) + ");";

    public string UpdateOpsReservation(OpsReservationWrite r, int authUserId) =>
        "UPDATE public.ops_reservations SET " +
        "table_id = " + Uuid(r.TableId) + ", " +
        "floor_name = " + Quote(r.FloorName) + ", " +
        "res_date = " + Date(r.Date) + ", " +
        "customer_name = " + Quote(r.CustomerName) + ", " +
        "phone = " + Nullable(r.Phone) + ", " +
        "email = " + Nullable(r.Email) + ", " +
        "party_size = " + Int(r.PartySize) + ", " +
        "res_time = " + Time(r.Time) + ", " +
        "status = " + Quote(r.Status) + ", " +
        "notes = " + Nullable(r.Notes) + ", " +
        "reference = " + Nullable(r.Reference) + ", " +
        "auth_user_id = " + Int(authUserId) + ", affected_ts = now() " +
        "WHERE reservation_id = " + Uuid(r.ReservationId) + ";";

    public string SoftDeleteOpsReservation(Guid reservationId, int actorUserId) =>
        "UPDATE public.ops_reservations SET " +
        "deleted = TRUE, deleted_ts = now(), deleted_user_id = " + Int(actorUserId) + ", " +
        "active = FALSE, auth_user_id = " + Int(actorUserId) + ", affected_ts = now() " +
        "WHERE reservation_id = " + Uuid(reservationId) + " AND COALESCE(deleted, FALSE) = FALSE;";

    // region: Operations and Services — floor plan layouts -------------------------------------------

    private const string OpsLayoutColumns =
        "layout_id, layout_date, floor_name, table_id, pos_x, pos_y, " +
        "affected_ts, COALESCE(is_seed, FALSE) AS is_seed";

    public string SelectAllOpsFloorPlanLayouts() =>
        "SELECT " + OpsLayoutColumns + " FROM public.ops_floor_plan_layouts";

    /// <summary>
    /// Two-statement upsert for a per-(date, floor, table) position. PostgreSQL 9.3 has no
    /// <c>ON CONFLICT</c>, so we UPDATE first (no-op if the row is missing) and then INSERT
    /// guarded by <c>WHERE NOT EXISTS</c> on the UNIQUE key. Both statements batch together.
    /// </summary>
    public string UpsertOpsFloorPlanLayout(
        Guid layoutId, DateOnly date, string floorName, Guid tableId,
        double posX, double posY, int authUserId, bool isSeed)
    {
        var update =
            "UPDATE public.ops_floor_plan_layouts SET " +
            "pos_x = " + Dbl(posX) + ", pos_y = " + Dbl(posY) + ", " +
            "auth_user_id = " + Int(authUserId) + ", affected_ts = now() " +
            "WHERE layout_date = " + Date(date) + " AND floor_name = " + Quote(floorName) +
            " AND table_id = " + Uuid(tableId) + ";";

        var insert =
            "INSERT INTO public.ops_floor_plan_layouts " +
            "(layout_id, layout_date, floor_name, table_id, pos_x, pos_y, auth_user_id, affected_ts, is_seed) " +
            "SELECT " + Uuid(layoutId) + ", " + Date(date) + ", " + Quote(floorName) + ", " +
            Uuid(tableId) + ", " + Dbl(posX) + ", " + Dbl(posY) + ", " +
            Int(authUserId) + ", now(), " + Bool(isSeed) + " " +
            "WHERE NOT EXISTS (SELECT 1 FROM public.ops_floor_plan_layouts " +
            "WHERE layout_date = " + Date(date) + " AND floor_name = " + Quote(floorName) +
            " AND table_id = " + Uuid(tableId) + ");";

        return update + " " + insert;
    }

    // region: Remote control (POS_CONTROL) → local RPT lookups --------------------------------------

    /// <summary>
    /// Remote <c>public.branches</c>: all branches in the same <c>branch_group</c> as the row whose
    /// <c>branch_code</c> matches this terminal (from <c>Branch.txt</c>). Executed against
    /// <see cref="AppStatus.ServerConnectionstring()"/>; <paramref name="localBranchCode"/> must be
    /// passed from <see cref="AppStatus.propertyBranchCode"/> and composed only via <see cref="Quote"/>.
    /// </summary>
    public string SelectRemoteBranchesForBranchGroup(string localBranchCode) =>
        "SELECT b.branch_code, b.descr, b.active, b.auth_user_id " +
        "FROM public.branches b " +
        "WHERE b.branch_group IN (" +
        "SELECT b2.branch_group FROM public.branches b2 WHERE b2.branch_code = " + Quote(localBranchCode) +
        ") ORDER BY b.branch_code";

    /// <summary>
    /// Remote <c>public.branches.rpt_sync_terminal</c> for the branch row matching this terminal&apos;s
    /// <c>Branch.txt</c> code. Executed against <see cref="AppStatus.ServerConnectionstring()"/>.
    /// </summary>
    public string SelectRptSyncTerminalForBranch(string localBranchCode) =>
        "SELECT rpt_sync_terminal FROM public.branches WHERE branch_code = " + Quote(localBranchCode.Trim()) + " LIMIT 1";

    /// <summary>Remote <c>public.rpt_channels</c> full row set for lookup sync.</summary>
    public string SelectRemoteRptChannels() =>
        "SELECT channel_code, descr, active, auth_user_id FROM public.rpt_channels ORDER BY channel_code";

    /// <summary>Remote <c>public.rpt_user_roles</c> full row set for lookup sync.</summary>
    public string SelectRemoteRptUserRoles() =>
        "SELECT userrole_code, descr, active, auth_user_id FROM public.rpt_user_roles ORDER BY userrole_code";

    /// <summary>
    /// PostgreSQL 9.3-safe upsert into local <c>public.rpt_branches</c>: <c>UPDATE</c> then
    /// <c>INSERT ... WHERE NOT EXISTS</c> (no <c>ON CONFLICT</c>).
    /// </summary>
    public string UpsertLocalRptBranch(string branchCode, string descr, bool active, int authUserId)
    {
        var d = descr ?? "";
        return
            "UPDATE public.rpt_branches SET descr = " + Quote(d) + ", active = " + Bool(active) +
            ", auth_user_id = " + Int(authUserId) + ", modified_ts = now() WHERE branch_code = " + Quote(branchCode) + "; " +
            "INSERT INTO public.rpt_branches (branch_code, descr, active, auth_user_id) SELECT " +
            Quote(branchCode) + ", " + Quote(d) + ", " + Bool(active) + ", " + Int(authUserId) +
            " WHERE NOT EXISTS (SELECT 1 FROM public.rpt_branches WHERE branch_code = " + Quote(branchCode) + "); ";
    }

    /// <summary>PostgreSQL 9.3-safe upsert into local <c>public.rpt_channels</c>.</summary>
    public string UpsertLocalRptChannel(string channelCode, string descr, bool active, int authUserId)
    {
        var d = descr ?? "";
        return
            "UPDATE public.rpt_channels SET descr = " + Quote(d) + ", active = " + Bool(active) +
            ", auth_user_id = " + Int(authUserId) + ", modified_ts = now() WHERE channel_code = " + Quote(channelCode) + "; " +
            "INSERT INTO public.rpt_channels (channel_code, descr, active, auth_user_id) SELECT " +
            Quote(channelCode) + ", " + Quote(d) + ", " + Bool(active) + ", " + Int(authUserId) +
            " WHERE NOT EXISTS (SELECT 1 FROM public.rpt_channels WHERE channel_code = " + Quote(channelCode) + "); ";
    }

    /// <summary>PostgreSQL 9.3-safe upsert into local <c>public.rpt_user_roles</c>.</summary>
    public string UpsertLocalRptUserRole(string userroleCode, string descr, bool active, int authUserId)
    {
        var d = descr ?? "";
        return
            "UPDATE public.rpt_user_roles SET descr = " + Quote(d) + ", active = " + Bool(active) +
            ", auth_user_id = " + Int(authUserId) + ", modified_ts = now() WHERE userrole_code = " + Quote(userroleCode) + "; " +
            "INSERT INTO public.rpt_user_roles (userrole_code, descr, active, auth_user_id) SELECT " +
            Quote(userroleCode) + ", " + Quote(d) + ", " + Bool(active) + ", " + Int(authUserId) +
            " WHERE NOT EXISTS (SELECT 1 FROM public.rpt_user_roles WHERE userrole_code = " + Quote(userroleCode) + "); ";
    }

    /// <summary>
    /// Deletes local <c>public.rpt_branches</c> rows whose <c>branch_code</c> is not in the remote snapshot.
    /// Returns an empty string when <paramref name="remoteKeys"/> is empty or has no usable values —
    /// otherwise <c>NOT IN ()</c> would remove every row.
    /// </summary>
    public string DeleteLocalRptBranchesNotInRemoteKeys(IReadOnlyCollection<string> remoteKeys)
    {
        if (remoteKeys == null || remoteKeys.Count == 0)
            return "";
        var b = new StringBuilder();
        b.Append("DELETE FROM public.rpt_branches WHERE branch_code NOT IN (");
        var any = false;
        foreach (var code in remoteKeys)
        {
            if (string.IsNullOrWhiteSpace(code))
                continue;
            if (any)
                b.Append(", ");
            any = true;
            b.Append(Quote(code.Trim()));
        }

        if (!any)
            return "";
        b.Append("); ");
        return b.ToString();
    }

    /// <summary>
    /// Deletes local <c>public.rpt_channels</c> rows whose <c>channel_code</c> is not in the remote snapshot.
    /// Returns empty when there are no remote keys to retain (same guard as branches).
    /// </summary>
    public string DeleteLocalRptChannelsNotInRemoteKeys(IReadOnlyCollection<string> remoteKeys)
    {
        if (remoteKeys == null || remoteKeys.Count == 0)
            return "";
        var b = new StringBuilder();
        b.Append("DELETE FROM public.rpt_channels WHERE channel_code NOT IN (");
        var any = false;
        foreach (var code in remoteKeys)
        {
            if (string.IsNullOrWhiteSpace(code))
                continue;
            if (any)
                b.Append(", ");
            any = true;
            b.Append(Quote(code.Trim()));
        }

        if (!any)
            return "";
        b.Append("); ");
        return b.ToString();
    }

    /// <summary>
    /// Deletes local <c>public.rpt_user_roles</c> rows whose <c>userrole_code</c> is not in the remote snapshot.
    /// Returns empty when there are no remote keys to retain (same guard as branches).
    /// </summary>
    public string DeleteLocalRptUserRolesNotInRemoteKeys(IReadOnlyCollection<string> remoteKeys)
    {
        if (remoteKeys == null || remoteKeys.Count == 0)
            return "";
        var b = new StringBuilder();
        b.Append("DELETE FROM public.rpt_user_roles WHERE userrole_code NOT IN (");
        var any = false;
        foreach (var code in remoteKeys)
        {
            if (string.IsNullOrWhiteSpace(code))
                continue;
            if (any)
                b.Append(", ");
            any = true;
            b.Append(Quote(code.Trim()));
        }

        if (!any)
            return "";
        b.Append("); ");
        return b.ToString();
    }

    /// <summary>Local <c>public.rpt_branches</c> rows for dashboard filter combo (<c>active = TRUE</c>).</summary>
    public string SelectLocalRptBranchesForFilters() =>
        "SELECT branch_code, descr FROM public.rpt_branches WHERE active = TRUE ORDER BY branch_code";

    /// <summary>Local <c>public.rpt_channels</c> rows for dashboard filter combo (<c>active = TRUE</c>).</summary>
    public string SelectLocalRptChannelsForFilters() =>
        "SELECT channel_code, descr FROM public.rpt_channels WHERE active = TRUE ORDER BY descr";

    /// <summary>Local <c>public.rpt_user_roles</c> rows for dashboard filter combo (<c>active = TRUE</c>).</summary>
    public string SelectLocalRptUserRolesForFilters() =>
        "SELECT userrole_code, descr FROM public.rpt_user_roles WHERE active = TRUE ORDER BY descr";

    /// <summary>Remote <c>public.rpt_report_categories</c> full row set for lookup sync (includes dashboard UI columns).</summary>
    public string SelectRemoteRptReportCategories() =>
        "SELECT category_code, descr, active, auth_user_id, " +
        "browse_panel_descr, browse_icon_glyph_id, browse_show_chevron, browse_tile_report_count, " +
        "dashboard_browse_row, dashboard_browse_sort_order, " +
        "ui_icon_backdrop_hex, ui_icon_foreground_hex, ui_hover_border_hex, ui_hover_surface_hex, ui_chevron_hot_hex " +
        "FROM public.rpt_report_categories ORDER BY category_code";

    /// <summary>Remote <c>public.rpt_reports</c> full row set for lookup sync (includes dashboard UI columns).</summary>
    public string SelectRemoteRptReports() =>
        "SELECT report_code, category_code, descr, long_descr, icon_glyph_id, active, auth_user_id, " +
        "ui_icon_backdrop_hex, ui_icon_foreground_hex, ui_hover_border_hex, ui_hover_surface_hex, ui_chevron_hot_hex, " +
        "ui_badge_icon_backdrop_hex, ui_badge_icon_foreground_hex, ui_badge_hover_border_hex, ui_badge_hover_surface_hex, ui_badge_chevron_hot_hex, " +
        "dashboard_recent_sort_order, recent_last_run_display, recent_last_accessed_offset_hours, recent_last_accessed_start_of_today_utc, " +
        "dashboard_attention_sort_order, dashboard_attention_count, dashboard_browse_in_group_sort_order " +
        "FROM public.rpt_reports ORDER BY report_code";

    /// <summary>
    /// Local branch: Recently Used — up to <paramref name="maxDistinctReports"/> distinct <c>report_code</c>
    /// for <paramref name="userId"/>, ordered by latest <c>accessed_ts</c> in <c>rpt_report_access_log</c>.
    /// </summary>
    public string SelectLocalRptRecentlyUsedReportsForUser(int userId, int maxDistinctReports) =>
        "SELECT r.report_code, r.category_code, r.descr, r.icon_glyph_id, " +
        "r.ui_icon_backdrop_hex, r.ui_icon_foreground_hex, r.ui_hover_border_hex, r.ui_hover_surface_hex, r.ui_chevron_hot_hex, " +
        "x.last_accessed_ts " +
        "FROM (" +
        "SELECT report_code, MAX(accessed_ts) AS last_accessed_ts " +
        "FROM public.rpt_report_access_log WHERE user_id = " + Int(userId) + " GROUP BY report_code" +
        ") x " +
        "INNER JOIN public.rpt_reports r ON r.report_code = x.report_code AND COALESCE(r.active, TRUE) = TRUE " +
        "ORDER BY x.last_accessed_ts DESC LIMIT " + Int(maxDistinctReports);

    /// <summary>
    /// When <paramref name="userId"/> has no rows in <c>public.rpt_report_access_log</c>, inserts four default
    /// <c>report_code</c> rows with <c>accessed_ts</c> set from the current UTC time (staggered by seconds for stable
    /// Recently Used ordering). No-op if the user already has any access-log row. PG 9.3-safe (<c>INSERT … SELECT … WHERE NOT EXISTS</c>).
    /// </summary>
    public string InsertLocalRptDefaultRecentReportsWhenUserHasNoHistory(int userId, int authUserId)
    {
        var u = Int(userId);
        var a = Int(authUserId);
        var t0 = DateTime.UtcNow;
        var t1 = t0.AddSeconds(-1);
        var t2 = t0.AddSeconds(-2);
        var t3 = t0.AddSeconds(-3);
        return
            "INSERT INTO public.rpt_report_access_log (user_id, report_code, accessed_ts, auth_user_id) " +
            "SELECT v.user_id, v.report_code, v.accessed_ts, v.auth_user_id " +
            "FROM (VALUES " +
            "(" + u + ", " + Quote("rpt.daily_sales") + ", " + Ts(t0) + "::timestamp, " + a + "), " +
            "(" + u + ", " + Quote("rpt.vat_summary") + ", " + Ts(t1) + "::timestamp, " + a + "), " +
            "(" + u + ", " + Quote("rpt.wastage") + ", " + Ts(t2) + "::timestamp, " + a + "), " +
            "(" + u + ", " + Quote("rpt.voids") + ", " + Ts(t3) + "::timestamp, " + a + ")" +
            ") AS v(user_id, report_code, accessed_ts, auth_user_id) " +
            "WHERE NOT EXISTS (SELECT 1 FROM public.rpt_report_access_log WHERE user_id = " + u + "); ";
    }

    /// <summary>
    /// Dev-only (<see cref="AppStatus.SeedDummyDataOnStartup"/>): clear report access history before reinserting demo rows.
    /// </summary>
    public string TruncateRptReportAccessLog() =>
        "TRUNCATE TABLE public.rpt_report_access_log; ";

    /// <summary>
    /// Dev-only: seed <c>rpt_report_access_log</c> after <see cref="TruncateRptReportAccessLog"/> so Recently Used shows four demo reports.
    /// </summary>
    public string InsertSeedRptReportAccessLog(int userId, int authUserId)
    {
        var now = DateTime.UtcNow;
        var u = Int(userId);
        var a = Int(authUserId);
        var tSales = Ts(now.AddHours(-2));
        var tVat = Ts(now.AddHours(-24));
        var tWaste = Ts(now.AddDays(-3));
        var tVoids = Ts(now.Date);
        return
            "INSERT INTO public.rpt_report_access_log (user_id, report_code, accessed_ts, auth_user_id) VALUES " +
            "(" + u + ", " + Quote("rpt.daily_sales") + ", " + tSales + ", " + a + "), " +
            "(" + u + ", " + Quote("rpt.vat_summary") + ", " + tVat + ", " + a + "), " +
            "(" + u + ", " + Quote("rpt.wastage") + ", " + tWaste + ", " + a + "), " +
            "(" + u + ", " + Quote("rpt.voids") + ", " + tVoids + ", " + a + "); ";
    }

    /// <summary>Local branch: catalog chrome for dashboard strip cards (title, icon, UI hex) for the given report codes.</summary>
    public string SelectLocalRptReportDashboardCardsByCodes(IEnumerable<string> reportCodes)
    {
        var codes = (reportCodes ?? Array.Empty<string>())
                .Select(c => (c ?? string.Empty).Trim())
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        var b = new StringBuilder();
        b.Append(
                "SELECT report_code, descr, icon_glyph_id, " +
                "ui_icon_backdrop_hex, ui_icon_foreground_hex, ui_hover_border_hex, ui_hover_surface_hex, ui_chevron_hot_hex, " +
                "ui_badge_icon_backdrop_hex, ui_badge_icon_foreground_hex, ui_badge_hover_border_hex, ui_badge_hover_surface_hex, ui_badge_chevron_hot_hex " +
                "FROM public.rpt_reports WHERE active = TRUE");
        if (codes.Count == 0)
        {
            b.Append(" AND 1 = 0");
            return b.ToString();
        }

        b.Append(" AND report_code IN (");
        for (var i = 0; i < codes.Count; i++)
        {
            if (i > 0)
                b.Append(", ");
            b.Append(Quote(codes[i]));
        }

        b.Append(')');
        return b.ToString();
    }

    /// <summary>Local branch: Browse grouping tiles for one horizontal row (<paramref name="browseRow"/> is 1 or 2).</summary>
    public string SelectLocalRptCategoriesForDashboardBrowseRow(int browseRow) =>
        "SELECT category_code, descr, browse_panel_descr, browse_icon_glyph_id, browse_show_chevron, browse_tile_report_count, " +
        "ui_icon_backdrop_hex, ui_icon_foreground_hex, ui_hover_border_hex, ui_hover_surface_hex, ui_chevron_hot_hex " +
        "FROM public.rpt_report_categories WHERE active = TRUE AND dashboard_browse_row = " + Int(browseRow) +
        " ORDER BY dashboard_browse_sort_order";

    /// <summary>Reports listed inside a browse category tile.</summary>
    public string SelectLocalRptReportsForBrowseSubgroup(string categoryCode) =>
        "SELECT report_code, descr, icon_glyph_id FROM public.rpt_reports WHERE active = TRUE AND category_code = " +
        Quote(categoryCode) + " AND dashboard_browse_in_group_sort_order IS NOT NULL " +
        "ORDER BY dashboard_browse_in_group_sort_order";

    /// <summary>PostgreSQL 9.3-safe upsert into local <c>public.rpt_report_categories</c> (dashboard UI included).</summary>
    public string UpsertLocalRptReportCategory(
        string categoryCode,
        string descr,
        bool active,
        int authUserId,
        string? browsePanelDescr,
        string? browseIconGlyphId,
        bool browseShowChevron,
        int browseTileReportCount,
        int? dashboardBrowseRow,
        int? dashboardBrowseSortOrder,
        string? uiIconBackdropHex,
        string? uiIconForegroundHex,
        string? uiHoverBorderHex,
        string? uiHoverSurfaceHex,
        string? uiChevronHotHex)
    {
        var d = descr ?? "";
        return
            "UPDATE public.rpt_report_categories SET descr = " + Quote(d) + ", active = " + Bool(active) +
            ", auth_user_id = " + Int(authUserId) + ", modified_ts = now(), " +
            "browse_panel_descr = " + Nullable(browsePanelDescr) + ", " +
            "browse_icon_glyph_id = " + Nullable(browseIconGlyphId) + ", " +
            "browse_show_chevron = " + Bool(browseShowChevron) + ", " +
            "browse_tile_report_count = " + Int(browseTileReportCount) + ", " +
            "dashboard_browse_row = " + Int(dashboardBrowseRow) + ", " +
            "dashboard_browse_sort_order = " + Int(dashboardBrowseSortOrder) + ", " +
            "ui_icon_backdrop_hex = " + Nullable(uiIconBackdropHex) + ", " +
            "ui_icon_foreground_hex = " + Nullable(uiIconForegroundHex) + ", " +
            "ui_hover_border_hex = " + Nullable(uiHoverBorderHex) + ", " +
            "ui_hover_surface_hex = " + Nullable(uiHoverSurfaceHex) + ", " +
            "ui_chevron_hot_hex = " + Nullable(uiChevronHotHex) +
            " WHERE category_code = " + Quote(categoryCode) + "; " +
            "INSERT INTO public.rpt_report_categories (" +
            "category_code, descr, active, auth_user_id, browse_panel_descr, browse_icon_glyph_id, browse_show_chevron, browse_tile_report_count, " +
            "dashboard_browse_row, dashboard_browse_sort_order, ui_icon_backdrop_hex, ui_icon_foreground_hex, ui_hover_border_hex, ui_hover_surface_hex, ui_chevron_hot_hex) SELECT " +
            Quote(categoryCode) + ", " + Quote(d) + ", " + Bool(active) + ", " + Int(authUserId) + ", " +
            Nullable(browsePanelDescr) + ", " + Nullable(browseIconGlyphId) + ", " + Bool(browseShowChevron) + ", " + Int(browseTileReportCount) + ", " +
            Int(dashboardBrowseRow) + ", " + Int(dashboardBrowseSortOrder) + ", " +
            Nullable(uiIconBackdropHex) + ", " + Nullable(uiIconForegroundHex) + ", " + Nullable(uiHoverBorderHex) + ", " +
            Nullable(uiHoverSurfaceHex) + ", " + Nullable(uiChevronHotHex) +
            " WHERE NOT EXISTS (SELECT 1 FROM public.rpt_report_categories WHERE category_code = " + Quote(categoryCode) + "); ";
    }

    /// <summary>PostgreSQL 9.3-safe upsert into local <c>public.rpt_reports</c> (dashboard UI included).</summary>
    public string UpsertLocalRptReport(
        string reportCode,
        string categoryCode,
        string descr,
        string? longDescr,
        string? iconGlyphId,
        bool active,
        int authUserId,
        string? uiIconBackdropHex,
        string? uiIconForegroundHex,
        string? uiHoverBorderHex,
        string? uiHoverSurfaceHex,
        string? uiChevronHotHex,
        string? uiBadgeIconBackdropHex,
        string? uiBadgeIconForegroundHex,
        string? uiBadgeHoverBorderHex,
        string? uiBadgeHoverSurfaceHex,
        string? uiBadgeChevronHotHex,
        int? dashboardRecentSortOrder,
        string? recentLastRunDisplay,
        int? recentLastAccessedOffsetHours,
        bool recentLastAccessedStartOfTodayUtc,
        int? dashboardAttentionSortOrder,
        int? dashboardAttentionCount,
        int? dashboardBrowseInGroupSortOrder)
    {
        var d = descr ?? "";
        var cat = categoryCode ?? "";
        return
            "UPDATE public.rpt_reports SET category_code = " + Quote(cat) + ", descr = " + Quote(d) +
            ", long_descr = " + Nullable(longDescr) +
            ", icon_glyph_id = " + Nullable(iconGlyphId) +
            ", active = " + Bool(active) +
            ", auth_user_id = " + Int(authUserId) + ", modified_ts = now(), " +
            "ui_icon_backdrop_hex = " + Nullable(uiIconBackdropHex) + ", " +
            "ui_icon_foreground_hex = " + Nullable(uiIconForegroundHex) + ", " +
            "ui_hover_border_hex = " + Nullable(uiHoverBorderHex) + ", " +
            "ui_hover_surface_hex = " + Nullable(uiHoverSurfaceHex) + ", " +
            "ui_chevron_hot_hex = " + Nullable(uiChevronHotHex) + ", " +
            "ui_badge_icon_backdrop_hex = " + Nullable(uiBadgeIconBackdropHex) + ", " +
            "ui_badge_icon_foreground_hex = " + Nullable(uiBadgeIconForegroundHex) + ", " +
            "ui_badge_hover_border_hex = " + Nullable(uiBadgeHoverBorderHex) + ", " +
            "ui_badge_hover_surface_hex = " + Nullable(uiBadgeHoverSurfaceHex) + ", " +
            "ui_badge_chevron_hot_hex = " + Nullable(uiBadgeChevronHotHex) + ", " +
            "dashboard_recent_sort_order = " + Int(dashboardRecentSortOrder) + ", " +
            "recent_last_run_display = " + Nullable(recentLastRunDisplay) + ", " +
            "recent_last_accessed_offset_hours = " + Int(recentLastAccessedOffsetHours) + ", " +
            "recent_last_accessed_start_of_today_utc = " + Bool(recentLastAccessedStartOfTodayUtc) + ", " +
            "dashboard_attention_sort_order = " + Int(dashboardAttentionSortOrder) + ", " +
            "dashboard_attention_count = " + Int(dashboardAttentionCount) + ", " +
            "dashboard_browse_in_group_sort_order = " + Int(dashboardBrowseInGroupSortOrder) +
            " WHERE report_code = " + Quote(reportCode) + "; " +
            "INSERT INTO public.rpt_reports (" +
            "report_code, category_code, descr, long_descr, icon_glyph_id, active, auth_user_id, " +
            "ui_icon_backdrop_hex, ui_icon_foreground_hex, ui_hover_border_hex, ui_hover_surface_hex, ui_chevron_hot_hex, " +
            "ui_badge_icon_backdrop_hex, ui_badge_icon_foreground_hex, ui_badge_hover_border_hex, ui_badge_hover_surface_hex, ui_badge_chevron_hot_hex, " +
            "dashboard_recent_sort_order, recent_last_run_display, recent_last_accessed_offset_hours, recent_last_accessed_start_of_today_utc, " +
            "dashboard_attention_sort_order, dashboard_attention_count, dashboard_browse_in_group_sort_order) SELECT " +
            Quote(reportCode) + ", " + Quote(cat) + ", " + Quote(d) + ", " + Nullable(longDescr) + ", " +
            Nullable(iconGlyphId) + ", " + Bool(active) + ", " + Int(authUserId) + ", " +
            Nullable(uiIconBackdropHex) + ", " + Nullable(uiIconForegroundHex) + ", " + Nullable(uiHoverBorderHex) + ", " +
            Nullable(uiHoverSurfaceHex) + ", " + Nullable(uiChevronHotHex) + ", " +
            Nullable(uiBadgeIconBackdropHex) + ", " + Nullable(uiBadgeIconForegroundHex) + ", " + Nullable(uiBadgeHoverBorderHex) + ", " +
            Nullable(uiBadgeHoverSurfaceHex) + ", " + Nullable(uiBadgeChevronHotHex) + ", " +
            Int(dashboardRecentSortOrder) + ", " + Nullable(recentLastRunDisplay) + ", " + Int(recentLastAccessedOffsetHours) + ", " +
            Bool(recentLastAccessedStartOfTodayUtc) + ", " +
            Int(dashboardAttentionSortOrder) + ", " + Int(dashboardAttentionCount) + ", " + Int(dashboardBrowseInGroupSortOrder) +
            " WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = " + Quote(reportCode) + "); ";
    }

    /// <summary>
    /// Deletes local <c>public.rpt_reports</c> rows whose <c>report_code</c> is not in the remote snapshot.
    /// Returns empty when there are no remote keys (same guard as <see cref="DeleteLocalRptChannelsNotInRemoteKeys"/>).
    /// </summary>
    public string DeleteLocalRptReportsNotInRemoteKeys(IReadOnlyCollection<string> remoteKeys)
    {
        if (remoteKeys == null || remoteKeys.Count == 0)
            return "";
        var b = new StringBuilder();
        b.Append("DELETE FROM public.rpt_reports WHERE report_code NOT IN (");
        var any = false;
        foreach (var code in remoteKeys)
        {
            if (string.IsNullOrWhiteSpace(code))
                continue;
            if (any)
                b.Append(", ");
            any = true;
            b.Append(Quote(code.Trim()));
        }

        if (!any)
            return "";
        b.Append("); ");
        return b.ToString();
    }

    /// <summary>
    /// Deletes local <c>public.rpt_report_categories</c> rows whose <c>category_code</c> is not in the remote snapshot.
    /// Call only after <see cref="DeleteLocalRptReportsNotInRemoteKeys"/> so FK parents are not referenced.
    /// </summary>
    public string DeleteLocalRptReportCategoriesNotInRemoteKeys(IReadOnlyCollection<string> remoteKeys)
    {
        if (remoteKeys == null || remoteKeys.Count == 0)
            return "";
        var b = new StringBuilder();
        b.Append("DELETE FROM public.rpt_report_categories WHERE category_code NOT IN (");
        var any = false;
        foreach (var code in remoteKeys)
        {
            if (string.IsNullOrWhiteSpace(code))
                continue;
            if (any)
                b.Append(", ");
            any = true;
            b.Append(Quote(code.Trim()));
        }

        if (!any)
            return "";
        b.Append("); ");
        return b.ToString();
    }

    /// <summary>
    /// Append-only access log on the branch DB (<c>accessed_ts</c> defaults to <c>now()</c>).
    /// </summary>
    public string InsertReportAccessLog(int userId, string reportCode, int authUserId) =>
        "INSERT INTO public.rpt_report_access_log (user_id, report_code, auth_user_id) VALUES (" +
        Int(userId) + ", " + Quote(reportCode.Trim()) + ", " + Int(authUserId) + "); ";

    /// <summary>
    /// Local DB: active branch codes after lookup sync; drives which remote branch databases receive
    /// dev-only <c>rpt_daily_sales</c> / <c>rpt_vat</c> seed when <see cref="AppStatus.SeedDummyDataOnStartup"/> is true.
    /// </summary>
    public string SelectLocalRptBranchCodesActive() =>
        "SELECT branch_code FROM public.rpt_branches WHERE active = TRUE ORDER BY branch_code";

    /// <summary>
    /// Per-branch DB: active channel codes for cartesian seed (must match keys used in facts).
    /// </summary>
    public string SelectBranchDbActiveChannelCodes() =>
        "SELECT channel_code FROM public.rpt_channels WHERE active = TRUE ORDER BY channel_code";

    /// <summary>
    /// Per-branch DB: active user-role codes for cartesian seed.
    /// </summary>
    public string SelectBranchDbActiveUserRoleCodes() =>
        "SELECT userrole_code FROM public.rpt_user_roles WHERE active = TRUE ORDER BY userrole_code";

    /// <summary>
    /// Dev-only: remove all rows from <c>public.rpt_daily_sales</c> before reload. If <c>TRUNCATE</c>
    /// fails (e.g. FK), replace with <see cref="DeleteAllRptDailySales"/> in application code.
    /// </summary>
    public string TruncateRptDailySales() =>
        "TRUNCATE TABLE public.rpt_daily_sales; ";

    /// <summary>Fallback wipe when <c>TRUNCATE</c> is not permitted.</summary>
    public string DeleteAllRptDailySales() =>
        "DELETE FROM public.rpt_daily_sales; ";

    /// <summary>
    /// Dev-only: remove all rows from <c>public.rpt_vat</c> before reload. If <c>TRUNCATE</c> fails,
    /// replace with <see cref="DeleteAllRptVat"/> in application code.
    /// </summary>
    public string TruncateRptVat() =>
        "TRUNCATE TABLE public.rpt_vat; ";

    /// <summary>Fallback wipe when <c>TRUNCATE</c> is not permitted.</summary>
    public string DeleteAllRptVat() =>
        "DELETE FROM public.rpt_vat; ";

    /// <summary>
    /// Remote branch DB: VAT report facts for one branch (ignores rows whose <c>branch_code</c> does not match).
    /// Executed against <see cref="AppStatus.ServerConnectionstring(string)"/>.
    /// </summary>
    public string SelectRemoteRptVatForBranch(string branchCode) =>
        "SELECT report_date, branch_code, channel_code, userrole_code, vat_rate_id, net_amount, modified_ts " +
        "FROM public.rpt_vat WHERE branch_code = " + Quote(branchCode) +
        " ORDER BY modified_ts, report_date, channel_code, userrole_code, vat_rate_id";

    /// <summary>
    /// Incremental pull from a peer branch DB: <c>rpt_vat</c> rows changed after the stored <c>modified_ts</c> cursor.
    /// </summary>
    public string SelectRemoteRptVatModifiedAfter(string branchCode, DateTime modifiedAfterUnspecified) =>
        "SELECT report_date, branch_code, channel_code, userrole_code, vat_rate_id, net_amount, modified_ts " +
        "FROM public.rpt_vat WHERE branch_code = " + Quote(branchCode) +
        " AND modified_ts > " + TsUnspecified(modifiedAfterUnspecified) +
        " ORDER BY modified_ts, report_date, channel_code, userrole_code, vat_rate_id";

    /// <summary>
    /// Local DB: remove replicated <c>rpt_vat</c> slice for a peer branch before full reload (home excluded by caller).
    /// </summary>
    public string DeleteLocalRptVatForBranch(string branchCode) =>
        "DELETE FROM public.rpt_vat WHERE branch_code = " + Quote(branchCode) + "; ";

    /// <summary>PostgreSQL 9.3-safe upsert for one <c>rpt_vat</c> fact row (peer replication).</summary>
    public string UpsertLocalRptVatRow(
        DateOnly reportDate,
        string branchCode,
        string channelCode,
        string userroleCode,
        int vatRateId,
        decimal netAmount,
        DateTime remoteModifiedTsUnspecified)
    {
        var rd = Date(reportDate);
        var bc = Quote(branchCode);
        var cc = Quote(channelCode);
        var ur = Quote(userroleCode);
        var vid = Int(vatRateId);
        var mt = TsUnspecified(remoteModifiedTsUnspecified);
        return
            "UPDATE public.rpt_vat SET net_amount = " + Num(netAmount) +
            ", modified_ts = " + mt + " WHERE report_date = " + rd + " AND branch_code = " + bc +
            " AND channel_code = " + cc + " AND userrole_code = " + ur + " AND vat_rate_id = " + vid + "; " +
            "INSERT INTO public.rpt_vat (report_date, branch_code, channel_code, userrole_code, vat_rate_id, net_amount, modified_ts) SELECT " +
            rd + ", " + bc + ", " + cc + ", " + ur + ", " + vid + ", " + Num(netAmount) + ", " + mt +
            " WHERE NOT EXISTS (SELECT 1 FROM public.rpt_vat WHERE report_date = " + rd + " AND branch_code = " + bc +
            " AND channel_code = " + cc + " AND userrole_code = " + ur + " AND vat_rate_id = " + vid + "); ";
    }

    /// <summary>Prefix for batched <c>INSERT</c> into <c>rpt_vat</c> including explicit <c>modified_ts</c>.</summary>
    public string InsertRptVatBatchPrefixWithModifiedTs() =>
        "INSERT INTO public.rpt_vat (report_date, branch_code, channel_code, userrole_code, vat_rate_id, net_amount, modified_ts) VALUES ";

    /// <summary>One row for <see cref="InsertRptVatBatchPrefixWithModifiedTs"/> batches.</summary>
    public static string InsertRptVatValuesRowWithModifiedTs(
        DateOnly reportDate,
        string branchCode,
        string channelCode,
        string userroleCode,
        int vatRateId,
        decimal netAmount,
        DateTime remoteModifiedTsUnspecified) =>
        "(" + Date(reportDate) + ", " + Quote(branchCode) + ", " + Quote(channelCode) + ", " + Quote(userroleCode) + ", " +
        Int(vatRateId) + ", " + Num(netAmount) + ", " + TsUnspecified(remoteModifiedTsUnspecified) + ")";

    /// <summary>Prefix for dev seed <c>INSERT</c> into <c>rpt_vat</c> (defaults <c>modified_ts</c>).</summary>
    public string InsertRptVatBatchPrefix() =>
        "INSERT INTO public.rpt_vat (report_date, branch_code, channel_code, userrole_code, vat_rate_id, net_amount) VALUES ";

    /// <summary>One row for <see cref="InsertRptVatBatchPrefix"/>.</summary>
    public static string InsertRptVatValuesRow(
        DateOnly reportDate,
        string branchCode,
        string channelCode,
        string userroleCode,
        int vatRateId,
        decimal netAmount) =>
        "(" + Date(reportDate) + ", " + Quote(branchCode) + ", " + Quote(channelCode) + ", " + Quote(userroleCode) + ", " +
        Int(vatRateId) + ", " + Num(netAmount) + ")";

    /// <summary>
    /// Per-branch DB: active <c>vat_rate_id</c> values for cartesian VAT seed (must match keys used in facts).
    /// </summary>
    public string SelectBranchDbActiveVatRateIds() =>
        "SELECT tax_id AS vat_rate_id FROM public.taxes WHERE active = TRUE AND COALESCE(deleted, FALSE) = FALSE ORDER BY tax_id";

    /// <summary>
    /// Remote branch DB: daily sales for one branch (ignores rows whose <c>branch_code</c> does not match).
    /// Executed against <see cref="AppStatus.ServerConnectionstring(string)"/>.
    /// </summary>
    public string SelectRemoteRptDailySalesForBranch(string branchCode) =>
        "SELECT report_date, branch_code, channel_code, userrole_code, sales, nr_transactions, modified_ts " +
        "FROM public.rpt_daily_sales WHERE branch_code = " + Quote(branchCode) +
        " ORDER BY modified_ts, report_date, channel_code, userrole_code";

    /// <summary>
    /// Incremental pull from a peer branch DB: rows changed after the stored <c>modified_ts</c> cursor.
    /// </summary>
    public string SelectRemoteRptDailySalesModifiedAfter(string branchCode, DateTime modifiedAfterUnspecified) =>
        "SELECT report_date, branch_code, channel_code, userrole_code, sales, nr_transactions, modified_ts " +
        "FROM public.rpt_daily_sales WHERE branch_code = " + Quote(branchCode) +
        " AND modified_ts > " + TsUnspecified(modifiedAfterUnspecified) +
        " ORDER BY modified_ts, report_date, channel_code, userrole_code";

    /// <summary>
    /// Local DB: remove replicated slice for a peer branch before reloading from remote (home branch excluded by caller).
    /// </summary>
    public string DeleteLocalRptDailySalesForBranch(string branchCode) =>
        "DELETE FROM public.rpt_daily_sales WHERE branch_code = " + Quote(branchCode) + "; ";

    /// <summary>PostgreSQL 9.3-safe upsert for one <c>rpt_daily_sales</c> fact row (peer replication).</summary>
    public string UpsertLocalRptDailySalesRow(
        DateOnly reportDate,
        string branchCode,
        string channelCode,
        string userroleCode,
        decimal sales,
        int nrTransactions,
        DateTime remoteModifiedTsUnspecified)
    {
        var rd = Date(reportDate);
        var bc = Quote(branchCode);
        var cc = Quote(channelCode);
        var ur = Quote(userroleCode);
        var mt = TsUnspecified(remoteModifiedTsUnspecified);
        return
            "UPDATE public.rpt_daily_sales SET sales = " + Num(sales) + ", nr_transactions = " + Int(nrTransactions) +
            ", modified_ts = " + mt + " WHERE report_date = " + rd + " AND branch_code = " + bc +
            " AND channel_code = " + cc + " AND userrole_code = " + ur + "; " +
            "INSERT INTO public.rpt_daily_sales (report_date, branch_code, channel_code, userrole_code, sales, nr_transactions, modified_ts) SELECT " +
            rd + ", " + bc + ", " + cc + ", " + ur + ", " + Num(sales) + ", " + Int(nrTransactions) + ", " + mt +
            " WHERE NOT EXISTS (SELECT 1 FROM public.rpt_daily_sales WHERE report_date = " + rd + " AND branch_code = " + bc +
            " AND channel_code = " + cc + " AND userrole_code = " + ur + "); ";
    }

    /// <summary>Prefix for batched <c>INSERT</c> including explicit <c>modified_ts</c> (full peer reload).</summary>
    public string InsertRptDailySalesBatchPrefixWithModifiedTs() =>
        "INSERT INTO public.rpt_daily_sales (report_date, branch_code, channel_code, userrole_code, sales, nr_transactions, modified_ts) VALUES ";

    /// <summary>One row for <see cref="InsertRptDailySalesBatchPrefixWithModifiedTs"/> batches.</summary>
    public static string InsertRptDailySalesValuesRowWithModifiedTs(
        DateOnly reportDate,
        string branchCode,
        string channelCode,
        string userroleCode,
        decimal sales,
        int nrTransactions,
        DateTime remoteModifiedTsUnspecified) =>
        "(" + Date(reportDate) + ", " + Quote(branchCode) + ", " + Quote(channelCode) + ", " + Quote(userroleCode) + ", " +
        Num(sales) + ", " + Int(nrTransactions) + ", " + TsUnspecified(remoteModifiedTsUnspecified) + ")";

    /// <summary>
    /// Local peer replication cursors: <c>rpt_daily_sales</c> uses <c>last_remote_max_modified_ts</c> /
    /// <c>last_full_reconcile_utc</c>; <c>rpt_vat</c> uses <c>vat_last_*</c> columns.
    /// </summary>
    public string SelectRptReplicationPeerState(string sourceBranchCode) =>
        "SELECT source_branch_code, last_success_sync_utc, last_remote_max_modified_ts, last_full_reconcile_utc, " +
        "vat_last_remote_max_modified_ts, vat_last_full_reconcile_utc " +
        "FROM public.rpt_replication_peer_state WHERE source_branch_code = " + Quote(sourceBranchCode.Trim());

    /// <summary>
    /// PostgreSQL 9.3-safe upsert into <c>public.rpt_replication_peer_state</c> for <c>rpt_daily_sales</c> cursors only.
    /// Does not modify VAT cursor columns.
    /// </summary>
    public string UpsertRptReplicationPeerState(
        string sourceBranch,
        DateTime lastSuccessUtc,
        DateTime? lastRemoteMaxModifiedUnspecified,
        DateTime? lastFullReconcileUtc)
    {
        var bc = Quote(sourceBranch.Trim());
        var u1 = Ts(lastSuccessUtc);
        var u2 = lastRemoteMaxModifiedUnspecified == null ? "NULL" : TsUnspecified(lastRemoteMaxModifiedUnspecified.Value);
        var u3 = lastFullReconcileUtc == null ? "NULL" : Ts(lastFullReconcileUtc.Value);
        return
            "UPDATE public.rpt_replication_peer_state SET last_success_sync_utc = " + u1 +
            ", last_remote_max_modified_ts = " + u2 + ", last_full_reconcile_utc = " + u3 +
            " WHERE source_branch_code = " + bc + "; " +
            "INSERT INTO public.rpt_replication_peer_state " +
            "(source_branch_code, last_success_sync_utc, last_remote_max_modified_ts, last_full_reconcile_utc, vat_last_remote_max_modified_ts, vat_last_full_reconcile_utc) SELECT " +
            bc + ", " + u1 + ", " + u2 + ", " + u3 + ", NULL, NULL " +
            " WHERE NOT EXISTS (SELECT 1 FROM public.rpt_replication_peer_state WHERE source_branch_code = " + bc + "); ";
    }

    /// <summary>
    /// PostgreSQL 9.3-safe upsert of <c>rpt_vat</c> peer replication cursors. Does not modify daily-sales cursor columns.
    /// </summary>
    public string UpsertRptReplicationPeerStateVat(
        string sourceBranch,
        DateTime lastSuccessUtc,
        DateTime? vatLastRemoteMaxModifiedUnspecified,
        DateTime? vatLastFullReconcileUtc)
    {
        var bc = Quote(sourceBranch.Trim());
        var u1 = Ts(lastSuccessUtc);
        var u2 = vatLastRemoteMaxModifiedUnspecified == null ? "NULL" : TsUnspecified(vatLastRemoteMaxModifiedUnspecified.Value);
        var u3 = vatLastFullReconcileUtc == null ? "NULL" : Ts(vatLastFullReconcileUtc.Value);
        return
            "UPDATE public.rpt_replication_peer_state SET last_success_sync_utc = " + u1 +
            ", vat_last_remote_max_modified_ts = " + u2 + ", vat_last_full_reconcile_utc = " + u3 +
            " WHERE source_branch_code = " + bc + "; " +
            "INSERT INTO public.rpt_replication_peer_state " +
            "(source_branch_code, last_success_sync_utc, last_remote_max_modified_ts, last_full_reconcile_utc, vat_last_remote_max_modified_ts, vat_last_full_reconcile_utc) SELECT " +
            bc + ", " + u1 + ", NULL, NULL, " + u2 + ", " + u3 +
            " WHERE NOT EXISTS (SELECT 1 FROM public.rpt_replication_peer_state WHERE source_branch_code = " + bc + "); ";
    }

    /// <summary>Whether a successful reporting sync already completed for the given local calendar day.</summary>
    public string SelectHasSuccessfulRptSyncForSyncDate(DateOnly syncDate) =>
        "SELECT 1 AS ok FROM public.rpt_sync_run_log WHERE sync_date = " + Date(syncDate) +
        " AND success = TRUE LIMIT 1";

    /// <summary>Most recent sync attempt row for a calendar day (success / finished / started).</summary>
    public string SelectRptSyncRunLastForSyncDate(DateOnly syncDate) =>
        "SELECT success, finished_ts, started_ts FROM public.rpt_sync_run_log WHERE sync_date = " + Date(syncDate) +
        " ORDER BY run_id DESC LIMIT 1";

    public string InsertRptSyncRunStart(string machineName, string trigger, DateOnly syncDate) =>
        "INSERT INTO public.rpt_sync_run_log (machine_name, trigger, sync_date, started_ts) VALUES (" +
        Quote(machineName) + ", " + Quote(trigger) + ", " + Date(syncDate) + ", now()) RETURNING run_id";

    public string UpdateRptSyncRunFinish(int runId, bool success, string? errorMessage)
    {
        string errSql;
        if (string.IsNullOrEmpty(errorMessage))
            errSql = "NULL";
        else
        {
            var t = errorMessage.Length > 4000 ? errorMessage.Substring(0, 4000) : errorMessage;
            errSql = Quote(t);
        }

        return "UPDATE public.rpt_sync_run_log SET finished_ts = now(), success = " + Bool(success) +
            ", error_message = " + errSql + " WHERE run_id = " + Int(runId) + "; ";
    }

    /// <summary>
    /// Retention cleanup: delete access log rows older than <paramref name="maxRetentionDays"/> (by <c>accessed_ts</c>).
    /// PostgreSQL 9.3-safe; <paramref name="maxRetentionDays"/> must be composed via <see cref="Int(int)"/> at the call site.
    /// </summary>
    public string DeleteRptReportAccessLogOlderThanDays(int maxRetentionDays) =>
        "DELETE FROM public.rpt_report_access_log WHERE accessed_ts < (CURRENT_TIMESTAMP - (" +
        Int(maxRetentionDays) + " * INTERVAL '1 day')); ";

    /// <summary>
    /// Retention cleanup: delete sync audit rows whose <c>sync_date</c> is strictly before <c>CURRENT_DATE - N days</c>.
    /// </summary>
    public string DeleteRptSyncRunLogOlderThanDays(int maxRetentionDays) =>
        "DELETE FROM public.rpt_sync_run_log WHERE sync_date < (CURRENT_DATE - " + Int(maxRetentionDays) + "); ";

    /// <summary>
    /// Retention cleanup: delete peer replication rows with no recent activity (stale cursors). The next sync
    /// performs a full reconcile for removed peers. Uses <c>COALESCE(last_success_sync_utc, last_full_reconcile_utc, epoch)</c>.
    /// </summary>
    public string DeleteRptReplicationPeerStateStaleOlderThanDays(int maxRetentionDays) =>
        "DELETE FROM public.rpt_replication_peer_state WHERE " +
        "COALESCE(last_success_sync_utc, last_full_reconcile_utc, vat_last_full_reconcile_utc, TIMESTAMP '1970-01-01 00:00:00') " +
        "< (CURRENT_TIMESTAMP - (" + Int(maxRetentionDays) + " * INTERVAL '1 day')); ";

    /// <summary>Post-sync maintenance (autocommit): database-wide analyze for the planner.</summary>
    public string AnalyzeLocalDatabaseForMaintenance() => "ANALYZE;";

    /// <summary>Post-sync maintenance (autocommit): non-blocking vacuum plus analyze on user tables.</summary>
    public string VacuumAnalyzeLocalDatabaseForMaintenance() => "VACUUM ANALYZE;";

    /// <summary>Post-sync maintenance (autocommit): may require superuser on some installs.</summary>
    public string AnalyzePgCatalogPgClassForMaintenance() => "ANALYZE pg_catalog.pg_class;";

    /// <summary>Post-sync maintenance (autocommit): may require superuser on some installs.</summary>
    public string AnalyzePgCatalogPgAttributeForMaintenance() => "ANALYZE pg_catalog.pg_attribute;";

    /// <summary>Post-sync maintenance (autocommit): may require superuser on some installs.</summary>
    public string AnalyzePgCatalogPgIndexForMaintenance() => "ANALYZE pg_catalog.pg_index;";

    /// <summary>
    /// Local branch DB: aggregate daily sales by branch for the Daily Sales Summary report.
    /// Filters use sentinel <c>all</c> (case-insensitive) for unrestricted branch/channel/user role.
    /// </summary>
    public string SelectLocalRptDailySalesAggregatedByBranch(
            DateOnly rangeStart,
            DateOnly rangeEnd,
            string branchFilterCode,
            string channelFilterCode,
            string userRoleFilterCode)
    {
        var sb = new StringBuilder(512);
        sb.Append(
                "SELECT d.branch_code, MAX(COALESCE(b.descr, d.branch_code)) AS branch_descr, " +
                "SUM(d.sales) AS sum_sales, SUM(d.nr_transactions) AS sum_transactions " +
                "FROM public.rpt_daily_sales d " +
                "LEFT JOIN public.rpt_branches b ON b.branch_code = d.branch_code AND COALESCE(b.active, TRUE) = TRUE " +
                "WHERE d.report_date >= ").Append(Date(rangeStart)).Append(" AND d.report_date <= ").Append(Date(rangeEnd));

        if (!string.Equals(branchFilterCode?.Trim(), "all", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(branchFilterCode))
            sb.Append(" AND d.branch_code = ").Append(Quote(branchFilterCode.Trim()));

        if (!string.Equals(channelFilterCode?.Trim(), "all", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(channelFilterCode))
            sb.Append(" AND d.channel_code = ").Append(Quote(channelFilterCode.Trim()));

        if (!string.Equals(userRoleFilterCode?.Trim(), "all", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(userRoleFilterCode))
            sb.Append(" AND d.userrole_code = ").Append(Quote(userRoleFilterCode.Trim()));

        sb.Append(
                " GROUP BY d.branch_code ORDER BY MAX(COALESCE(b.descr, d.branch_code)); ");
        return sb.ToString();
    }

    /// <summary>
    /// Local branch DB: aggregate VAT facts by <c>vat_rate_id</c> for the VAT Summary report.
    /// Returns net, VAT amount (<c>net * rate</c>), and gross (<c>net * (1 + rate)</c>) per band.
    /// Filters use sentinel <c>all</c> (case-insensitive) for unrestricted branch/channel/user role.
    /// </summary>
    public string SelectLocalRptVatAggregatedByVatRate(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        string branchFilterCode,
        string channelFilterCode,
        string userRoleFilterCode)
    {
        var sb = new StringBuilder(768);
        sb.Append(
            "SELECT v.vat_rate_id, MAX(COALESCE(vr.descr, '')) AS descr, MAX(vr.rate) AS vat_rate, " +
            "SUM(v.net_amount) AS sum_net_amount, " +
            "SUM(v.net_amount * COALESCE(vr.rate, 0)) AS sum_vat_amount, " +
            "SUM(v.net_amount * (1 + COALESCE(vr.rate, 0))) AS sum_gross_amount " +
            "FROM public.rpt_vat v " +
            "INNER JOIN public.taxes vr ON vr.tax_id = v.vat_rate_id AND vr.active = TRUE AND COALESCE(vr.deleted, FALSE) = FALSE " +
            "WHERE v.report_date >= ").Append(Date(rangeStart)).Append(" AND v.report_date <= ").Append(Date(rangeEnd));

        if (!string.Equals(branchFilterCode?.Trim(), "all", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(branchFilterCode))
            sb.Append(" AND v.branch_code = ").Append(Quote(branchFilterCode.Trim()));

        if (!string.Equals(channelFilterCode?.Trim(), "all", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(channelFilterCode))
            sb.Append(" AND v.channel_code = ").Append(Quote(channelFilterCode.Trim()));

        if (!string.Equals(userRoleFilterCode?.Trim(), "all", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(userRoleFilterCode))
            sb.Append(" AND v.userrole_code = ").Append(Quote(userRoleFilterCode.Trim()));

        sb.Append(" GROUP BY v.vat_rate_id ORDER BY v.vat_rate_id; ");
        return sb.ToString();
    }

    /// <summary>Prefix for a multi-row <c>INSERT</c> into <c>public.rpt_daily_sales</c>.</summary>
    public string InsertRptDailySalesBatchPrefix() =>
        "INSERT INTO public.rpt_daily_sales (report_date, branch_code, channel_code, userrole_code, sales, nr_transactions) VALUES ";

    /// <summary>One parenthesized row for batched <c>INSERT</c> into <c>public.rpt_daily_sales</c>.</summary>
    public static string InsertRptDailySalesValuesRow(
        DateOnly reportDate, string branchCode, string channelCode, string userroleCode, decimal sales, int nrTransactions) =>
        "(" + Date(reportDate) + ", " + Quote(branchCode) + ", " + Quote(channelCode) + ", " + Quote(userroleCode) + ", " +
        Num(sales) + ", " + Int(nrTransactions) + ")";

    // region: Operations and Services — seed ---------------------------------------------------------
    //
    // Mirrors the Staff and Access pattern (<see cref="DeleteAllSeedRows"/> +
    // <see cref="InsertSeedUsers"/>): when App.config's SeedDummyDataOnStartup is TRUE, the app
    // wipes every is_seed = TRUE row and reinserts the canned demo dataset so reviewers always
    // see the same shifts, tables, reservations, and floor-plan layout on launch.

    /// <summary>
    /// Delete every <c>is_seed = TRUE</c> row from the Operations and Services tables. Order
    /// matters: ops_shift_tables and ops_floor_plan_layouts reference tables / shifts by UUID
    /// (no FKs in DDL, but we keep the deletion order intuitive for readers of the audit log).
    /// </summary>
    public string DeleteAllOpsSeedRows() =>
        "DELETE FROM public.ops_shift_tables WHERE is_seed = TRUE; " +
        "DELETE FROM public.ops_shifts WHERE is_seed = TRUE; " +
        "DELETE FROM public.ops_reservations WHERE is_seed = TRUE; " +
        "DELETE FROM public.ops_floor_plan_layouts WHERE is_seed = TRUE; " +
        "DELETE FROM public.ops_tables WHERE is_seed = TRUE; " +
        "DELETE FROM public.ops_floors WHERE is_seed = TRUE;";

    /// <summary>
    /// Build the full demo INSERT batch for Operations and Services. All UUIDs are minted in C#
    /// so the cross-references between seeded tables, shifts, reservations, and floor-plan layouts
    /// resolve correctly inside a single transaction. The employee ids come from the seeded users
    /// (1001..1006); see <see cref="InsertSeedUsers"/>. Floor ids start at 10001 so they sit
    /// comfortably above any client-created floor.
    /// </summary>
    public string InsertSeedOpsServices()
    {
        var auth = AppStatus.SystemBootstrapUserId;
        var today = DateTime.Today;
        var b = new StringBuilder();

        // Floors -------------------------------------------------------------------------------
        const int mainFloorId = 10001;
        const int patioFloorId = 10002;
        const string mainFloor = "Main Floor";
        const string patio = "Patio";
        b.Append(InsertOpsFloor(mainFloorId, mainFloor, auth, isSeed: true));
        b.Append(' ');
        b.Append(InsertOpsFloor(patioFloorId, patio, auth, isSeed: true));
        b.Append(' ');

        // Tables -------------------------------------------------------------------------------
        // Seed 7 tables mirroring the historical in-memory demo set. Waiter ids match the first
        // two seeded users (1001 = John, 1002 = Sarah). Keep the UUIDs fresh each reseed cycle;
        // the in-memory store is reloaded from DB so consumers see the new IDs immediately.
        var tbl1 = Guid.NewGuid();
        var tbl2 = Guid.NewGuid();
        var tbl3 = Guid.NewGuid();
        var tbl4 = Guid.NewGuid();
        var tbl5 = Guid.NewGuid();
        var tbl6 = Guid.NewGuid();
        var tbl7 = Guid.NewGuid();

        const int johnId = 1001;
        const int sarahId = 1002;

        AppendSeedTable(b, tbl1, "Table 1", mainFloor, 4, "Square", true, johnId, today, isOccupied: false, auth);
        AppendSeedTable(b, tbl2, "Table 2", mainFloor, 6, "Square", true, sarahId, today, isOccupied: true, auth, johnId);
        AppendSeedTable(b, tbl3, "Table 3", mainFloor, 4, "Square", true, null, today, isOccupied: false, auth);
        AppendSeedTable(b, tbl4, "Table 4", mainFloor, 2, "Square", true, null, today, isOccupied: false, auth);
        AppendSeedTable(b, tbl5, "Table 5", mainFloor, 4, "Square", false, null, today, isOccupied: false, auth);
        AppendSeedTable(b, tbl6, "Table 6", patio, 8, "Square", true, null, today, isOccupied: false, auth);
        AppendSeedTable(b, tbl7, "VIP Table", patio, 6, "Round", true, null, today, isOccupied: false, auth);

        // Shifts -------------------------------------------------------------------------------
        // Previous week through current week + 6 weeks (8 weeks total) so Week/Month views have
        // content to render without the user paging around. Mirrors the previous in-memory seeder.
        int[] empIds = { 1001, 1002, 1003, 1004, 1005, 1006 };
        var week0 = DateOnly.FromDateTime(StartOfWeekMonday(today.AddDays(-7)));
        for (var w = 0; w < 8; w++)
        {
            var monday = week0.AddDays(w * 7);

            AppendSeedShift(b, empIds[0], monday.AddDays(0),
                new TimeOnly(9, 0), new TimeOnly(17, 0),
                new[] { tbl1, tbl2 }, "Weekly", auth);
            AppendSeedShift(b, empIds[1], monday.AddDays(1),
                new TimeOnly(10, 0), new TimeOnly(18, 0),
                new[] { tbl2 }, "Weekly", auth);
            AppendSeedShift(b, empIds[2], monday.AddDays(2),
                new TimeOnly(12, 0), new TimeOnly(20, 0),
                Array.Empty<Guid>(), "Daily", auth);
            AppendSeedShift(b, empIds[3], monday.AddDays(3),
                new TimeOnly(9, 0), new TimeOnly(15, 0),
                new[] { tbl3, tbl4 }, "Daily", auth);
            if (w % 2 == 0)
            {
                AppendSeedShift(b, empIds[4], monday.AddDays(4),
                    new TimeOnly(9, 0), new TimeOnly(17, 0),
                    new[] { tbl6 }, "Monthly", auth);
            }
            AppendSeedShift(b, empIds[5], monday.AddDays(5),
                new TimeOnly(11, 0), new TimeOnly(19, 0),
                new[] { tbl1 }, "Weekly", auth);
        }

        // Reservations --------------------------------------------------------------------------
        var todayOnly = DateOnly.FromDateTime(today);
        AppendSeedReservation(b, tbl3, mainFloor, todayOnly, new TimeOnly(19, 30),
            "Robert Williams", "+44 078 890 0000", "robert@example.com", 4,
            "Confirmed", "Special request for a vegetarian menu.", "RW-1042", auth);
        AppendSeedReservation(b, tbl4, mainFloor, todayOnly, new TimeOnly(20, 15),
            "Mike Brown", "+44 070 100 2000", null, 2,
            "Seated", "", "MB-2211", auth);
        AppendSeedReservation(b, tbl7, patio, todayOnly, new TimeOnly(20, 0),
            "Barbara Miller", "+44 078 234 555", null, 2,
            "Pending", "", "BM-8831", auth);
        AppendSeedReservation(b, tbl2, mainFloor, todayOnly, new TimeOnly(20, 0),
            "David Anderson", "+44 078 111 2222", null, 4,
            "Confirmed", "", "DA-9901", auth);
        AppendSeedReservation(b, tbl1, mainFloor, todayOnly.AddDays(-1), new TimeOnly(18, 0),
            "Jennifer Taylor", "+44 078 000 3333", null, 3,
            "Completed", "", "JT-7712", auth);

        return b.ToString();
    }

    /// <summary>Monday-based start-of-week (client's roster week). Kept here to avoid pulling in OpsServicesStore from Sql.</summary>
    private static DateTime StartOfWeekMonday(DateTime d)
    {
        var date = d.Date;
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff);
    }

    private void AppendSeedTable(
        StringBuilder b, Guid tableId, string name, string floor, int seats, string shape,
        bool isActive, int? waiterId, DateTime today, bool isOccupied, int authUserId,
        int? serverId = null)
    {
        var write = new OpsTableWrite
        {
            TableId = tableId,
            Name = name,
            LocationName = floor,
            SeatCount = seats,
            Shape = shape,
            IsActive = isActive,
            AssignedWaiterId = waiterId,
            Status = "Available",
            OpsStatus = isOccupied ? "Occupied" : (isActive ? "Available" : "Inactive"),
            OpsServerId = isOccupied ? serverId : null,
            SeatedAtUtc = isOccupied ? DateTime.UtcNow.AddMinutes(-106) : (DateTime?)null,
            PartySize = isOccupied ? 4 : (int?)null,
            CreatedUtc = today.AddDays(-120).ToUniversalTime(),
            ModifiedUtc = today.AddDays(-1).ToUniversalTime(),
            IsSeed = true
        };
        b.Append(InsertOpsTable(write, authUserId));
        b.Append(' ');
    }

    private void AppendSeedShift(
        StringBuilder b, int employeeId, DateOnly date, TimeOnly start, TimeOnly end,
        Guid[] tableIds, string sourceKind, int authUserId)
    {
        var shiftId = Guid.NewGuid();
        b.Append(InsertOpsShift(shiftId, employeeId, date, start, end, sourceKind, authUserId, isSeed: true));
        b.Append(' ');
        foreach (var tableId in tableIds)
        {
            b.Append(InsertOpsShiftTableLink(shiftId, tableId, isSeed: true));
            b.Append(' ');
        }
    }

    private void AppendSeedReservation(
        StringBuilder b, Guid tableId, string floor, DateOnly date, TimeOnly time,
        string name, string phone, string? email, int party, string status,
        string notes, string reference, int authUserId)
    {
        var write = new OpsReservationWrite
        {
            ReservationId = Guid.NewGuid(),
            TableId = tableId,
            FloorName = floor,
            Date = date,
            Time = time,
            CustomerName = name,
            Phone = phone,
            Email = email,
            PartySize = party,
            Status = status,
            Notes = notes,
            Reference = reference,
            IsSeed = true
        };
        b.Append(InsertOpsReservation(write, authUserId));
        b.Append(' ');
    }
}
