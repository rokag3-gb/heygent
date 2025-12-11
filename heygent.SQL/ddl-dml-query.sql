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

select * from hr.flex_api_log order by id desc limit 5;

123456789012
Unauthorized

client_id=open-api&grant_type=refresh_token&refresh_token=eyJ38r97fdh8237hf87hd782hd81d1dh18dh128ehdcmqposdk12335
{"access_token":"eyJraWQiOiI2NWYwYmNiZC0yODdjLTRmZjAtODQxNS1kOTdmOWIwNjMwYzkiLCJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.eyJ0eXAiOiJCZWFyZXIiLCJjaWgiOiJyTTBBcDJ5NDhsIiwiZXhwIjoxNzY1NDI4MzA0LCJpYXQiOjE3NjU0Mjc3MDQsIndpaCI6Ijc5MDd5cjVYRWciLCJzaWQiOiI1YjA0MTRmMy03MTM5LTQ4NzYtOTBiMi0xYmM0NzBjM2I0YzYifQ.jNOmjEuGdbbjZwecU_RCswSVlXfeH79BCFcZtS_VeF54-XOdLKXHJZvwDRGetrmo4l5FBJAuLxq1P6s9YJe0Q7oq24Km7rHutwuD9hjnNlGwlM2u12VAt-uNU0Wbzk7FA7AGIjubk_zkZbrCTgSJYPwkO8L4Hm6ikFlBIpuOsXdVYxEullgEVAyaVifMOR2zTcBr4Kpg_wHw4W-MwJYYdwQuHnXasgxyKU2wp-kj7iNv0UAwURWG4aytlqfoTB8feL3CMRK66HX1heUINflsSQEjDTHei_NLIlByejuvlF6fmJLa2e_jZcSG8H5iG_DR2js9ftYQg8rXLg84qV0RzQ","expires_in":600,"refresh_token":"eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJ0eXAiOiJSZWZyZXNoIiwiY2loIjoick0wQXAyeTQ4bCIsImV4cCI6MTc2NjAzMjUwNCwiaWF0IjoxNzY1NDI3NzA0LCJ3aWgiOiI3OTA3eXI1WEVnIiwic2lkIjoiNWIwNDE0ZjMtNzEzOS00ODc2LTkwYjItMWJjNDcwYzNiNGM2In0.DCPEIlK3dR7XXFzWD_uVCLjMRfu9ztl8GTeBugf15VY","refresh_expires_in":604800,"token_type":"Bearer","not-before-policy":0,"session_state":"5b0414f3-7139-4876-90b2-1bc470c3b4c6","scope":"profile email"}
