-- ============================================================================
-- Staff and Access PostgreSQL migration
-- Date: 2026-04-21
-- Target: public.users, public.roles
-- Notes for the client DBA:
--   * All ALTER TABLE / INSERT statements are idempotent. Safe to run multiple
--     times: `ADD COLUMN IF NOT EXISTS` is a no-op when the column is there;
--     `ON CONFLICT DO NOTHING` is a no-op when the row is there.
--   * users.password continues to be `text`. Its SEMANTICS change: the WPF app
--     now writes a PBKDF2 record of the form
--         pbkdf2$<iterations>$<base64 salt>$<base64 subkey>
--     Legacy plain-text values are still accepted on sign-in and will be
--     upgraded on first successful login.
--   * Audit trail: lives in a companion migration file
--     2026-04-21_audit_trail_init.sql (creates public.audit_trail and
--     public.database_update_phase -- client's canonical schema). Run the
--     audit migration alongside this one. users.audit_id is still written
--     as NULL by the app; the live event stream is public.audit_trail.
--   * is_seed column guards the "reseed dummy data on startup" feature: the
--     app only deletes rows where is_seed = TRUE. Production data MUST keep
--     is_seed = FALSE (default) so it is never touched by reseed.
--   * Bootstrap ordering: the only relevant FK between these two tables is
--     public.roles.auth_user_id -> public.users.user_id. public.users.role_id
--     is NOT NULL but has NO FK to public.roles. So we insert the bootstrap
--     user (user_id = 1) FIRST -- its auth_user_id = 1 is a self-reference,
--     which Postgres accepts in a single INSERT without DEFERRABLE tricks --
--     then we insert the 5 fixed roles with auth_user_id = 1.
-- ============================================================================

-- If a previous run of this script aborted inside a transaction, the session
-- is in "transaction aborted" state (SQLSTATE 25P02) and every command is
-- refused until ROLLBACK. ROLLBACK with no active transaction is a harmless
-- warning, so it is safe to execute unconditionally before BEGIN.
ROLLBACK;

BEGIN;

-- ---------------------------------------------------------------------------
-- public.roles: add is_seed (so fixed seed roles can be distinguished from
-- client-created roles). No other structural changes to roles.
-- ---------------------------------------------------------------------------
ALTER TABLE public.roles
    ADD COLUMN IF NOT EXISTS is_seed boolean NOT NULL DEFAULT FALSE;

-- ---------------------------------------------------------------------------
-- public.users: extended columns required by the Staff & Access UI.
--   * job_title              -- operational job label (Server, Host, ...)
--   * middle_name            -- keeps users.second_name for legacy use and
--                               introduces a distinct middle name. The app
--                               currently writes both to stay consistent.
--   * accent_color_hex       -- per-user accent (#RRGGBB) used in UI badges.
--   * biometric_enrolled     -- fingerprint enrolment flag (finger_print text
--                               already exists for template storage).
--   * id_doc_file_name       -- original uploaded filename for ID PDFs.
--   * profile_image_rel_path -- relative path under the configured staff docs
--                               repository root (disk source of truth).
--   * id_doc_sync_status     -- 'Synced' | 'PendingSync' (simulated remote).
--   * profile_image_sync_status
--   * is_seed                -- TRUE for rows inserted by the app's dummy-
--                               data seeder. The app only ever deletes seed
--                               rows.
-- ---------------------------------------------------------------------------
ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS job_title                text,
    ADD COLUMN IF NOT EXISTS middle_name              text,
    ADD COLUMN IF NOT EXISTS accent_color_hex         text,
    ADD COLUMN IF NOT EXISTS biometric_enrolled       boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS id_doc_file_name         text,
    ADD COLUMN IF NOT EXISTS profile_image_rel_path   text,
    ADD COLUMN IF NOT EXISTS id_doc_sync_status       text,
    ADD COLUMN IF NOT EXISTS profile_image_sync_status text,
    ADD COLUMN IF NOT EXISTS is_seed                  boolean NOT NULL DEFAULT FALSE;

-- ---------------------------------------------------------------------------
-- Bootstrap system user (user_id = 1). auth_user_id = 1 is a self-reference;
-- Postgres accepts it on a single INSERT. role_id = 5 has no FK in the base
-- DDL, so inserting before the roles row is fine.
-- ---------------------------------------------------------------------------
INSERT INTO public.users
    (user_id, user_name, password, role_id, auth_user_id, first_name, surname,
     active, affected_ts, is_seed)
VALUES
    (1, 'system', '', 5, 1, 'System', 'Account', TRUE, now(), FALSE)
ON CONFLICT (user_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Fixed roles (1..5). Descriptions match the app's legacy StaffAccessRole
-- enum so the UI can map role_id <-> display name. auth_user_id = 1 is
-- satisfied because the bootstrap user was inserted above. Existing rows
-- are left untouched.
-- ---------------------------------------------------------------------------
INSERT INTO public.roles (role_id, descr, auth_user_id, active, deleted, affected_ts, is_seed)
VALUES
    (1, 'Admin',      1, TRUE, FALSE, now(), FALSE),
    (2, 'Manager',    1, TRUE, FALSE, now(), FALSE),
    (3, 'Supervisor', 1, TRUE, FALSE, now(), FALSE),
    (4, 'User',       1, TRUE, FALSE, now(), FALSE),
    (5, 'System',     1, TRUE, FALSE, now(), FALSE)
ON CONFLICT (role_id) DO NOTHING;

COMMIT;

-- ============================================================================
-- Related migrations (apply together, any order):
--   * 2026-04-21_audit_trail_init.sql    -- creates public.audit_trail and
--       public.database_update_phase (client's canonical audit schema) and
--       drops the interim public.audit_log table if it exists.
--   * 2026-04-21_users_soft_delete.sql   -- adds deleted / deleted_ts /
--       deleted_user_id to public.users so account deletions are reversible
--       and preserve Operations history.
-- ============================================================================
