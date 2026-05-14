-- =============================================================================================
-- Migration: Stock variance report catalog fix (POS_CONTROL / catalog source)
-- Date: 2026-05-14
-- PostgreSQL 9.3. ODBC-safe: plain INSERT/UPDATE only (no DO blocks).
-- =============================================================================================
ROLLBACK;
BEGIN;

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.stock_variance', 'grp.stock', 'Variance Report', NULL, 'package', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.stock_variance');

UPDATE public.rpt_reports SET
    category_code = 'grp.procurement',
    descr = 'Delivery Variances',
    dashboard_browse_in_group_sort_order = NULL,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.delivery_variances';

UPDATE public.rpt_reports SET
    category_code = 'grp.stock',
    descr = 'Variance Report',
    dashboard_browse_in_group_sort_order = 4,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.stock_variance';

COMMIT;
