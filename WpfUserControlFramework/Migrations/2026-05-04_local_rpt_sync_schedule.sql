-- =============================================================================================
-- Migration: Reporting sync audit + peer replication state (branch/local DB)
-- Date: 2026-05-04
-- PostgreSQL 9.3. DDL only — run on each branch database.
--
-- rpt_sync_run_log: one row per sync attempt from the designated terminal.
-- rpt_replication_peer_state: incremental cursor per peer branch for rpt_daily_sales pulls.
-- =============================================================================================
ROLLBACK;
BEGIN;

CREATE TABLE IF NOT EXISTS public.rpt_sync_run_log
(
    run_id              serial NOT NULL,
    machine_name        text COLLATE pg_catalog."default" NOT NULL,
    trigger             text COLLATE pg_catalog."default" NOT NULL,
    sync_date           date NOT NULL,
    started_ts          timestamp without time zone NOT NULL DEFAULT now(),
    finished_ts         timestamp without time zone NULL,
    success             boolean NULL,
    error_message       text COLLATE pg_catalog."default" NULL,
    CONSTRAINT rpt_sync_run_log_pk PRIMARY KEY (run_id)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.rpt_sync_run_log OWNER TO postgres;

CREATE TABLE IF NOT EXISTS public.rpt_replication_peer_state
(
    source_branch_code              text COLLATE pg_catalog."default" NOT NULL,
    last_success_sync_utc           timestamp without time zone NULL,
    last_remote_max_modified_ts     timestamp without time zone NULL,
    last_full_reconcile_utc         timestamp without time zone NULL,
    CONSTRAINT rpt_replication_peer_state_pk PRIMARY KEY (source_branch_code)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.rpt_replication_peer_state OWNER TO postgres;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public' AND indexname = 'ix_rpt_sync_run_log_sync_date_started'
    ) THEN
        CREATE INDEX ix_rpt_sync_run_log_sync_date_started
            ON public.rpt_sync_run_log (sync_date, started_ts DESC);
    END IF;
END$$;

COMMIT;
