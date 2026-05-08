-- Client helper functions for dummy `public.rpt_daily_sales` data (PostgreSQL 9.3+).
-- Mirrors the intent of `AppStatus.ComputeDemoDailySales` / `ComputeDemoDailyTransactions`
-- (WpfUserControlFramework/Utils/AppStatus.cs) using stable PostgreSQL hashing (`hashtext`)
-- instead of .NET `StringComparer.Ordinal.GetHashCode` (not portable / not stable across .NET runs).
--
-- Install once per database before running:
--   2026-05-04_client_rpt_daily_sales_seed_initial.sql
--   2026-05-04_client_rpt_daily_sales_seed_yesterday.sql
--
-- Idempotent: CREATE OR REPLACE FUNCTION.

BEGIN;

CREATE OR REPLACE FUNCTION public.rpt_demo_mix_sales(
    p_branch_code text,
    p_channel_code text,
    p_userrole_code text
) RETURNS bigint
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT ((hashtext($1)::bigint # hashtext($2 || '|' || $3)::bigint) & 2147483647::bigint);
$$;

CREATE OR REPLACE FUNCTION public.rpt_demo_mix_transactions(
    p_branch_code text,
    p_report_date date,
    p_channel_code text,
    p_userrole_code text
) RETURNS bigint
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT (hashtext($1 || to_char($2, 'YYYY-MM-DD') || $3 || $4)::bigint & 2147483647::bigint);
$$;

CREATE OR REPLACE FUNCTION public.rpt_demo_daily_sales(
    p_report_date date,
    p_branch_code text,
    p_channel_code text,
    p_userrole_code text
) RETURNS numeric
LANGUAGE plpgsql
IMMUTABLE
AS $$
DECLARE
    mix bigint;
    day_of_year int;
    dow int;
    weekend double precision;
    wave double precision;
    micro double precision;
    channel_boost double precision;
    role_boost double precision;
    base_amt numeric;
    factor double precision;
BEGIN
    mix := public.rpt_demo_mix_sales(p_branch_code, p_channel_code, p_userrole_code);
    day_of_year := extract(doy FROM p_report_date)::int;
    dow := extract(dow FROM p_report_date)::int;

    IF dow = 0 OR dow = 6 THEN
        weekend := 1.28;
    ELSE
        weekend := 1.0;
    END IF;

    wave := 1.0 + 0.11 * sin(day_of_year * (pi() * 2.0 / 366.0));
    micro := 1.0 + 0.06 * sin((day_of_year + (mix % 17)) * (pi() * 2.0 / 17.0));
    channel_boost := 1.0 + (char_length(p_channel_code) % 6) * 0.035;
    role_boost := 1.0 + (char_length(p_userrole_code) % 5) * 0.042;
    base_amt := 650::numeric + (mix % 520);
    factor := weekend * wave * micro * channel_boost * role_boost;

    RETURN round(base_amt * factor::numeric, 2);
END;
$$;

CREATE OR REPLACE FUNCTION public.rpt_demo_daily_transactions(
    p_sales numeric,
    p_report_date date,
    p_branch_code text,
    p_channel_code text,
    p_userrole_code text
) RETURNS integer
LANGUAGE plpgsql
IMMUTABLE
AS $$
DECLARE
    mix bigint;
    base_tx int;
BEGIN
    mix := public.rpt_demo_mix_transactions(p_branch_code, p_report_date, p_channel_code, p_userrole_code);
    base_tx := floor(p_sales / 42.0)::int;
    RETURN greatest(4, base_tx + (mix % 18)::int);
END;
$$;

COMMIT;
