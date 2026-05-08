-- ============================================================================
-- Staff and Access PostgreSQL migration
-- Date: 2026-04-21
-- Target: public.users, public.roles
-- PostgreSQL target: 9.3 (client production). This script is deliberately
-- written against the 9.3 feature set:
--   * NO `ON CONFLICT ...`        (added in 9.5) -- use INSERT ... WHERE NOT EXISTS.
--   * NO `ADD COLUMN IF NOT EXISTS` (added in 9.6) -- use DO blocks that probe
--     information_schema.columns.
--   * NO `CREATE INDEX IF NOT EXISTS` (added in 9.5) -- use DO blocks that probe
--     pg_indexes.
-- `CREATE TABLE IF NOT EXISTS`, `CREATE EXTENSION IF NOT EXISTS`, and PL/pgSQL
-- DO blocks are all valid from 9.1/9.0 and ARE used.
--
-- Notes for the client DBA:
--   * All ALTER TABLE / INSERT statements are idempotent. Safe to run multiple
--     times: the DO blocks are a no-op when the column/index is there; the
--     INSERT ... WHERE NOT EXISTS guards are a no-op when the row is there.
--   * users.password continues to be `text`. Its SEMANTICS: the WPF app
--     encrypts the value with the AppStatus.crypt (Crypt) cipher on every save,
--     seed and reseed. PasswordHasher was removed.
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
--     user (user_id = 0) FIRST -- its auth_user_id = 0 is a self-reference,
--     which Postgres accepts in a single INSERT without DEFERRABLE tricks --
--     then we insert the 6 fixed roles (Developer = 0, Admin = 1, Manager = 2,
--     Supervisor = 3, User = 4, System = 5) with auth_user_id = 1.
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
-- 9.3-safe: emulate ADD COLUMN IF NOT EXISTS via a DO block.
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'roles' AND column_name = 'is_seed'
    ) THEN
        ALTER TABLE public.roles
            ADD COLUMN is_seed boolean NOT NULL DEFAULT FALSE;
    END IF;
END$$;

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
-- 9.3-safe: emulate ADD COLUMN IF NOT EXISTS via a DO block.
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='job_title') THEN
        ALTER TABLE public.users ADD COLUMN job_title text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='middle_name') THEN
        ALTER TABLE public.users ADD COLUMN middle_name text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='accent_color_hex') THEN
        ALTER TABLE public.users ADD COLUMN accent_color_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='biometric_enrolled') THEN
        ALTER TABLE public.users ADD COLUMN biometric_enrolled boolean NOT NULL DEFAULT FALSE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='id_doc_file_name') THEN
        ALTER TABLE public.users ADD COLUMN id_doc_file_name text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='profile_image_rel_path') THEN
        ALTER TABLE public.users ADD COLUMN profile_image_rel_path text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='id_doc_sync_status') THEN
        ALTER TABLE public.users ADD COLUMN id_doc_sync_status text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='profile_image_sync_status') THEN
        ALTER TABLE public.users ADD COLUMN profile_image_sync_status text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='is_seed') THEN
        ALTER TABLE public.users ADD COLUMN is_seed boolean NOT NULL DEFAULT FALSE;
    END IF;
END$$;

-- ---------------------------------------------------------------------------
-- Bootstrap system user (user_id = 0). auth_user_id = 0 is a self-reference;
-- Postgres accepts it on a single INSERT. role_id = 0 has no FK in the base
-- DDL, so inserting before the roles row is fine.
-- 9.3-safe: INSERT ... WHERE NOT EXISTS instead of ON CONFLICT DO NOTHING.
-- ---------------------------------------------------------------------------
INSERT INTO public.users
    (user_id, user_name, password, role_id, auth_user_id, first_name, surname,
     active, affected_ts, is_seed)
SELECT 0, 'berend', 'ACAAAAHHHHABCDBN', 0, 0, 'Berend', 'Howard', TRUE, now(), FALSE
WHERE NOT EXISTS (SELECT 1 FROM public.users WHERE user_id = 0);

-- ---------------------------------------------------------------------------
-- Fixed roles (0..5). role_id = 0 is Developer, a selectable engineering role
-- shown in the WPF role picker alongside Admin/Manager/Supervisor/User. Only
-- System (role_id = 5) is hidden from the picker. Descriptions match the
-- app's StaffAccessRole enum so the UI can map role_id <-> display name.
-- auth_user_id = 1 is satisfied because the bootstrap user was inserted
-- above. Existing rows are left untouched.
-- 9.3-safe: one INSERT per row, each guarded by WHERE NOT EXISTS.
-- ---------------------------------------------------------------------------
INSERT INTO public.roles (role_id, descr, auth_user_id, active, deleted, affected_ts, is_seed)
SELECT 0, 'Developer',  1, TRUE, FALSE, now(), FALSE
WHERE NOT EXISTS (SELECT 1 FROM public.roles WHERE role_id = 0);

INSERT INTO public.roles (role_id, descr, auth_user_id, active, deleted, affected_ts, is_seed)
SELECT 1, 'Admin',      1, TRUE, FALSE, now(), FALSE
WHERE NOT EXISTS (SELECT 1 FROM public.roles WHERE role_id = 1);

INSERT INTO public.roles (role_id, descr, auth_user_id, active, deleted, affected_ts, is_seed)
SELECT 2, 'Manager',    1, TRUE, FALSE, now(), FALSE
WHERE NOT EXISTS (SELECT 1 FROM public.roles WHERE role_id = 2);

INSERT INTO public.roles (role_id, descr, auth_user_id, active, deleted, affected_ts, is_seed)
SELECT 3, 'Supervisor', 1, TRUE, FALSE, now(), FALSE
WHERE NOT EXISTS (SELECT 1 FROM public.roles WHERE role_id = 3);

INSERT INTO public.roles (role_id, descr, auth_user_id, active, deleted, affected_ts, is_seed)
SELECT 4, 'User',       1, TRUE, FALSE, now(), FALSE
WHERE NOT EXISTS (SELECT 1 FROM public.roles WHERE role_id = 4);

INSERT INTO public.roles (role_id, descr, auth_user_id, active, deleted, affected_ts, is_seed)
SELECT 5, 'System',     1, TRUE, FALSE, now(), FALSE
WHERE NOT EXISTS (SELECT 1 FROM public.roles WHERE role_id = 5);

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
