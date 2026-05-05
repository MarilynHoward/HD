-- Initial dummy seed for `public.rpt_vat` (PostgreSQL 9.3+).
--
-- Scope (matches WPF dev seed `SeedRemoteBranchRptVatCore` when `SeedDummyDataOnStartup=true`):
--   - Date range: from (yesterday - 3 calendar months) through yesterday, inclusive.
--   - One row per day per (active channel x active user role x active vat_rate_id).
--   - `net_amount` from `public.rpt_demo_vat_net` (see `2026-05-05_client_rpt_vat_functions.sql`).
--
-- ODBC-oriented: plain `TRUNCATE` + single `INSERT ... SELECT` — no `DO` blocks, no `$$`.
--
-- Target database:
--   Each **branch** PostgreSQL database (facts live per branch). For a branch-dedicated DB use
--   TRUNCATE below. If multiple branches share one database, comment out TRUNCATE and use the
--   DELETE line scoped by `branch_code`.
--
-- Prerequisites:
--   1) `2026-05-05_Vat_report.sql` (or equivalent tables).
--   2) `2026-05-04_client_rpt_daily_sales_functions.sql`
--   3) `2026-05-05_client_rpt_vat_functions.sql`
--
-- Branch parameter: `branch_code` = `current_database()::text` (name the DB to match your app).
--
-- Timezone: "yesterday" is `current_date` of the session; set `TimeZone` / schedule accordingly.

BEGIN;

TRUNCATE TABLE public.rpt_vat;

-- Multi-branch shared database: comment TRUNCATE above and use:
-- DELETE FROM public.rpt_vat WHERE branch_code = current_database()::text;

INSERT INTO public.rpt_vat (report_date, branch_code, channel_code, userrole_code, vat_rate_id, net_amount)
SELECT
    x.report_date,
    x.branch_code,
    x.channel_code,
    x.userrole_code,
    v.vat_rate_id,
    public.rpt_demo_vat_net(
        x.report_date,
        x.branch_code,
        x.channel_code,
        x.userrole_code,
        v.vat_rate_id
    )
FROM (
    SELECT
        (b.start_d + n) AS report_date,
        current_database()::text AS branch_code,
        c.channel_code,
        u.userrole_code
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
) AS x
CROSS JOIN public.vat_rates v
WHERE v.active = TRUE;

COMMIT;
