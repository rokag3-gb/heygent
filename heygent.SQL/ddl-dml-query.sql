
select * from hr.flex_api_log order by id desc limit 100;

--DROP TABLE hr.flex_department;
--delete from hr.flex_department;
select * from hr.flex_department where name like '엔지니어링%'
where visible = true;

select * from hr.flex_department_head;

select * from hr.flex_jobRoleCode;
select * from hr.flex_jobRankCode;
select * from hr.flex_jobTitleCode;

--drop table hr.flex_employee;
--delete from hr.flex_employee;
select * from hr.flex_employee where employee_number = '225646' order by employee_number;

select * from hr.flex_employee
where company_leave_date is null
and name = '김두한'
and (primary_department_name = '파생트레이딩플랫폼팀'
     or primary_department_name like '코어%'
    or primary_department_name like '프로덕트엔지니어링팀%'
    or primary_department_name like '테크본부%'
    )
order by company_group_join_date;

/*
create table hr.tbl_test1
(
    col1 BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    /*
    col1 bigserial generated always as identity
        constraint tbl_test1_pk
       primary key,
    */
    col2 varchar(100)
);

select * from hr.tbl_test1;

insert into hr.tbl_test1 (col2) values ('테스트3');

update hr.tbl_test1
set col2 = 'Test2'
where col1 = 2;

delete from hr.tbl_test1 where col1 = 3;

select * from hr.flex_employee;

insert into hr.flex_employee (user_id, name, email, employee_number, organization_code)
values ('227699', '홍길동', 'jwoo.kim@nextsecurities.com', 'employee_number', 'organization_code');
*/