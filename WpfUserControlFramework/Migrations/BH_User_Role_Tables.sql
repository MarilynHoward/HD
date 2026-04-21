CREATE TABLE IF NOT EXISTS public.users
(
    user_id integer NOT NULL,
    user_name text COLLATE pg_catalog."default" NOT NULL,
    password text COLLATE pg_catalog."default" NOT NULL,
    role_id integer NOT NULL,
    inserted_ts timestamp without time zone NOT NULL DEFAULT now(),
    auth_user_id integer NOT NULL,
    image_path text COLLATE pg_catalog."default",
    card_number text COLLATE pg_catalog."default",
    first_name text COLLATE pg_catalog."default",
    second_name text COLLATE pg_catalog."default",
    surname text COLLATE pg_catalog."default",
    id_doc_path text COLLATE pg_catalog."default",
    active boolean NOT NULL DEFAULT true,
    affected_ts timestamp without time zone,
    audit_id bigint,
    finger_print text COLLATE pg_catalog."default",
    password_changed_ts timestamp with time zone,
    CONSTRAINT users_prim PRIMARY KEY (user_id),
    CONSTRAINT user_name_uniq UNIQUE (user_name),
    CONSTRAINT auth_users_fk FOREIGN KEY (auth_user_id)
        REFERENCES public.users (user_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)
WITH (
    OIDS = FALSE
)
TABLESPACE pg_default;

ALTER TABLE public.users
    OWNER to postgres;

    

CREATE TABLE IF NOT EXISTS public.roles
(
    role_id integer NOT NULL,
    descr text COLLATE pg_catalog."default" NOT NULL,
    inserted_ts timestamp without time zone NOT NULL DEFAULT now(),
    auth_user_id integer,
    active boolean NOT NULL DEFAULT true,
    note text COLLATE pg_catalog."default",
    deleted boolean NOT NULL DEFAULT false,
    deleted_ts timestamp without time zone,
    deleted_user_id integer,
    audit_id bigint,
    affected_ts timestamp without time zone NOT NULL DEFAULT now(),
    CONSTRAINT roles_prim PRIMARY KEY (role_id),
    CONSTRAINT auth_users_fk FOREIGN KEY (auth_user_id)
        REFERENCES public.users (user_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)
WITH (
    OIDS = FALSE
)
TABLESPACE pg_default;

ALTER TABLE public.roles
    OWNER to postgres;