CREATE TABLE IF NOT EXISTS public.taxes
(
    tax_id integer NOT NULL,
    descr text COLLATE pg_catalog."default" NOT NULL,
    rate numeric NOT NULL,
    active boolean NOT NULL DEFAULT false,
    affected_ts timestamp without time zone NOT NULL DEFAULT now(),
    auth_user_id integer NOT NULL,
    note text COLLATE pg_catalog."default",
    deleted boolean NOT NULL DEFAULT false,
    deleted_ts timestamp without time zone,
    deleted_user_id integer,
    audit_id bigint,
    inserted_ts timestamp without time zone NOT NULL DEFAULT now(),
    CONSTRAINT tax_prim PRIMARY KEY (tax_id),
    CONSTRAINT audit_fk FOREIGN KEY (audit_id)
        REFERENCES public.audit_trail (audit_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION,
    CONSTRAINT auth_users_fk FOREIGN KEY (auth_user_id)
        REFERENCES public.users (user_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION,
    CONSTRAINT del_users_fk FOREIGN KEY (deleted_user_id)
        REFERENCES public.users (user_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)
WITH (
    OIDS = FALSE
)
TABLESPACE pg_default;

ALTER TABLE public.taxes
    OWNER to postgres;

-- Bootstrap rows (auth_user_id = 0 matches app bootstrap user; run after `users` exists).
INSERT INTO public.taxes (tax_id, descr, rate, active, auth_user_id)
SELECT 1, 'Standard Rate (15%)', 0.15, true, 0
WHERE NOT EXISTS (SELECT 1 FROM public.taxes WHERE tax_id = 1);

INSERT INTO public.taxes (tax_id, descr, rate, active, auth_user_id)
SELECT 2, 'Zero-Rated Items', 0, true, 0
WHERE NOT EXISTS (SELECT 1 FROM public.taxes WHERE tax_id = 2);

INSERT INTO public.taxes (tax_id, descr, rate, active, auth_user_id)
SELECT 3, 'Exempt Items', 0, true, 0
WHERE NOT EXISTS (SELECT 1 FROM public.taxes WHERE tax_id = 3);


CREATE TABLE IF NOT EXISTS public.rpt_vat
(
    report_date			date NOT NULL,
	branch_code         text    COLLATE pg_catalog."default" NOT NULL,
	channel_code		text    COLLATE pg_catalog."default" NOT NULL,
	userrole_code		text    COLLATE pg_catalog."default" NOT NULL,

    vat_rate_id		    integer NOT NULL,
	net_amount		    numeric NOT NULL DEFAULT 0,

	created_ts          timestamp without time zone NOT NULL DEFAULT now(),
    modified_ts         timestamp without time zone NOT NULL DEFAULT now(),
    CONSTRAINT rpt_vat_pk PRIMARY KEY (report_date, branch_code, channel_code, userrole_code, vat_rate_id)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.rpt_vat OWNER TO postgres;


