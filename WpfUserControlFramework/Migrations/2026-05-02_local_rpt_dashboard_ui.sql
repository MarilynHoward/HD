-- =============================================================================================
-- Migration: RPT dashboard UI columns on branch/local DB (DDL only)
-- Date: 2026-05-02
-- PostgreSQL 9.3. Adds columns with DO-block guards.
--
-- Do NOT duplicate POS_CONTROL seed UPDATEs here. Branch databases receive report/category rows,
-- including all ui_* / browse_* / dashboard_* columns, via AppStatus.SyncRemoteControlLookupsCore
-- (remote read from POS_CONTROL → local upsert). Single source of truth stays on POS_CONTROL.
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

COMMIT;
