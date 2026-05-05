CREATE TABLE IF NOT EXISTS public.vat_rates
(
    vat_rate_id		integer NOT NULL,
	descr           text    COLLATE pg_catalog."default" NOT NULL,
	vat_rate        numeric NOT NULL DEFAULT 0.15,
	active          boolean NOT NULL DEFAULT TRUE,

	created_ts          timestamp without time zone NOT NULL DEFAULT now(),
    modified_ts         timestamp without time zone NOT NULL DEFAULT now(),
    CONSTRAINT vat_rates_pk PRIMARY KEY (vat_rate_id)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.vat_rates OWNER TO postgres;

insert into public.vat_rates (vat_rate_id, descr, vat_rate, active)
select 1, 'Standard Rate (15%)', 0.15, true
union all select 2, 'Zero-Rated Items', 0, true
union all select 3, 'Exempt Items', 0, true;


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


