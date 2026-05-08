-- Daily dummy upsert for `public.rpt_vat` — previous calendar day only (PostgreSQL 9.3+).
--
-- Updates / inserts all (active channel x active user role x configured demo tax) rows for
-- `report_date = current_date - 1 day`, using PG 9.3–safe `UPDATE ... FROM` then
-- `INSERT ... WHERE NOT EXISTS` (same pattern as `Sql.UpsertLocalRptVatRow`).
--
-- ODBC-oriented: **no** `CREATE FUNCTION`, no `DO` blocks, no `$$`. Two statements (UPDATE then
-- INSERT); run each as a single batch if your driver requires one statement per execute.
--
-- Target database: each **branch** database (not a naive shared consolidation DB without scoping).
--
-- Prerequisites:
--   `2026-05-04_client_rpt_daily_sales_functions.sql`
--   `2026-05-05_client_rpt_vat_functions.sql`
--
-- Branch parameter: `branch_code` = `current_database()::text`.
--
-- ─────────────────────────────────────────────────────────────────────────────
-- DEMO TAX IDS — edit the three integers in BOTH `params` CTEs below (keep them identical).
-- They must match rows in `public.taxes` and match `2026-05-05_client_rpt_vat_seed_initial.sql`.
-- ─────────────────────────────────────────────────────────────────────────────
--
-- Scheduling: call these two statements from your job runner (pgAgent, application cron, etc.).
-- Example pg_cron (if allowed) — use separate schedule entries or a small SQL-only wrapper your
-- platform supports; **do not** embed `$$` in cron strings if ODBC tools parse them badly.
--
-- Timezone: `current_date` follows session time zone.

BEGIN;

WITH params AS (
    SELECT
        1 AS demo_tax_id_1,
        2 AS demo_tax_id_2,
        3 AS demo_tax_id_3
),
d0 AS (
    SELECT ((current_date - interval '1 day')::date) AS report_date
),
dims AS (
    SELECT c.channel_code, u.userrole_code, v.tax_id AS vat_rate_id,
           p.demo_tax_id_1, p.demo_tax_id_2, p.demo_tax_id_3
    FROM params p
    CROSS JOIN public.rpt_channels c
    CROSS JOIN public.rpt_user_roles u
    INNER JOIN public.taxes v
        ON v.tax_id IN (p.demo_tax_id_1, p.demo_tax_id_2, p.demo_tax_id_3)
    WHERE c.active = TRUE
      AND u.active = TRUE
      AND v.active = TRUE
      AND COALESCE(v.deleted, FALSE) = FALSE
),
vals AS (
    SELECT
        d0.report_date,
        current_database()::text AS branch_code,
        d.channel_code,
        d.userrole_code,
        d.vat_rate_id,
        public.rpt_demo_vat_net(
            d0.report_date,
            current_database()::text,
            d.channel_code,
            d.userrole_code,
            d.vat_rate_id,
            d.demo_tax_id_1,
            d.demo_tax_id_2,
            d.demo_tax_id_3
        ) AS net_amount
    FROM d0
    CROSS JOIN dims d
)
UPDATE public.rpt_vat AS t
SET
    net_amount = v.net_amount,
    modified_ts = now()
FROM vals v
WHERE t.report_date = v.report_date
  AND t.branch_code = v.branch_code
  AND t.channel_code = v.channel_code
  AND t.userrole_code = v.userrole_code
  AND t.vat_rate_id = v.vat_rate_id;

WITH params AS (
    SELECT
        1 AS demo_tax_id_1,
        2 AS demo_tax_id_2,
        3 AS demo_tax_id_3
),
d0 AS (
    SELECT ((current_date - interval '1 day')::date) AS report_date
),
dims AS (
    SELECT c.channel_code, u.userrole_code, v.tax_id AS vat_rate_id,
           p.demo_tax_id_1, p.demo_tax_id_2, p.demo_tax_id_3
    FROM params p
    CROSS JOIN public.rpt_channels c
    CROSS JOIN public.rpt_user_roles u
    INNER JOIN public.taxes v
        ON v.tax_id IN (p.demo_tax_id_1, p.demo_tax_id_2, p.demo_tax_id_3)
    WHERE c.active = TRUE
      AND u.active = TRUE
      AND v.active = TRUE
      AND COALESCE(v.deleted, FALSE) = FALSE
),
vals AS (
    SELECT
        d0.report_date,
        current_database()::text AS branch_code,
        d.channel_code,
        d.userrole_code,
        d.vat_rate_id,
        public.rpt_demo_vat_net(
            d0.report_date,
            current_database()::text,
            d.channel_code,
            d.userrole_code,
            d.vat_rate_id,
            d.demo_tax_id_1,
            d.demo_tax_id_2,
            d.demo_tax_id_3
        ) AS net_amount
    FROM d0
    CROSS JOIN dims d
)
INSERT INTO public.rpt_vat (
    report_date,
    branch_code,
    channel_code,
    userrole_code,
    vat_rate_id,
    net_amount,
    modified_ts
)
SELECT
    v.report_date,
    v.branch_code,
    v.channel_code,
    v.userrole_code,
    v.vat_rate_id,
    v.net_amount,
    now()
FROM vals v
WHERE NOT EXISTS (
    SELECT 1
    FROM public.rpt_vat r
    WHERE r.report_date = v.report_date
      AND r.branch_code = v.branch_code
      AND r.channel_code = v.channel_code
      AND r.userrole_code = v.userrole_code
      AND r.vat_rate_id = v.vat_rate_id
);

COMMIT;
