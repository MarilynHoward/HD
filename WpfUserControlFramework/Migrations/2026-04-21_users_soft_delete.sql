-- =============================================================================================
-- Migration: add soft-delete columns to public.users
-- Date:      2026-04-21
-- Author:    RestaurantPosWpf / Staff and Access
-- PostgreSQL target: 9.3 (client production). This script avoids 9.5+/9.6+ syntax:
--   * NO `ADD COLUMN IF NOT EXISTS`   (9.6+) -> DO block probing information_schema.columns
--   * NO `CREATE INDEX IF NOT EXISTS` (9.5+) -> DO block probing pg_indexes
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
--   1. Add column public.users.deleted         BOOLEAN NOT NULL DEFAULT FALSE   (if missing)
--   2. Add column public.users.deleted_ts      TIMESTAMPTZ                      (if missing)
--   3. Add column public.users.deleted_user_id INTEGER                          (if missing)
--   4. Create partial index ix_users_not_deleted ON public.users(user_id) WHERE deleted = FALSE
--      (if missing)
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

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='public' AND table_name='users' AND column_name='deleted'
    ) THEN
        ALTER TABLE public.users ADD COLUMN deleted BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='public' AND table_name='users' AND column_name='deleted_ts'
    ) THEN
        ALTER TABLE public.users ADD COLUMN deleted_ts TIMESTAMP WITH TIME ZONE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='public' AND table_name='users' AND column_name='deleted_user_id'
    ) THEN
        ALTER TABLE public.users ADD COLUMN deleted_user_id INTEGER;
    END IF;
END$$;

COMMENT ON COLUMN public.users.deleted IS
    'Soft-delete flag. Live reads filter WHERE deleted = FALSE.';
COMMENT ON COLUMN public.users.deleted_ts IS
    'UTC timestamp of soft delete; NULL while the account is live.';
COMMENT ON COLUMN public.users.deleted_user_id IS
    'users.user_id of the operator who performed the soft delete.';

-- Partial index to keep "active roster" reads fast without excluding deleted rows from history.
-- 9.3-safe: emulate CREATE INDEX IF NOT EXISTS via DO block.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname='public' AND indexname='ix_users_not_deleted'
    ) THEN
        CREATE INDEX ix_users_not_deleted
            ON public.users (user_id)
            WHERE deleted = FALSE;
    END IF;
END$$;

COMMIT;

-- Related migrations already applied:
--   2026-04-21_staff_access_init.sql   (staff profile columns, fixed roles, bootstrap user)
--   2026-04-21_audit_trail_init.sql    (public.audit_trail + public.database_update_phase — client schema)
