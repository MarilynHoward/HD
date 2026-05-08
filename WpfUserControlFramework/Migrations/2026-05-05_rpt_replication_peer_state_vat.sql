-- =============================================================================================
-- Migration: VAT peer replication cursors on rpt_replication_peer_state (branch/local DB)
-- Date: 2026-05-05
-- PostgreSQL 9.3. DDL only — run on each branch database.
--
-- ODBC deployment: plain ALTER TABLE only (no DO $$ blocks — incompatible with client ODBC runner).
-- Apply once per DB; re-run fails if columns already exist (handle at migrator or run manually).
--
-- Independent incremental / full-reconcile cursors for public.rpt_vat peer sync (do not share
-- last_remote_max_modified_ts / last_full_reconcile_utc used by rpt_daily_sales).
-- =============================================================================================
ROLLBACK;
BEGIN;

ALTER TABLE public.rpt_replication_peer_state
    ADD COLUMN vat_last_remote_max_modified_ts timestamp without time zone NULL;

ALTER TABLE public.rpt_replication_peer_state
    ADD COLUMN vat_last_full_reconcile_utc timestamp without time zone NULL;

COMMIT;
