-- =============================================================================================
-- Migration: Operations and Services schema
-- Date:      2026-04-23
-- Author:    RestaurantPosWpf / Operations and Services
-- PostgreSQL target: 9.3 (client production). Avoids 9.5+/9.6+ syntax:
--   * NO `ON CONFLICT ...`         (9.5+)  -> INSERT ... WHERE NOT EXISTS
--   * NO `CREATE INDEX IF NOT EXISTS` (9.5+) -> DO block that probes pg_indexes
--   * NO `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` (9.6+) -> DO block via information_schema
-- `CREATE TABLE IF NOT EXISTS` is valid since 9.1 and is used as-is.
--
-- Scope
--   Creates the persistence layer for the Operations and Services user control. The WPF
--   `OpsServicesStore` used to keep everything in memory; this migration provides the backing
--   tables so the store can follow the same pattern as Staff and Access (see
--   2026-04-21_staff_access_init.sql): live rows stay around across restarts, and dummy demo
--   data is toggled by `SeedDummyDataOnStartup` in App.config.
--
-- Tables
--   public.ops_floors             -- canonical floor names (empty floors allowed)
--   public.ops_tables             -- restaurant tables / floor assets (OpsFloorTable)
--   public.ops_shifts             -- scheduled shifts (OpsScheduledShift)
--   public.ops_shift_tables       -- M:N join between shifts and tables
--   public.ops_reservations       -- guest reservations (OpsReservation)
--   public.ops_floor_plan_layouts -- per (date, floor, table) x/y positions in the floor plan
--
-- Keys
--   * ops_floors.floor_id is an integer PK (uniqueness is also enforced on name).
--   * ops_tables / ops_shifts / ops_reservations / ops_floor_plan_layouts all use a client-
--     generated uuid PK, matching the existing in-memory Guid identity used by the WPF UI.
--     PostgreSQL 9.3 has a native `uuid` type (since 8.3); we do NOT depend on uuid-ossp /
--     gen_random_uuid(), because the app always supplies the uuid string in its INSERTs.
--   * Staff references (ops_tables.assigned_waiter_id, ops_tables.ops_server_id,
--     ops_shifts.employee_id) key into public.users.user_id as plain integers, per the
--     wpf-postgresql-appstatus-pattern rule (no Guid keys for users/staff).
--
-- Seed / is_seed contract
--   Every table gets an is_seed boolean (default FALSE). The app's seeder deletes only
--   is_seed = TRUE rows and reinserts fresh demo data each launch when
--   SeedDummyDataOnStartup = TRUE. Production data (is_seed = FALSE) is never touched.
--
-- Soft delete
--   ops_floors, ops_tables, ops_shifts, ops_reservations all carry the standard
--   deleted / deleted_ts / deleted_user_id triplet so UI reads filter
--   `COALESCE(deleted, FALSE) = FALSE` and historical audit/operations stays referentially
--   valid after a removal. ops_shift_tables and ops_floor_plan_layouts are derived/positional
--   data; they are hard-deleted with their owning shift/table.
--
-- Idempotent: safe to rerun.
-- =============================================================================================

ROLLBACK;
BEGIN;

-- -------------------------------------------------------------------------------------------------
-- 1. ops_floors: canonical floor names shown in filters, combos, and the manage-floors dialog.
-- -------------------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ops_floors
(
    floor_id         integer NOT NULL,
    name             text    COLLATE pg_catalog."default" NOT NULL,
    auth_user_id     integer NOT NULL,
    active           boolean NOT NULL DEFAULT TRUE,
    deleted          boolean NOT NULL DEFAULT FALSE,
    deleted_ts       timestamp without time zone,
    deleted_user_id  integer,
    affected_ts      timestamp without time zone NOT NULL DEFAULT now(),
    is_seed          boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT ops_floors_pk PRIMARY KEY (floor_id),
    CONSTRAINT ops_floors_name_unique UNIQUE (name)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.ops_floors OWNER TO postgres;

-- -------------------------------------------------------------------------------------------------
-- 2. ops_tables: restaurant tables. UUID PK matches the in-memory OpsFloorTable.Id.
--    location_name is the floor name (denormalized — kept in sync with ops_floors.name on write).
-- -------------------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ops_tables
(
    table_id            uuid    NOT NULL,
    name                text    COLLATE pg_catalog."default" NOT NULL,
    location_name       text    COLLATE pg_catalog."default" NOT NULL DEFAULT 'Main Floor',
    seat_count          integer NOT NULL DEFAULT 4,
    shape               text    COLLATE pg_catalog."default" NOT NULL DEFAULT 'Square',
    is_active           boolean NOT NULL DEFAULT TRUE,
    assigned_waiter_id  integer,
    zone                integer NOT NULL DEFAULT 1,
    station             integer NOT NULL DEFAULT 1,
    turn_time_minutes   integer NOT NULL DEFAULT 60,
    status              text    COLLATE pg_catalog."default" NOT NULL DEFAULT 'Available',
    notes               text    COLLATE pg_catalog."default",
    accessible          boolean NOT NULL DEFAULT FALSE,
    vip_priority        boolean NOT NULL DEFAULT FALSE,
    can_merge           boolean NOT NULL DEFAULT TRUE,
    created_ts          timestamp without time zone NOT NULL DEFAULT now(),
    modified_ts         timestamp without time zone NOT NULL DEFAULT now(),
    ops_status          text    COLLATE pg_catalog."default" NOT NULL DEFAULT 'Available',
    ops_server_id       integer,
    seated_at_ts        timestamp without time zone,
    party_size          integer,
    auth_user_id        integer NOT NULL,
    affected_ts         timestamp without time zone NOT NULL DEFAULT now(),
    active              boolean NOT NULL DEFAULT TRUE,
    deleted             boolean NOT NULL DEFAULT FALSE,
    deleted_ts          timestamp without time zone,
    deleted_user_id     integer,
    is_seed             boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT ops_tables_pk PRIMARY KEY (table_id)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.ops_tables OWNER TO postgres;

-- -------------------------------------------------------------------------------------------------
-- 3. ops_shifts: scheduled shifts. UUID PK matches OpsScheduledShift.Id. employee_id is the
--    integer public.users.user_id of the assigned staff member.
-- -------------------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ops_shifts
(
    shift_id         uuid    NOT NULL,
    employee_id      integer NOT NULL,
    shift_date       date    NOT NULL,
    start_time       time without time zone NOT NULL,
    end_time         time without time zone NOT NULL,
    source_kind      text    COLLATE pg_catalog."default" NOT NULL DEFAULT 'Daily',
    auth_user_id     integer NOT NULL,
    affected_ts      timestamp without time zone NOT NULL DEFAULT now(),
    active           boolean NOT NULL DEFAULT TRUE,
    deleted          boolean NOT NULL DEFAULT FALSE,
    deleted_ts       timestamp without time zone,
    deleted_user_id  integer,
    is_seed          boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT ops_shifts_pk PRIMARY KEY (shift_id)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.ops_shifts OWNER TO postgres;

-- -------------------------------------------------------------------------------------------------
-- 4. ops_shift_tables: join between shifts and the tables they cover. Hard-deleted with the
--    owning shift (no soft-delete columns — the join row is purely derived).
-- -------------------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ops_shift_tables
(
    shift_id   uuid    NOT NULL,
    table_id   uuid    NOT NULL,
    is_seed    boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT ops_shift_tables_pk PRIMARY KEY (shift_id, table_id)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.ops_shift_tables OWNER TO postgres;

-- -------------------------------------------------------------------------------------------------
-- 5. ops_reservations: guest reservations. UUID PK matches OpsReservation.Id.
--    status is the OpsReservationStatus name (Pending, Confirmed, Seated, Completed, Cancelled).
-- -------------------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ops_reservations
(
    reservation_id   uuid    NOT NULL,
    table_id         uuid    NOT NULL,
    floor_name       text    COLLATE pg_catalog."default" NOT NULL DEFAULT 'Main Floor',
    res_date         date    NOT NULL,
    customer_name    text    COLLATE pg_catalog."default" NOT NULL DEFAULT '',
    phone            text    COLLATE pg_catalog."default",
    email            text    COLLATE pg_catalog."default",
    party_size       integer NOT NULL DEFAULT 2,
    res_time         time without time zone NOT NULL,
    status           text    COLLATE pg_catalog."default" NOT NULL DEFAULT 'Pending',
    notes            text    COLLATE pg_catalog."default",
    reference        text    COLLATE pg_catalog."default",
    auth_user_id     integer NOT NULL,
    affected_ts      timestamp without time zone NOT NULL DEFAULT now(),
    active           boolean NOT NULL DEFAULT TRUE,
    deleted          boolean NOT NULL DEFAULT FALSE,
    deleted_ts       timestamp without time zone,
    deleted_user_id  integer,
    is_seed          boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT ops_reservations_pk PRIMARY KEY (reservation_id)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.ops_reservations OWNER TO postgres;

-- -------------------------------------------------------------------------------------------------
-- 6. ops_floor_plan_layouts: per (date, floor, table) x/y positions for the floor plan canvas.
--    Positional data only; no soft-delete.
-- -------------------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ops_floor_plan_layouts
(
    layout_id     uuid    NOT NULL,
    layout_date   date    NOT NULL,
    floor_name    text    COLLATE pg_catalog."default" NOT NULL,
    table_id      uuid    NOT NULL,
    pos_x         double precision NOT NULL DEFAULT 0,
    pos_y         double precision NOT NULL DEFAULT 0,
    auth_user_id  integer NOT NULL,
    affected_ts   timestamp without time zone NOT NULL DEFAULT now(),
    is_seed       boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT ops_floor_plan_layouts_pk PRIMARY KEY (layout_id),
    CONSTRAINT ops_floor_plan_layouts_unique UNIQUE (layout_date, floor_name, table_id)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.ops_floor_plan_layouts OWNER TO postgres;

-- -------------------------------------------------------------------------------------------------
-- 7. Indexes used by the WPF store's filters and range queries.
--    9.3-safe: emulate CREATE INDEX IF NOT EXISTS via DO blocks against pg_indexes.
-- -------------------------------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='ix_ops_tables_location') THEN
        CREATE INDEX ix_ops_tables_location
            ON public.ops_tables (location_name)
            WHERE COALESCE(deleted, FALSE) = FALSE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='ix_ops_tables_waiter') THEN
        CREATE INDEX ix_ops_tables_waiter
            ON public.ops_tables (assigned_waiter_id)
            WHERE COALESCE(deleted, FALSE) = FALSE AND assigned_waiter_id IS NOT NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='ix_ops_shifts_emp_date') THEN
        CREATE INDEX ix_ops_shifts_emp_date
            ON public.ops_shifts (employee_id, shift_date)
            WHERE COALESCE(deleted, FALSE) = FALSE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='ix_ops_shifts_date') THEN
        CREATE INDEX ix_ops_shifts_date
            ON public.ops_shifts (shift_date)
            WHERE COALESCE(deleted, FALSE) = FALSE;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='ix_ops_shift_tables_table') THEN
        CREATE INDEX ix_ops_shift_tables_table
            ON public.ops_shift_tables (table_id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='ix_ops_reservations_date_floor') THEN
        CREATE INDEX ix_ops_reservations_date_floor
            ON public.ops_reservations (res_date, floor_name)
            WHERE COALESCE(deleted, FALSE) = FALSE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='ix_ops_reservations_table') THEN
        CREATE INDEX ix_ops_reservations_table
            ON public.ops_reservations (table_id, res_date)
            WHERE COALESCE(deleted, FALSE) = FALSE;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='ix_ops_floor_plan_layouts_key') THEN
        CREATE INDEX ix_ops_floor_plan_layouts_key
            ON public.ops_floor_plan_layouts (layout_date, floor_name);
    END IF;
END$$;

COMMIT;

-- =============================================================================================
-- Rollback hint (reverses this migration):
--   ROLLBACK; BEGIN;
--     DROP TABLE IF EXISTS public.ops_floor_plan_layouts;
--     DROP TABLE IF EXISTS public.ops_reservations;
--     DROP TABLE IF EXISTS public.ops_shift_tables;
--     DROP TABLE IF EXISTS public.ops_shifts;
--     DROP TABLE IF EXISTS public.ops_tables;
--     DROP TABLE IF EXISTS public.ops_floors;
--   COMMIT;
-- =============================================================================================
