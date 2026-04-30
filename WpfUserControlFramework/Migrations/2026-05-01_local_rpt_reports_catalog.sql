-- =============================================================================================
-- Migration: RPT report catalog + access log on branch/local DB
-- Date: 2026-05-01
-- PostgreSQL 9.3. Same catalog DDL/seeds as POS_CONTROL plus local-only access log.
-- =============================================================================================
ROLLBACK;
BEGIN;

CREATE TABLE IF NOT EXISTS public.rpt_report_categories
(
    category_code   text COLLATE pg_catalog."default" NOT NULL,
    descr           text COLLATE pg_catalog."default" NOT NULL,
    active          boolean NOT NULL DEFAULT true,
    auth_user_id    integer NOT NULL,
    created_ts      timestamp without time zone NOT NULL DEFAULT now(),
    modified_ts     timestamp without time zone NOT NULL DEFAULT now(),
    CONSTRAINT rpt_report_categories_pk PRIMARY KEY (category_code)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.rpt_report_categories OWNER TO postgres;

CREATE TABLE IF NOT EXISTS public.rpt_reports
(
    report_code     text COLLATE pg_catalog."default" NOT NULL,
    category_code   text COLLATE pg_catalog."default" NOT NULL,
    descr           text COLLATE pg_catalog."default" NOT NULL,
    long_descr      text COLLATE pg_catalog."default" NULL,
    icon_glyph_id   text COLLATE pg_catalog."default" NULL,
    active          boolean NOT NULL DEFAULT true,
    auth_user_id    integer NOT NULL,
    created_ts      timestamp without time zone NOT NULL DEFAULT now(),
    modified_ts     timestamp without time zone NOT NULL DEFAULT now(),
    CONSTRAINT rpt_reports_pk PRIMARY KEY (report_code),
    CONSTRAINT rpt_reports_fk_category FOREIGN KEY (category_code)
        REFERENCES public.rpt_report_categories (category_code)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.rpt_reports OWNER TO postgres;

CREATE TABLE IF NOT EXISTS public.rpt_report_access_log
(
    access_id       serial NOT NULL,
    user_id         integer NOT NULL,
    report_code     text COLLATE pg_catalog."default" NOT NULL,
    accessed_ts     timestamp without time zone NOT NULL DEFAULT now(),
    auth_user_id    integer NOT NULL,
    CONSTRAINT rpt_report_access_log_pk PRIMARY KEY (access_id)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.rpt_report_access_log OWNER TO postgres;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public' AND indexname = 'ix_rpt_report_access_log_user_accessed'
    ) THEN
        CREATE INDEX ix_rpt_report_access_log_user_accessed
            ON public.rpt_report_access_log (user_id, accessed_ts DESC);
    END IF;
END$$;

-- Categories
INSERT INTO public.rpt_report_categories (category_code, descr, auth_user_id)
SELECT 'grp.sales', 'Sales Reports', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_report_categories WHERE category_code = 'grp.sales');
INSERT INTO public.rpt_report_categories (category_code, descr, auth_user_id)
SELECT 'grp.financial', 'Financial', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_report_categories WHERE category_code = 'grp.financial');
INSERT INTO public.rpt_report_categories (category_code, descr, auth_user_id)
SELECT 'grp.stock', 'Stock Reports', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_report_categories WHERE category_code = 'grp.stock');
INSERT INTO public.rpt_report_categories (category_code, descr, auth_user_id)
SELECT 'grp.ops', 'Operational Control', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_report_categories WHERE category_code = 'grp.ops');
INSERT INTO public.rpt_report_categories (category_code, descr, auth_user_id)
SELECT 'grp.procurement', 'Supplier & Procurement', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_report_categories WHERE category_code = 'grp.procurement');
INSERT INTO public.rpt_report_categories (category_code, descr, auth_user_id)
SELECT 'grp.profitability', 'Profitability Reports', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_report_categories WHERE category_code = 'grp.profitability');
INSERT INTO public.rpt_report_categories (category_code, descr, auth_user_id)
SELECT 'grp.staff_access', 'Staff & Access Reports', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_report_categories WHERE category_code = 'grp.staff_access');

-- Reports
INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.daily_sales', 'grp.sales', 'Daily Sales Summary', NULL, 'bar_chart', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.daily_sales');
INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.revenue', 'grp.sales', 'Revenue', NULL, 'pie_chart', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.revenue');
INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.vat_summary', 'grp.financial', 'VAT Summary', NULL, 'staff_documents_tab', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.vat_summary');
INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.wastage', 'grp.stock', 'Wastage Report', NULL, 'staff_delete_user_trash', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.wastage');
INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.voids', 'grp.ops', 'Voids Report', NULL, 'alert_circle', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.voids');
INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.high_value_voids', 'grp.ops', 'High-Value Voids', NULL, 'alert_circle', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.high_value_voids');
INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.low_stock', 'grp.stock', 'Low Stock Items', NULL, 'package', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.low_stock');
INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.delivery_variances', 'grp.procurement', 'Delivery Variances', NULL, 'trend_down', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.delivery_variances');
INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.till_balance', 'grp.ops', 'Till Not Balanced', NULL, 'coins', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.till_balance');

COMMIT;
