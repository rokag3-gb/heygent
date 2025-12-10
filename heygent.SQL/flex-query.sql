-- 1. hr 스키마에 대한 권한을 flex_app 에 직접 부여
GRANT USAGE, CREATE ON SCHEMA hr TO flex_app;

-- 2. hr 스키마 안의 기존 테이블들에 대한 권한 부여
GRANT SELECT, INSERT, UPDATE, DELETE
ON ALL TABLES IN SCHEMA hr
TO flex_app;

-- 3. 앞으로 hr 스키마에 새로 만들어질 테이블에 대한 기본 권한
ALTER DEFAULT PRIVILEGES IN SCHEMA hr
GRANT SELECT, INSERT, UPDATE, DELETE
ON TABLES TO flex_app;

-- 4. 시퀀스 권한도 같이
GRANT USAGE, SELECT
ON ALL SEQUENCES IN SCHEMA hr
TO flex_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA hr
GRANT USAGE, SELECT
ON SEQUENCES TO flex_app;

SHOW search_path;
ALTER ROLE flex_app SET search_path = hr, public;

-- 패스워드 변경
ALTER USER postgres WITH PASSWORD '{your-Strong-Password-Ever}';