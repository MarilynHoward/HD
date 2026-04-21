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

    // region: Roles ----------------------------------------------------------------------------------

    /// <summary>Active, non-deleted roles ordered by id. Columns: role_id, descr, active.</summary>
    public string SelectActiveRoles() =>
        "SELECT role_id, descr, active " +
        "FROM public.roles " +
        "WHERE active = TRUE AND deleted = FALSE " +
        "ORDER BY role_id";

    /// <summary>Guarantee the five fixed roles exist. Idempotent; uses ON CONFLICT DO NOTHING.</summary>
    public string EnsureFixedRoles()
    {
        var b = new StringBuilder();
        AppendEnsureRole(b, AppStatus.RoleIdAdmin, "Admin");
        AppendEnsureRole(b, AppStatus.RoleIdManager, "Manager");
        AppendEnsureRole(b, AppStatus.RoleIdSupervisor, "Supervisor");
        AppendEnsureRole(b, AppStatus.RoleIdUser, "User");
        AppendEnsureRole(b, AppStatus.RoleIdSystem, "System");
        return b.ToString();
    }

    private static void AppendEnsureRole(StringBuilder b, int roleId, string descr)
    {
        b.Append("INSERT INTO public.roles (role_id, descr, auth_user_id, active, deleted, affected_ts, is_seed) ")
         .Append("VALUES (").Append(Int(roleId)).Append(", ").Append(Quote(descr))
         .Append(", ").Append(Int(AppStatus.SystemBootstrapUserId))
         .Append(", TRUE, FALSE, now(), FALSE) ")
         .Append("ON CONFLICT (role_id) DO NOTHING; ");
    }

    /// <summary>
    /// Guarantee bootstrap <c>user_id = 1</c> ("system") exists so <c>auth_user_id</c> has a valid FK
    /// target before any real login exists. Self-references via <c>auth_user_id = 1</c>.
    /// </summary>
    public string EnsureBootstrapSystemUser()
    {
        return "INSERT INTO public.users " +
               "(user_id, user_name, password, role_id, auth_user_id, first_name, surname, active, affected_ts, is_seed) " +
               "VALUES (" +
               Int(AppStatus.SystemBootstrapUserId) + ", 'system', '', " +
               Int(AppStatus.RoleIdSystem) + ", " +
               Int(AppStatus.SystemBootstrapUserId) + ", 'System', 'Account', TRUE, now(), FALSE) " +
               "ON CONFLICT (user_id) DO NOTHING;";
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
        var where = "WHERE user_id <> " + Int(AppStatus.SystemBootstrapUserId) +
                    " AND COALESCE(deleted, FALSE) = FALSE";
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

    public string InsertUser(UserWrite u, int authUserId, string hashedPassword)
    {
        return "INSERT INTO public.users " +
               "(user_id, user_name, password, role_id, auth_user_id, image_path, card_number, " +
               " first_name, second_name, surname, id_doc_path, active, affected_ts, " +
               " job_title, middle_name, accent_color_hex, biometric_enrolled, " +
               " id_doc_file_name, profile_image_rel_path, id_doc_sync_status, profile_image_sync_status, is_seed) " +
               "VALUES (" +
               Int(u.UserId) + ", " +
               Quote(u.UserName) + ", " +
               Quote(hashedPassword) + ", " +
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

    public string UpdateUserPassword(int userId, string hashedPassword, int authUserId) =>
        "UPDATE public.users SET " +
        "password = " + Quote(hashedPassword) + ", " +
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
    /// Soft delete a role. The five fixed roles (1..5) are protected — the WHERE clause prevents
    /// the client from accidentally retiring Admin / Manager / Supervisor / User / System.
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
    /// Passwords are the same plain-text value ("password") hashed with PBKDF2.
    /// </summary>
    public string InsertSeedUsers()
    {
        var pw = PasswordHasher.Hash("password");
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
            b.Append("INSERT INTO public.users ")
             .Append("(user_id, user_name, password, role_id, auth_user_id, card_number, ")
             .Append(" first_name, surname, active, affected_ts, ")
             .Append(" job_title, accent_color_hex, is_seed) ")
             .Append("VALUES (")
             .Append(Int(r.Id)).Append(", ")
             .Append(Quote(r.User)).Append(", ")
             .Append(Quote(pw)).Append(", ")
             .Append(Int(r.RoleId)).Append(", ")
             .Append(Int(auth)).Append(", ")
             .Append(Quote(r.Card)).Append(", ")
             .Append(Quote(r.First)).Append(", ")
             .Append(Quote(r.Last)).Append(", TRUE, now(), ")
             .Append(Quote(r.Job)).Append(", ")
             .Append(Quote(palette[i % palette.Length])).Append(", TRUE) ")
             .Append("ON CONFLICT (user_id) DO NOTHING; ");
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
    /// known. Uses <c>COALESCE(MAX(phase_id), 0) + 1</c> and <c>ON CONFLICT (descr) DO NOTHING</c>
    /// so concurrent calls are safe. Pair with <see cref="SelectPhaseIdByDescr"/> to get the id.
    /// </summary>
    public string EnsurePhaseIdUpsert(string descr) =>
        "INSERT INTO public.database_update_phase (phase_id, descr) " +
        "SELECT COALESCE(MAX(phase_id), 0) + 1, " + Quote(descr) + " " +
        "FROM public.database_update_phase " +
        "ON CONFLICT (descr) DO NOTHING;";

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
}
