CREATE TABLE IF NOT EXISTS public.rpt_branches
(
	branch_code		text    COLLATE pg_catalog."default" NOT NULL,
	descr 				text COLLATE pg_catalog."default" NOT NULL,
    active 				boolean NOT NULL DEFAULT true,

    auth_user_id 		integer NOT NULL,
	created_ts          timestamp without time zone NOT NULL DEFAULT now(),
    modified_ts         timestamp without time zone NOT NULL DEFAULT now(),
    CONSTRAINT rpt_branches_pk PRIMARY KEY (branch_code)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.rpt_branches OWNER TO postgres;


CREATE TABLE IF NOT EXISTS public.rpt_channels
(
	channel_code		text    COLLATE pg_catalog."default" NOT NULL,
	descr 				text COLLATE pg_catalog."default" NOT NULL,
    active 				boolean NOT NULL DEFAULT true,

    auth_user_id 		integer NOT NULL,
	created_ts          timestamp without time zone NOT NULL DEFAULT now(),
    modified_ts         timestamp without time zone NOT NULL DEFAULT now(),
    CONSTRAINT rpt_channels_pk PRIMARY KEY (channel_code)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.rpt_channels OWNER TO postgres;


CREATE TABLE IF NOT EXISTS public.rpt_user_roles
(
	userrole_code		text    COLLATE pg_catalog."default" NOT NULL,
	descr 				text COLLATE pg_catalog."default" NOT NULL,
    active 				boolean NOT NULL DEFAULT true,

    auth_user_id 		integer NOT NULL,
	created_ts          timestamp without time zone NOT NULL DEFAULT now(),
    modified_ts         timestamp without time zone NOT NULL DEFAULT now(),
    CONSTRAINT rpt_user_roles_pk PRIMARY KEY (userrole_code)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.rpt_user_roles OWNER TO postgres;

CREATE TABLE IF NOT EXISTS public.rpt_daily_sales
(
    report_date			date NOT NULL,
	branch_code         text    COLLATE pg_catalog."default" NOT NULL,
	channel_code		text    COLLATE pg_catalog."default" NOT NULL,
	userrole_code		text    COLLATE pg_catalog."default" NOT NULL,

	sales  				numeric NOT NULL DEFAULT 0,
	nr_transactions  	integer NOT NULL DEFAULT 0,

	created_ts          timestamp without time zone NOT NULL DEFAULT now(),
    modified_ts         timestamp without time zone NOT NULL DEFAULT now(),
    CONSTRAINT rpt_daily_sales_pk PRIMARY KEY (report_date, branch_code, channel_code, userrole_code)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;

ALTER TABLE public.rpt_daily_sales OWNER TO postgres;



-- For remote branches - populate POS_CONTROL lookups

insert into public.rpt_channels (channel_code, descr, auth_user_id)
select 'dinein', 'Dine-In', 0
union all select 'takeaway', 'Takeaway', 0
union all select 'delivery', 'Delivery', 0
union all select 'online', 'Online Orders', 0
union all select 'ubereats', 'Uber Eats', 0
union all select 'mrd', 'Mr D Food', 0;

insert into public.rpt_user_roles (userrole_code, descr, auth_user_id)
select 'waiters', 'Waiters', 0
union all select 'cashiers', 'Cashiers', 0
union all select 'managers', 'Managers', 0
union all select 'kitchen', 'Kitchen Staff', 0
union all select 'drivers', 'Drivers', 0;


-- load test branches
insert into public.rpt_branches (branch_code, descr, auth_user_id)
select 'grp1_sandton', 'Sandton', 0
union all select 'grp1_cpt', 'Cape Town CBD', 0
union all select 'grp1_umhlanga', 'Umhlanga', 0
union all select 'grp1_pta', 'Pretoria', 0
union all select 'grp1_jhb', 'Johannesburg', 0
union all select 'grp1_durban', 'Durban', 0;
