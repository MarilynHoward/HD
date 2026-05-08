-- Initial dummy seed for `public.rpt_daily_sales` (PostgreSQL 9.3+).
--
-- Scope (matches WPF dev seed `SeedRemoteBranchRptDailySalesCore` when `SeedDummyDataOnStartup=true`):
--   - Date range: from (yesterday - 3 calendar months) through yesterday, inclusive.
--   - One row per day per (active channel x active user role) from `rpt_channels` / `rpt_user_roles`.
--   - Sales / transaction counts follow the same formula shape as the app; mixing uses `hashtext`
--     (see `2026-05-04_client_rpt_daily_sales_functions.sql`) — not bit-identical to every .NET run.
--
-- Target database:
--   Run against each **branch** PostgreSQL database (the DB that holds that branch’s facts), not the
--   shared **local property** consolidation DB. On a branch-only DB the table holds one branch; use
--   TRUNCATE below. If multiple branches share one database, comment out TRUNCATE and use the DELETE
--   line instead (scoped by `branch_code`).
--
-- Prerequisites:
--   1) Apply schema (`2026-04-30_local_rpt_lookup.sql` or equivalent).
--   2) Install functions: `2026-05-04_client_rpt_daily_sales_functions.sql`
--
-- Branch parameter: `branch_code` on inserted rows is set from `current_database()` (the database
-- you are connected to). Name the branch database to match `public.branches.branch_code` / your app.
--
-- Usage:
--   psql "YOUR_BRANCH_CONNSTRING" -f 2026-05-04_client_rpt_daily_sales_seed_initial.sql
--
-- Timezone: "yesterday" is `current_date` of the database session. Schedule after local midnight for
-- the intended business calendar, or set `PGTZ` / session `TimeZone` before running.

BEGIN;

-- Full wipe (branch-dedicated database only):
TRUNCATE TABLE public.rpt_daily_sales;

-- Multi-branch shared database: comment TRUNCATE above and use:
-- DELETE FROM public.rpt_daily_sales WHERE branch_code = current_database()::text;

INSERT INTO public.rpt_daily_sales (report_date, branch_code, channel_code, userrole_code, sales, nr_transactions)
SELECT
    x.report_date,
    x.branch_code,
    x.channel_code,
    x.userrole_code,
    x.sales,
    public.rpt_demo_daily_transactions(
        x.sales,
        x.report_date,
        x.branch_code,
        x.channel_code,
        x.userrole_code
    )::integer
FROM (
    SELECT
        (b.start_d + n) AS report_date,
        current_database()::text AS branch_code,
        c.channel_code,
        u.userrole_code,
        public.rpt_demo_daily_sales(
            (b.start_d + n),
            current_database()::text,
            c.channel_code,
            u.userrole_code
        ) AS sales
    FROM (
        SELECT
            (((current_date - interval '1 day')::date - interval '3 months')::date) AS start_d,
            ((current_date - interval '1 day')::date) AS end_d
    ) AS b
    CROSS JOIN generate_series(0, (b.end_d - b.start_d), 1) AS s(n)
    CROSS JOIN public.rpt_channels c
    CROSS JOIN public.rpt_user_roles u
    WHERE c.active = TRUE
      AND u.active = TRUE
) AS x;

COMMIT;
