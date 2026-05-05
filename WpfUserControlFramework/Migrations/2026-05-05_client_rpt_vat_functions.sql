-- Client helper for dummy `public.rpt_vat.net_amount` (PostgreSQL 9.3+).
-- Mirrors `AppStatus.ComputeDemoVatNet` (WpfUserControlFramework/Utils/AppStatus.cs): shares the same
-- base as Daily Sales via `public.rpt_demo_daily_sales`, then applies band share + jitter from
-- `hashtext` / day-of-year (stable in PostgreSQL; not bit-identical to .NET `GetHashCode`).
--
-- ODBC-oriented: **LANGUAGE sql** with a **single-quoted** function body (no `$$` dollar quotes,
-- no `DO` blocks). Submit as **one statement** if your ODBC splitter breaks on semicolons inside
-- quoted strings only — this body contains no semicolons.
--
-- Prerequisites (same database):
--   1) Schema: `2026-05-05_Vat_report.sql` (or equivalent: `rpt_vat`, `vat_rates`).
--   2) Daily Sales demo functions: `2026-05-04_client_rpt_daily_sales_functions.sql`
--      (`public.rpt_demo_daily_sales` must exist).
--
-- Install once per branch database before:
--   `2026-05-05_client_rpt_vat_seed_initial.sql`
--   `2026-05-05_client_rpt_vat_seed_yesterday.sql`

BEGIN;

CREATE OR REPLACE FUNCTION public.rpt_demo_vat_net(
    p_report_date date,
    p_branch_code text,
    p_channel_code text,
    p_userrole_code text,
    p_vat_rate_id integer
) RETURNS numeric
LANGUAGE sql
IMMUTABLE
AS 'SELECT round(
    public.rpt_demo_daily_sales($1, $2, $3, $4)
    * (CASE $5 WHEN 1 THEN 0.62 WHEN 2 THEN 0.25 WHEN 3 THEN 0.13 ELSE 0.34 END)
    * (
        0.92::numeric
        + (
            (
                ((hashtext($2 || ''|'' || $5::text) # (extract(doy FROM $1)::integer)) & 255)
                % 17
            )::numeric
            * 0.004
        )
    ),
    2)';

COMMENT ON FUNCTION public.rpt_demo_vat_net(date, text, text, text, integer) IS
    'Demo net_amount per VAT band for rpt_vat; requires rpt_demo_daily_sales.';

COMMIT;
