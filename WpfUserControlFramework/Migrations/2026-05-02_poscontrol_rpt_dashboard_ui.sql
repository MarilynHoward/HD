-- =============================================================================================
-- Migration: RPT dashboard UI metadata (POS_CONTROL — canonical catalog + seeds)
-- Date: 2026-05-02
-- PostgreSQL 9.3. Adds columns with DO-block guards; UPDATE seeds rows on POS_CONTROL only.
-- Branch databases: run 2026-05-02_local_rpt_dashboard_ui.sql (DDL only), then rely on app sync
-- (SyncRemoteControlLookupsCore) to copy this data locally — do not duplicate seeds on branches.
-- =============================================================================================
ROLLBACK;
BEGIN;

-- rpt_report_categories: browse strip + tile chrome ---------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_report_categories' AND column_name = 'browse_panel_descr') THEN
        ALTER TABLE public.rpt_report_categories ADD COLUMN browse_panel_descr text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_report_categories' AND column_name = 'browse_icon_glyph_id') THEN
        ALTER TABLE public.rpt_report_categories ADD COLUMN browse_icon_glyph_id text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_report_categories' AND column_name = 'browse_show_chevron') THEN
        ALTER TABLE public.rpt_report_categories ADD COLUMN browse_show_chevron boolean NOT NULL DEFAULT false;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_report_categories' AND column_name = 'browse_tile_report_count') THEN
        ALTER TABLE public.rpt_report_categories ADD COLUMN browse_tile_report_count integer NOT NULL DEFAULT 0;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_report_categories' AND column_name = 'dashboard_browse_row') THEN
        ALTER TABLE public.rpt_report_categories ADD COLUMN dashboard_browse_row smallint;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_report_categories' AND column_name = 'dashboard_browse_sort_order') THEN
        ALTER TABLE public.rpt_report_categories ADD COLUMN dashboard_browse_sort_order integer;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_report_categories' AND column_name = 'ui_icon_backdrop_hex') THEN
        ALTER TABLE public.rpt_report_categories ADD COLUMN ui_icon_backdrop_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_report_categories' AND column_name = 'ui_icon_foreground_hex') THEN
        ALTER TABLE public.rpt_report_categories ADD COLUMN ui_icon_foreground_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_report_categories' AND column_name = 'ui_hover_border_hex') THEN
        ALTER TABLE public.rpt_report_categories ADD COLUMN ui_hover_border_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_report_categories' AND column_name = 'ui_hover_surface_hex') THEN
        ALTER TABLE public.rpt_report_categories ADD COLUMN ui_hover_surface_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_report_categories' AND column_name = 'ui_chevron_hot_hex') THEN
        ALTER TABLE public.rpt_report_categories ADD COLUMN ui_chevron_hot_hex text;
    END IF;
END$$;

-- rpt_reports: card themes, attention badge, recent/attention/browse ordering --------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'ui_icon_backdrop_hex') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN ui_icon_backdrop_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'ui_icon_foreground_hex') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN ui_icon_foreground_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'ui_hover_border_hex') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN ui_hover_border_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'ui_hover_surface_hex') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN ui_hover_surface_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'ui_chevron_hot_hex') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN ui_chevron_hot_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'ui_badge_icon_backdrop_hex') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN ui_badge_icon_backdrop_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'ui_badge_icon_foreground_hex') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN ui_badge_icon_foreground_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'ui_badge_hover_border_hex') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN ui_badge_hover_border_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'ui_badge_hover_surface_hex') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN ui_badge_hover_surface_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'ui_badge_chevron_hot_hex') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN ui_badge_chevron_hot_hex text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'dashboard_recent_sort_order') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN dashboard_recent_sort_order integer;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'recent_last_run_display') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN recent_last_run_display text;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'recent_last_accessed_offset_hours') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN recent_last_accessed_offset_hours integer;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'recent_last_accessed_start_of_today_utc') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN recent_last_accessed_start_of_today_utc boolean NOT NULL DEFAULT false;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'dashboard_attention_sort_order') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN dashboard_attention_sort_order integer;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'dashboard_attention_count') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN dashboard_attention_count integer;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'rpt_reports' AND column_name = 'dashboard_browse_in_group_sort_order') THEN
        ALTER TABLE public.rpt_reports ADD COLUMN dashboard_browse_in_group_sort_order integer;
    END IF;
END$$;

-- Browse category tiles (row 1 / row 2)
UPDATE public.rpt_report_categories SET
    browse_panel_descr = 'Track daily sales, revenue and product performance.',
    browse_icon_glyph_id = 'bar_chart',
    browse_show_chevron = false,
    browse_tile_report_count = 5,
    dashboard_browse_row = 1,
    dashboard_browse_sort_order = 1,
    ui_icon_backdrop_hex = '#DBEAFE',
    ui_icon_foreground_hex = '#1D4ED8',
    ui_hover_border_hex = '#3B82F6',
    ui_hover_surface_hex = '#E6F3FF',
    ui_chevron_hot_hex = '#334155'
WHERE category_code = 'grp.sales';

UPDATE public.rpt_report_categories SET
    browse_panel_descr = 'Monitor inventory levels, usage and variance.',
    browse_icon_glyph_id = 'package',
    browse_show_chevron = true,
    browse_tile_report_count = 5,
    dashboard_browse_row = 1,
    dashboard_browse_sort_order = 2,
    ui_icon_backdrop_hex = '#CCFBF1',
    ui_icon_foreground_hex = '#0F766E',
    ui_hover_border_hex = '#14B8A6',
    ui_hover_surface_hex = '#E8FFFA',
    ui_chevron_hot_hex = '#334155'
WHERE category_code = 'grp.stock';

UPDATE public.rpt_report_categories SET
    browse_panel_descr = 'Monitor voids, refunds and operational compliance.',
    browse_icon_glyph_id = 'shield_check',
    browse_show_chevron = false,
    browse_tile_report_count = 5,
    dashboard_browse_row = 1,
    dashboard_browse_sort_order = 3,
    ui_icon_backdrop_hex = '#FFE4E6',
    ui_icon_foreground_hex = '#BE123C',
    ui_hover_border_hex = '#E02424',
    ui_hover_surface_hex = '#FDF2F4',
    ui_chevron_hot_hex = '#334155'
WHERE category_code = 'grp.ops';

UPDATE public.rpt_report_categories SET
    browse_panel_descr = 'Track purchases, supplier performance and delivery.',
    browse_icon_glyph_id = 'truck',
    browse_show_chevron = false,
    browse_tile_report_count = 5,
    dashboard_browse_row = 2,
    dashboard_browse_sort_order = 1,
    ui_icon_backdrop_hex = '#FFEDD5',
    ui_icon_foreground_hex = '#C2410C',
    ui_hover_border_hex = '#E8A05B',
    ui_hover_surface_hex = '#FDF8F2',
    ui_chevron_hot_hex = '#334155'
WHERE category_code = 'grp.procurement';

UPDATE public.rpt_report_categories SET
    browse_panel_descr = 'Analyse margins, costs and profitability metrics.',
    browse_icon_glyph_id = 'pie_chart',
    browse_show_chevron = true,
    browse_tile_report_count = 5,
    dashboard_browse_row = 2,
    dashboard_browse_sort_order = 2,
    ui_icon_backdrop_hex = '#EDE9FE',
    ui_icon_foreground_hex = '#6D28D9',
    ui_hover_border_hex = '#8B5CF6',
    ui_hover_surface_hex = '#F5F3FF',
    ui_chevron_hot_hex = '#334155'
WHERE category_code = 'grp.profitability';

UPDATE public.rpt_report_categories SET
    browse_panel_descr = 'Monitor staff activity, user access and security controls.',
    browse_icon_glyph_id = 'shield_check',
    browse_show_chevron = false,
    browse_tile_report_count = 5,
    dashboard_browse_row = 2,
    dashboard_browse_sort_order = 3,
    ui_icon_backdrop_hex = '#DBEAFE',
    ui_icon_foreground_hex = '#1D4ED8',
    ui_hover_border_hex = '#3B82F6',
    ui_hover_surface_hex = '#E6F3FF',
    ui_chevron_hot_hex = '#334155'
WHERE category_code = 'grp.staff_access';

-- Reports: themes + dashboard ordering (recent / attention / browse subgroup)
UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#DBEAFE', ui_icon_foreground_hex = '#1D4ED8', ui_hover_border_hex = '#3B82F6', ui_hover_surface_hex = '#E6F3FF', ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL, ui_badge_icon_foreground_hex = NULL, ui_badge_hover_border_hex = NULL, ui_badge_hover_surface_hex = NULL, ui_badge_chevron_hot_hex = NULL,
    dashboard_recent_sort_order = 1, recent_last_run_display = 'Last run: 2 hours ago', recent_last_accessed_offset_hours = -2, recent_last_accessed_start_of_today_utc = false,
    dashboard_attention_sort_order = NULL, dashboard_attention_count = NULL,
    dashboard_browse_in_group_sort_order = 1
WHERE report_code = 'rpt.daily_sales';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#DBEAFE', ui_icon_foreground_hex = '#1D4ED8', ui_hover_border_hex = '#3B82F6', ui_hover_surface_hex = '#E6F3FF', ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL, ui_badge_icon_foreground_hex = NULL, ui_badge_hover_border_hex = NULL, ui_badge_hover_surface_hex = NULL, ui_badge_chevron_hot_hex = NULL,
    dashboard_recent_sort_order = NULL, recent_last_run_display = NULL, recent_last_accessed_offset_hours = NULL, recent_last_accessed_start_of_today_utc = false,
    dashboard_attention_sort_order = NULL, dashboard_attention_count = NULL,
    dashboard_browse_in_group_sort_order = 2
WHERE report_code = 'rpt.revenue';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#E6D5FF', ui_icon_foreground_hex = '#C95BFF', ui_hover_border_hex = '#C7B1DA', ui_hover_surface_hex = '#F8F6FD', ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL, ui_badge_icon_foreground_hex = NULL, ui_badge_hover_border_hex = NULL, ui_badge_hover_surface_hex = NULL, ui_badge_chevron_hot_hex = NULL,
    dashboard_recent_sort_order = 2, recent_last_run_display = 'Last run: Yesterday', recent_last_accessed_offset_hours = -24, recent_last_accessed_start_of_today_utc = false,
    dashboard_attention_sort_order = NULL, dashboard_attention_count = NULL,
    dashboard_browse_in_group_sort_order = NULL
WHERE report_code = 'rpt.vat_summary';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#CCFBF1', ui_icon_foreground_hex = '#0F766E', ui_hover_border_hex = '#14B8A6', ui_hover_surface_hex = '#E8FFFA', ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL, ui_badge_icon_foreground_hex = NULL, ui_badge_hover_border_hex = NULL, ui_badge_hover_surface_hex = NULL, ui_badge_chevron_hot_hex = NULL,
    dashboard_recent_sort_order = 3, recent_last_run_display = 'Last run: 3 days ago', recent_last_accessed_offset_hours = -72, recent_last_accessed_start_of_today_utc = false,
    dashboard_attention_sort_order = NULL, dashboard_attention_count = NULL,
    dashboard_browse_in_group_sort_order = 1
WHERE report_code = 'rpt.wastage';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#FEE2E2', ui_icon_foreground_hex = '#B91C1C', ui_hover_border_hex = '#EF4444', ui_hover_surface_hex = '#FEF2F2', ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = NULL, ui_badge_icon_foreground_hex = NULL, ui_badge_hover_border_hex = NULL, ui_badge_hover_surface_hex = NULL, ui_badge_chevron_hot_hex = NULL,
    dashboard_recent_sort_order = 4, recent_last_run_display = 'Last run: Today', recent_last_accessed_offset_hours = 0, recent_last_accessed_start_of_today_utc = true,
    dashboard_attention_sort_order = NULL, dashboard_attention_count = NULL,
    dashboard_browse_in_group_sort_order = 1
WHERE report_code = 'rpt.voids';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#FEE2E2', ui_icon_foreground_hex = '#B91C1C', ui_hover_border_hex = '#EF4444', ui_hover_surface_hex = '#FEF2F2', ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = '#FEE2E2', ui_badge_icon_foreground_hex = '#B91C1C', ui_badge_hover_border_hex = '#EF4444', ui_badge_hover_surface_hex = '#FEF2F2', ui_badge_chevron_hot_hex = '#334155',
    dashboard_recent_sort_order = NULL, recent_last_run_display = NULL, recent_last_accessed_offset_hours = NULL, recent_last_accessed_start_of_today_utc = false,
    dashboard_attention_sort_order = 1, dashboard_attention_count = 3,
    dashboard_browse_in_group_sort_order = NULL
WHERE report_code = 'rpt.high_value_voids';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#FFEDD5', ui_icon_foreground_hex = '#9A3412', ui_hover_border_hex = '#EA580C', ui_hover_surface_hex = '#FFF7ED', ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = '#FFEDD5', ui_badge_icon_foreground_hex = '#9A3412', ui_badge_hover_border_hex = '#EA580C', ui_badge_hover_surface_hex = '#FFF7ED', ui_badge_chevron_hot_hex = '#334155',
    dashboard_recent_sort_order = NULL, recent_last_run_display = NULL, recent_last_accessed_offset_hours = NULL, recent_last_accessed_start_of_today_utc = false,
    dashboard_attention_sort_order = 2, dashboard_attention_count = 8,
    dashboard_browse_in_group_sort_order = NULL
WHERE report_code = 'rpt.low_stock';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#CCFBF1', ui_icon_foreground_hex = '#0F766E', ui_hover_border_hex = '#14B8A6', ui_hover_surface_hex = '#E8FFFA', ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = '#FFEDD5', ui_badge_icon_foreground_hex = '#9A3412', ui_badge_hover_border_hex = '#EA580C', ui_badge_hover_surface_hex = '#FFF7ED', ui_badge_chevron_hot_hex = '#334155',
    dashboard_recent_sort_order = NULL, recent_last_run_display = NULL, recent_last_accessed_offset_hours = NULL, recent_last_accessed_start_of_today_utc = false,
    dashboard_attention_sort_order = 3, dashboard_attention_count = 2,
    dashboard_browse_in_group_sort_order = NULL
WHERE report_code = 'rpt.delivery_variances';

UPDATE public.rpt_reports SET
    ui_icon_backdrop_hex = '#FEF9C3', ui_icon_foreground_hex = '#A16207', ui_hover_border_hex = '#EAB308', ui_hover_surface_hex = '#FEFCE8', ui_chevron_hot_hex = '#334155',
    ui_badge_icon_backdrop_hex = '#FEE2E2', ui_badge_icon_foreground_hex = '#B91C1C', ui_badge_hover_border_hex = '#EF4444', ui_badge_hover_surface_hex = '#FEF2F2', ui_badge_chevron_hot_hex = '#334155',
    dashboard_recent_sort_order = NULL, recent_last_run_display = NULL, recent_last_accessed_offset_hours = NULL, recent_last_accessed_start_of_today_utc = false,
    dashboard_attention_sort_order = 4, dashboard_attention_count = 1,
    dashboard_browse_in_group_sort_order = NULL
WHERE report_code = 'rpt.till_balance';

COMMIT;
