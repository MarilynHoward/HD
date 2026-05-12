-- =============================================================================================
-- Migration: Sales browse subgroup — five reports (POS_CONTROL / catalog source)
-- Date: 2026-05-12
-- PostgreSQL 9.3. ODBC-safe: plain INSERT/UPDATE only (no DO blocks).
-- =============================================================================================
ROLLBACK;
BEGIN;

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.sales_by_category', 'grp.sales', 'Sales By Category', NULL, 'bar_chart', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.sales_by_category');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.sales_by_product', 'grp.sales', 'Sales By Product', NULL, 'bar_chart', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.sales_by_product');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.sales_by_order_type', 'grp.sales', 'Sales By Order Type', NULL, 'bar_chart', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.sales_by_order_type');

UPDATE public.rpt_reports SET
    category_code = 'grp.profitability',
    dashboard_browse_in_group_sort_order = NULL,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.revenue';

UPDATE public.rpt_reports SET
    category_code = 'grp.sales',
    dashboard_browse_in_group_sort_order = 2,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.vat_summary';

UPDATE public.rpt_reports SET dashboard_browse_in_group_sort_order = 1, modified_ts = now(), auth_user_id = 0 WHERE report_code = 'rpt.daily_sales';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#DBEAFE',
    ui_icon_foreground_hex = '#1D4ED8',
    ui_hover_border_hex = '#3B82F6',
    ui_hover_surface_hex = '#E6F3FF',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL,
    ui_badge_icon_foreground_hex = NULL,
    ui_badge_hover_border_hex = NULL,
    ui_badge_hover_surface_hex = NULL,
    ui_badge_chevron_hot_hex = NULL,
    dashboard_browse_in_group_sort_order = 3,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.sales_by_category';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#DBEAFE',
    ui_icon_foreground_hex = '#1D4ED8',
    ui_hover_border_hex = '#3B82F6',
    ui_hover_surface_hex = '#E6F3FF',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL,
    ui_badge_icon_foreground_hex = NULL,
    ui_badge_hover_border_hex = NULL,
    ui_badge_hover_surface_hex = NULL,
    ui_badge_chevron_hot_hex = NULL,
    dashboard_browse_in_group_sort_order = 4,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.sales_by_product';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#DBEAFE',
    ui_icon_foreground_hex = '#1D4ED8',
    ui_hover_border_hex = '#3B82F6',
    ui_hover_surface_hex = '#E6F3FF',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL,
    ui_badge_icon_foreground_hex = NULL,
    ui_badge_hover_border_hex = NULL,
    ui_badge_hover_surface_hex = NULL,
    ui_badge_chevron_hot_hex = NULL,
    dashboard_browse_in_group_sort_order = 5,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.sales_by_order_type';

COMMIT;
