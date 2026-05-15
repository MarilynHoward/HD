-- =============================================================================================
-- Migration: Procurement + Profitability browse reports; wastage → profitability; stock=4
-- Date: 2026-05-15
-- PostgreSQL 9.3. ODBC-safe: plain INSERT/UPDATE only (no DO blocks).
-- Local / branch DB — mirror poscontrol catalog rows.
-- Run after dashboard UI columns exist (2026-05-02_local_rpt_dashboard_ui.sql).
-- =============================================================================================
ROLLBACK;
BEGIN;

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.purchases_by_supplier', 'grp.procurement', 'Purchases by Supplier', 'Supplier purchase history', 'truck', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.purchases_by_supplier');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.purchase_order_summary', 'grp.procurement', 'Purchase Order Summary', 'PO status and tracking', 'truck', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.purchase_order_summary');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.outstanding_orders', 'grp.procurement', 'Outstanding Orders', 'Pending purchase orders', 'truck', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.outstanding_orders');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.supplier_spend_analysis', 'grp.procurement', 'Supplier Spend Analysis', 'Cost analysis by supplier', 'truck', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.supplier_spend_analysis');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.gross_profit_by_product', 'grp.profitability', 'Gross Profit by Product', 'Product-level margin analysis', 'pie_chart', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.gross_profit_by_product');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.gross_profit_by_category', 'grp.profitability', 'Gross Profit by Category', 'Category margin overview', 'pie_chart', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.gross_profit_by_category');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.menu_item_margin_analysis', 'grp.profitability', 'Menu Item Margin Analysis', 'Individual item profitability', 'pie_chart', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.menu_item_margin_analysis');

INSERT INTO public.rpt_reports (report_code, category_code, descr, long_descr, icon_glyph_id, auth_user_id)
SELECT 'rpt.delivery_channel_profitability', 'grp.profitability', 'Delivery Channel Profitability', 'Channel-specific margins', 'pie_chart', 0
WHERE NOT EXISTS (SELECT 1 FROM public.rpt_reports WHERE report_code = 'rpt.delivery_channel_profitability');

UPDATE public.rpt_report_categories SET browse_tile_report_count = 4, modified_ts = now(), auth_user_id = 0 WHERE category_code = 'grp.stock';

UPDATE public.rpt_reports SET
    category_code = 'grp.stock',
    ui_icon_backdrop_hex = '#FFEDD5',
    ui_icon_foreground_hex = '#9A3412',
    ui_hover_border_hex = '#EA580C',
    ui_hover_surface_hex = '#FFF7ED',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = '#FFEDD5',
    ui_badge_icon_foreground_hex = '#9A3412',
    ui_badge_hover_border_hex = '#EA580C',
    ui_badge_hover_surface_hex = '#FFF7ED',
    ui_badge_chevron_hot_hex = '#334155',
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
    dashboard_browse_in_group_sort_order = 2,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.stock_movement';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#CCFBF1',
    ui_icon_foreground_hex = '#0F766E',
    ui_hover_border_hex = '#14B8A6',
    ui_hover_surface_hex = '#E8FFFA',
    ui_chevron_hot_hex = '#334155',
    dashboard_browse_in_group_sort_order = 3,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.stock_variance';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#CCFBF1',
    ui_icon_foreground_hex = '#0F766E',
    ui_hover_border_hex = '#14B8A6',
    ui_hover_surface_hex = '#E8FFFA',
    ui_chevron_hot_hex = '#334155',
    dashboard_browse_in_group_sort_order = 4,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.reorder';

UPDATE public.rpt_reports SET
    category_code = 'grp.profitability',
    descr = 'Stock & Waste Impact',
    icon_glyph_id = 'staff_delete_user_trash',
    ui_icon_backdrop_hex = '#EDE9FE',
    ui_icon_foreground_hex = '#6D28D9',
    ui_hover_border_hex = '#8B5CF6',
    ui_hover_surface_hex = '#F5F3FF',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL,
    ui_badge_icon_foreground_hex = NULL,
    ui_badge_hover_border_hex = NULL,
    ui_badge_hover_surface_hex = NULL,
    ui_badge_chevron_hot_hex = NULL,
    dashboard_browse_in_group_sort_order = 4,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.wastage';

UPDATE public.rpt_reports SET
    category_code = 'grp.procurement',
    descr = 'Purchases by Supplier',
    ui_icon_backdrop_hex = '#FFEDD5',
    ui_icon_foreground_hex = '#9A3412',
    ui_hover_border_hex = '#EA580C',
    ui_hover_surface_hex = '#FFF7ED',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL,
    ui_badge_icon_foreground_hex = NULL,
    ui_badge_hover_border_hex = NULL,
    ui_badge_hover_surface_hex = NULL,
    ui_badge_chevron_hot_hex = NULL,
    dashboard_browse_in_group_sort_order = 1,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.purchases_by_supplier';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#FFEDD5',
    ui_icon_foreground_hex = '#9A3412',
    ui_hover_border_hex = '#EA580C',
    ui_hover_surface_hex = '#FFF7ED',
    ui_chevron_hot_hex = '#334155',
    dashboard_browse_in_group_sort_order = 2,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.purchase_order_summary';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#FFEDD5',
    ui_icon_foreground_hex = '#9A3412',
    ui_hover_border_hex = '#EA580C',
    ui_hover_surface_hex = '#FFF7ED',
    ui_chevron_hot_hex = '#334155',
    dashboard_browse_in_group_sort_order = 3,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.outstanding_orders';

UPDATE public.rpt_reports SET
    category_code = 'grp.procurement',
    descr = 'Delivery Variance Report',
    ui_icon_backdrop_hex = '#FFEDD5',
    ui_icon_foreground_hex = '#9A3412',
    ui_hover_border_hex = '#EA580C',
    ui_hover_surface_hex = '#FFF7ED',
    ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = '#FFEDD5',
    ui_badge_icon_foreground_hex = '#9A3412',
    ui_badge_hover_border_hex = '#EA580C',
    ui_badge_hover_surface_hex = '#FFF7ED',
    ui_badge_chevron_hot_hex = '#334155',
    dashboard_browse_in_group_sort_order = 4,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.delivery_variances';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#FFEDD5',
    ui_icon_foreground_hex = '#9A3412',
    ui_hover_border_hex = '#EA580C',
    ui_hover_surface_hex = '#FFF7ED',
    ui_chevron_hot_hex = '#334155',
    dashboard_browse_in_group_sort_order = 5,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.supplier_spend_analysis';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#EDE9FE',
    ui_icon_foreground_hex = '#6D28D9',
    ui_hover_border_hex = '#8B5CF6',
    ui_hover_surface_hex = '#F5F3FF',
    ui_chevron_hot_hex = '#334155',
    dashboard_browse_in_group_sort_order = 1,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.gross_profit_by_product';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#EDE9FE',
    ui_icon_foreground_hex = '#6D28D9',
    ui_hover_border_hex = '#8B5CF6',
    ui_hover_surface_hex = '#F5F3FF',
    ui_chevron_hot_hex = '#334155',
    dashboard_browse_in_group_sort_order = 2,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.gross_profit_by_category';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#EDE9FE',
    ui_icon_foreground_hex = '#6D28D9',
    ui_hover_border_hex = '#8B5CF6',
    ui_hover_surface_hex = '#F5F3FF',
    ui_chevron_hot_hex = '#334155',
    dashboard_browse_in_group_sort_order = 3,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.menu_item_margin_analysis';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#EDE9FE',
    ui_icon_foreground_hex = '#6D28D9',
    ui_hover_border_hex = '#8B5CF6',
    ui_hover_surface_hex = '#F5F3FF',
    ui_chevron_hot_hex = '#334155',
    dashboard_browse_in_group_sort_order = 5,
    modified_ts = now(),
    auth_user_id = 0
WHERE report_code = 'rpt.delivery_channel_profitability';

COMMIT;
