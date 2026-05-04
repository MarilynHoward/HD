-- Daily dummy update for `public.rpt_daily_sales` — previous calendar day only (PostgreSQL 9.3+).
--
-- Defines `public.run_daily_sales_upsert()` for in-database scheduling (e.g. pg_cron). Upserts all
-- (active channel x active user role) rows for `report_date = current_date - 1 day`. Uses PostgreSQL
-- 9.3–safe `UPDATE` then `INSERT ... WHERE NOT EXISTS` (same pattern as
-- `Sql.UpsertLocalRptDailySalesRow` in WpfUserControlFramework/Utils/Sql.cs).
--
-- Target database:
--   Each **branch** database. Do **not** use this against the shared local consolidation database
--   with a naive TRUNCATE; for local-only slices use a branch-scoped variant.
--
-- Prerequisites:
--   `2026-05-04_client_rpt_daily_sales_functions.sql`
--
-- Branch parameter: `branch_code` is `current_database()::text` (name the database to match
-- `public.branches.branch_code` / your app).
--
-- Scheduling (pg_cron — extension must be installed and allowed by your platform; not in core PG):
--   CREATE EXTENSION IF NOT EXISTS pg_cron;
--   SELECT cron.schedule(
--       'daily-sales-upsert',
--       '0 1 * * *',
--       $$SELECT public.run_daily_sales_upsert();$$
--   );
--
-- Timezone: `current_date` uses the database/session time zone. Set `TimeZone` for the cron session
-- or run the job at an offset so "yesterday" matches your business day.
--
-- Manual run:
--   SELECT public.run_daily_sales_upsert();

/*

CREATE EXTENSION IF NOT EXISTS pg_cron;
SELECT cron.schedule(
    'daily-sales-upsert',
    '30 6 * * *',
    $$SELECT public.run_daily_sales_upsert();$$
);

*/

BEGIN;

CREATE OR REPLACE FUNCTION public.run_daily_sales_upsert()
RETURNS void
LANGUAGE plpgsql
VOLATILE
AS $$
BEGIN
    WITH d0 AS (
        SELECT ((current_date - interval '1 day')::date) AS report_date
    ),
    dims AS (
        SELECT c.channel_code, u.userrole_code
        FROM public.rpt_channels c
        CROSS JOIN public.rpt_user_roles u
        WHERE c.active = TRUE
          AND u.active = TRUE
    ),
    base AS (
        SELECT
            d0.report_date,
            current_database()::text AS branch_code,
            d.channel_code,
            d.userrole_code,
            public.rpt_demo_daily_sales(
                d0.report_date,
                current_database()::text,
                d.channel_code,
                d.userrole_code
            ) AS sales
        FROM d0
        CROSS JOIN dims d
    ),
    vals AS (
        SELECT
            b.report_date,
            b.branch_code,
            b.channel_code,
            b.userrole_code,
            b.sales,
            public.rpt_demo_daily_transactions(
                b.sales,
                b.report_date,
                b.branch_code,
                b.channel_code,
                b.userrole_code
            )::integer AS nr_transactions
        FROM base b
    )
    UPDATE public.rpt_daily_sales AS t
    SET
        sales = v.sales,
        nr_transactions = v.nr_transactions,
        modified_ts = now()
    FROM vals v
    WHERE t.report_date = v.report_date
      AND t.branch_code = v.branch_code
      AND t.channel_code = v.channel_code
      AND t.userrole_code = v.userrole_code;

    WITH d0 AS (
        SELECT ((current_date - interval '1 day')::date) AS report_date
    ),
    dims AS (
        SELECT c.channel_code, u.userrole_code
        FROM public.rpt_channels c
        CROSS JOIN public.rpt_user_roles u
        WHERE c.active = TRUE
          AND u.active = TRUE
    ),
    base AS (
        SELECT
            d0.report_date,
            current_database()::text AS branch_code,
            d.channel_code,
            d.userrole_code,
            public.rpt_demo_daily_sales(
                d0.report_date,
                current_database()::text,
                d.channel_code,
                d.userrole_code
            ) AS sales
        FROM d0
        CROSS JOIN dims d
    ),
    vals AS (
        SELECT
            b.report_date,
            b.branch_code,
            b.channel_code,
            b.userrole_code,
            b.sales,
            public.rpt_demo_daily_transactions(
                b.sales,
                b.report_date,
                b.branch_code,
                b.channel_code,
                b.userrole_code
            )::integer AS nr_transactions
        FROM base b
    )
    INSERT INTO public.rpt_daily_sales (
        report_date,
        branch_code,
        channel_code,
        userrole_code,
        sales,
        nr_transactions,
        modified_ts
    )
    SELECT
        v.report_date,
        v.branch_code,
        v.channel_code,
        v.userrole_code,
        v.sales,
        v.nr_transactions,
        now()
    FROM vals v
    WHERE NOT EXISTS (
        SELECT 1
        FROM public.rpt_daily_sales r
        WHERE r.report_date = v.report_date
          AND r.branch_code = v.branch_code
          AND r.channel_code = v.channel_code
          AND r.userrole_code = v.userrole_code
    );
END;
$$;

COMMENT ON FUNCTION public.run_daily_sales_upsert() IS
    'Temp dummy seed: upsert yesterday rpt_daily_sales for current_database() as branch_code; requires rpt_demo_* functions.';

COMMIT;
