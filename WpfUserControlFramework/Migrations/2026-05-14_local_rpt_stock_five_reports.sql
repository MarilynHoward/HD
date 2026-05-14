-- =============================================================================================
-- Migration: Stock browse subgroup — five reports (local / branch DB)
-- Date: 2026-05-14
-- PostgreSQL 9.3. ODBC-safe: plain INSERT/UPDATE only (no DO blocks).
-- Run after dashboard UI columns exist (2026-05-02_local_rpt_dashboard_ui.sql).
-- =============================================================================================
ROLLBACK;
BEGIN;

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.stock_movement', 'grp.stock', 'Stock Movement Report', NULL, 'package', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.stock_movement');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.reorder', 'grp.stock', 'Reorder Report', NULL, 'package', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.reorder');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.stock_variance', 'grp.stock', 'Variance Report', NULL, 'package', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.stock_variance');

UPDATE public.rpt_reports SET
    category_code = 'grp.stock',
    descr = 'Stock on Hand',
    dashboard_browse_in_group_sort_order = 1,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.low_stock';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#CCFBF1',
    ui_icon_foreground_hex = '#0F766E',
    ui_hover_border_hex = '#14B8A6',
    ui_hover_surface_hex = '#E8FFFA',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL,
    ui_badge_icon_foreground_hex = NULL,
    ui_badge_hover_border_hex = NULL,
    ui_badge_hover_surface_hex = NULL,
    ui_badge_chevron_hot_hex = NULL,
    dashboard_browse_in_group_sort_order = 2,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.stock_movement';

UPDATE public.rpt_reports SET
    dashboard_browse_in_group_sort_order = 3,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.wastage';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#CCFBF1',
    ui_icon_foreground_hex = '#0F766E',
    ui_hover_border_hex = '#14B8A6',
    ui_hover_surface_hex = '#E8FFFA',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL,
    ui_badge_icon_foreground_hex = NULL,
    ui_badge_hover_border_hex = NULL,
    ui_badge_hover_surface_hex = NULL,
    ui_badge_chevron_hot_hex = NULL,
    dashboard_browse_in_group_sort_order = 4,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.stock_variance';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#CCFBF1',
    ui_icon_foreground_hex = '#0F766E',
    ui_hover_border_hex = '#14B8A6',
    ui_hover_surface_hex = '#E8FFFA',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL,
    ui_badge_icon_foreground_hex = NULL,
    ui_badge_hover_border_hex = NULL,
    ui_badge_hover_surface_hex = NULL,
    ui_badge_chevron_hot_hex = NULL,
    dashboard_browse_in_group_sort_order = 5,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.reorder';

COMMIT;
