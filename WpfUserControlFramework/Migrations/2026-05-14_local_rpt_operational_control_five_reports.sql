-- =============================================================================================
-- Migration: Operational Control browse subgroup — five reports (local / branch DB)
-- Date: 2026-05-14
-- PostgreSQL 9.3. ODBC-safe: plain INSERT/UPDATE only (no DO blocks).
-- Run after dashboard UI columns exist (2026-05-02_local_rpt_dashboard_ui.sql).
-- =============================================================================================
ROLLBACK;
BEGIN;

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.refunds', 'grp.ops', 'Refunds Report', NULL, 'alert_circle', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.refunds');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.discount_audit', 'grp.ops', 'Discounts Audit', NULL, 'alert_circle', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.discount_audit');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.order_edit_audit', 'grp.ops', 'Order Edit Audit', NULL, 'alert_circle', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.order_edit_audit');

UPDATE public.rpt_reports SET
    category_code = 'grp.ops',
    dashboard_browse_in_group_sort_order = NULL,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.high_value_voids';

UPDATE public.rpt_reports SET
    category_code = 'grp.ops',
    dashboard_browse_in_group_sort_order = 1,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.voids';

UPDATE public.rpt_reports SET
    category_code = 'grp.ops',
    ui_icon_backdrop_hex = '#FEE2E2',
    ui_icon_foreground_hex = '#B91C1C',
    ui_hover_border_hex = '#EF4444',
    ui_hover_surface_hex = '#FEF2F2',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL,
    ui_badge_icon_foreground_hex = NULL,
    ui_badge_hover_border_hex = NULL,
    ui_badge_hover_surface_hex = NULL,
    ui_badge_chevron_hot_hex = NULL,
    dashboard_browse_in_group_sort_order = 2,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.refunds';

UPDATE public.rpt_reports SET
    category_code = 'grp.ops',
    ui_icon_backdrop_hex = '#FEE2E2',
    ui_icon_foreground_hex = '#B91C1C',
    ui_hover_border_hex = '#EF4444',
    ui_hover_surface_hex = '#FEF2F2',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL,
    ui_badge_icon_foreground_hex = NULL,
    ui_badge_hover_border_hex = NULL,
    ui_badge_hover_surface_hex = NULL,
    ui_badge_chevron_hot_hex = NULL,
    dashboard_browse_in_group_sort_order = 3,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.discount_audit';

UPDATE public.rpt_reports SET
    category_code = 'grp.ops',
    descr = 'Till / Drawer Variance',
    ui_icon_backdrop_hex = '#FEF9C3',
    ui_icon_foreground_hex = '#A16207',
    ui_hover_border_hex = '#EAB308',
    ui_hover_surface_hex = '#FEFCE8',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = '#FEE2E2',
    ui_badge_icon_foreground_hex = '#B91C1C',
    ui_badge_hover_border_hex = '#EF4444',
    ui_badge_hover_surface_hex = '#FEF2F2',
    ui_badge_chevron_hot_hex = '#334155',
    dashboard_browse_in_group_sort_order = 4,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.till_balance';

UPDATE public.rpt_reports SET
    category_code = 'grp.ops',
    ui_icon_backdrop_hex = '#FEE2E2',
    ui_icon_foreground_hex = '#B91C1C',
    ui_hover_border_hex = '#EF4444',
    ui_hover_surface_hex = '#FEF2F2',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL,
    ui_badge_icon_foreground_hex = NULL,
    ui_badge_hover_border_hex = NULL,
    ui_badge_hover_surface_hex = NULL,
    ui_badge_chevron_hot_hex = NULL,
    dashboard_browse_in_group_sort_order = 5,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.order_edit_audit';

COMMIT;
