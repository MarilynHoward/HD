-- =============================================================================================
-- Migration: Clone Stock & Waste Impact for Stock Reports browse (same report as rpt.wastage in Profitability).
-- Date: 2026-05-16
-- PostgreSQL 9.3. ODBC-safe: plain INSERT/UPDATE only (no DO blocks).
-- Local / branch DB — mirror poscontrol catalog rows.
-- =============================================================================================
ROLLBACK;
BEGIN;

UPDATE public.rpt_report_categories SET browse_tile_report_count = 5, modified_ts = now(), auth_user_id = 0 WHERE category_code = 'grp.stock';

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.stock_waste_impact', 'grp.stock', 'Stock & Waste Impact', 'Waste impact on stock holding', 'staff_delete_user_trash', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.stock_waste_impact');

UPDATE public.rpt_reports SET
    category_code = 'grp.stock',
    descr = 'Stock & Waste Impact',
    long_descr = 'Waste impact on stock holding',
    icon_glyph_id = 'staff_delete_user_trash',
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
WHERE report_code = 'rpt.stock_waste_impact';

COMMIT;
