-- =============================================================================================
-- Migration: add soft-delete columns to public.users
-- Date:      2026-04-21
-- Author:    RestaurantPosWpf / Staff and Access
--
-- Background
--   public.roles already carries deleted / deleted_ts / deleted_user_id (see BH_User_Role_Tables.sql).
--   public.users did NOT, and the app was hard-deleting rows via
--       DELETE FROM public.users WHERE user_id = <id>
--   With this migration the client can keep a full historical record of staff accounts: "who was
--   terminated, by whom, and when", and Operations references (scheduled shifts, table assignments)
--   can continue to dereference the original user_id without dangling FKs.
--
-- What this script does
--   1. ALTER public.users ADD COLUMN IF NOT EXISTS deleted BOOLEAN NOT NULL DEFAULT FALSE
--   2. ALTER public.users ADD COLUMN IF NOT EXISTS deleted_ts TIMESTAMPTZ
--   3. ALTER public.users ADD COLUMN IF NOT EXISTS deleted_user_id INTEGER
--   4. CREATE INDEX IF NOT EXISTS ix_users_not_deleted ON public.users(user_id) WHERE deleted = FALSE
--
-- Idempotent: safe to rerun.
-- Rollback:
--   ALTER TABLE public.users DROP COLUMN IF EXISTS deleted;
--   ALTER TABLE public.users DROP COLUMN IF EXISTS deleted_ts;
--   ALTER TABLE public.users DROP COLUMN IF EXISTS deleted_user_id;
--   DROP INDEX IF EXISTS public.ix_users_not_deleted;
-- =============================================================================================

ROLLBACK;
BEGIN;

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS deleted BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS deleted_ts TIMESTAMP WITH TIME ZONE;

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS deleted_user_id INTEGER;

COMMENT ON COLUMN public.users.deleted IS
    'Soft-delete flag. Live reads filter WHERE deleted = FALSE.';
COMMENT ON COLUMN public.users.deleted_ts IS
    'UTC timestamp of soft delete; NULL while the account is live.';
COMMENT ON COLUMN public.users.deleted_user_id IS
    'users.user_id of the operator who performed the soft delete.';

-- Partial index to keep "active roster" reads fast without excluding deleted rows from history.
CREATE INDEX IF NOT EXISTS ix_users_not_deleted
    ON public.users (user_id)
    WHERE deleted = FALSE;

COMMIT;

-- Related migrations already applied:
--   2026-04-21_staff_access_init.sql   (staff profile columns, fixed roles, bootstrap user)
--   2026-04-21_audit_trail_init.sql    (public.audit_trail + public.database_update_phase — client schema)
