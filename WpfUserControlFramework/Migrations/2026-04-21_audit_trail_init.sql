-- =============================================================================================
-- Migration: create public.audit_trail + public.database_update_phase (client-aligned schema)
-- Date:      2026-04-21
-- Author:    RestaurantPosWpf / Staff and Access
--
-- Background
--   An earlier iteration of this feature used public.audit_log (see removed 2026-04-21_audit_log_init.sql).
--   The client has a canonical audit schema in production (audit_trail + database_update_phase) and has
--   asked us to align to it. This migration:
--     1. Drops the interim public.audit_log table if it exists.
--     2. Creates public.audit_trail matching the client's DDL exactly.
--     3. Creates public.database_update_phase (lookup for phase_id <-> descr).
--     4. Seeds the phase rows that Staff and Access currently emits so reads work on day one.
--     5. Adds indexes that the Staff and Access audit viewer relies on.
--
-- Field mapping from the previous (audit_log) schema to this one:
--   ts_utc              -> inserted_ts      (timestamp WITHOUT TIME ZONE; the app writes UTC explicitly)
--   category+event_type -> phase            ("<category>: <event_type>", e.g. "Staff and Access: PasswordChanged")
--   subject_kind        -> control_id_descr (e.g. "user")
--   subject_id          -> control_id       (bigint)
--   actor_user_id       -> auth_user_id
--   details_json        -> event            (JSON payload; includes the sanitised summary text)
--   summary             -> folded into "event" (no dedicated column)
--   NEW invalid_password -> set on sign-in failures; stores the hashed attempted password (PBKDF2).
--   NEW role_id          -> role of the actor, resolved from public.users at INSERT time.
--   NEW phase_id         -> integer reference into public.database_update_phase (descr = phase).
--
-- Concurrency note (audit_id)
--   The client's DDL has no bigserial sequence on audit_id. The application assigns
--   audit_id = (SELECT COALESCE(MAX(audit_id), 0) + 1 FROM public.audit_trail) inside the INSERT.
--   In the single-user POS client this is acceptable; for heavy concurrency the client should
--   attach a dedicated sequence and default. The app's audit write is best-effort, so a PK
--   collision never breaks the user flow -- the event is simply dropped.
--
-- Idempotent: safe to rerun.
-- =============================================================================================

ROLLBACK;
BEGIN;

-- -------------------------------------------------------------------------------------------------
-- 1. Drop the interim audit_log table (the feature previously wrote here).
-- -------------------------------------------------------------------------------------------------
DROP TABLE IF EXISTS public.audit_log CASCADE;

-- -------------------------------------------------------------------------------------------------
-- 2. Lookup table: database_update_phase (client schema).
-- -------------------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.database_update_phase
(
    phase_id integer NOT NULL,
    descr    text    COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT phase_prim PRIMARY KEY (phase_id),
    CONSTRAINT uniq_descr UNIQUE (descr)
)
WITH (
    OIDS = FALSE
)
TABLESPACE pg_default;

ALTER TABLE public.database_update_phase OWNER TO postgres;

-- -------------------------------------------------------------------------------------------------
-- 3. Audit table: audit_trail (client schema - exact).
-- -------------------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.audit_trail
(
    audit_id          bigint      NOT NULL,
    phase             text        COLLATE pg_catalog."default",
    event             text        COLLATE pg_catalog."default",
    inserted_ts       timestamp without time zone NOT NULL DEFAULT now(),
    auth_user_id      integer     NOT NULL,
    role_id           integer     NOT NULL,
    control_id_descr  text        COLLATE pg_catalog."default",
    control_id        bigint,
    invalid_password  text        COLLATE pg_catalog."default",
    phase_id          integer,
    ip_address        text,
    machine_name      text,
    CONSTRAINT "AUDIT_PRIM" PRIMARY KEY (audit_id)
)
WITH (
    OIDS = FALSE
)
TABLESPACE pg_default;

ALTER TABLE public.audit_trail OWNER TO postgres;

-- -------------------------------------------------------------------------------------------------
-- 4. Seed the phases that Staff and Access currently emits (ON CONFLICT keeps live rows intact).
--    Phase text format: "<Category>: <EventType>".
-- -------------------------------------------------------------------------------------------------
INSERT INTO public.database_update_phase (phase_id, descr) VALUES
    ( 1, 'Staff and Access: UserCreated'),
    ( 2, 'Staff and Access: UserUpdated'),
    ( 3, 'Staff and Access: UserDeleted'),
    ( 4, 'Staff and Access: UserDeleteDenied'),
    ( 5, 'Staff and Access: PasswordChanged'),
    ( 6, 'Staff and Access: RoleChanged'),
    ( 7, 'Staff and Access: SignInStatusUpdated'),
    ( 8, 'Staff and Access: BiometricEnrollmentCompleted'),
    ( 9, 'Staff and Access: IdentityDocumentSynced'),
    (10, 'Staff and Access: ProfileImageSynced'),
    (11, 'Staff and Access: SignInFailure')
ON CONFLICT (descr) DO NOTHING;

-- -------------------------------------------------------------------------------------------------
-- 5. Indexes used by the Staff and Access audit viewer and future global admin feed.
-- -------------------------------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_audit_trail_subject
    ON public.audit_trail (control_id_descr, control_id, inserted_ts DESC);

CREATE INDEX IF NOT EXISTS ix_audit_trail_ts
    ON public.audit_trail (inserted_ts DESC);

CREATE INDEX IF NOT EXISTS ix_audit_trail_actor
    ON public.audit_trail (auth_user_id, inserted_ts DESC);

CREATE INDEX IF NOT EXISTS ix_audit_trail_phase_id
    ON public.audit_trail (phase_id);

COMMIT;

-- =============================================================================================
-- Rollback hint (reverses this migration):
--   ROLLBACK; BEGIN;
--     DROP INDEX IF EXISTS public.ix_audit_trail_phase_id;
--     DROP INDEX IF EXISTS public.ix_audit_trail_actor;
--     DROP INDEX IF EXISTS public.ix_audit_trail_ts;
--     DROP INDEX IF EXISTS public.ix_audit_trail_subject;
--     DROP TABLE IF EXISTS public.audit_trail;
--     DROP TABLE IF EXISTS public.database_update_phase;
--   COMMIT;
-- =============================================================================================
