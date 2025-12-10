-- 앱 계정 생성 (로그인 가능)
CREATE ROLE flex_app
    LOGIN
    PASSWORD 'next1234!';

-- 권한 전용 롤 생성 (로그인 불가, only Role)
CREATE ROLE flex_rw_role NOLOGIN;

-- flex DB에 대한 기본 PUBLIC 접속권한 제거 (보안)
REVOKE CONNECT ON DATABASE flex FROM PUBLIC;

-- flex_app 계정 -> flex DB 접속 허용
GRANT CONNECT ON DATABASE flex TO flex_app;

-- hr 스키마 생성
CREATE SCHEMA IF NOT EXISTS hr;

-- hr 스키마 사용 권한을 flex_rw_role 에 부여
GRANT USAGE ON SCHEMA hr TO flex_rw_role;

-- 이미 존재하는 hr 스키마 내 모든 테이블에 RW 권한 부여
GRANT SELECT, INSERT, UPDATE, DELETE
ON ALL TABLES IN SCHEMA hr
TO flex_rw_role;

GRANT USAGE, CREATE ON SCHEMA hr TO flex_rw_role;

-- 앞으로 생성될 hr 스키마 테이블에 자동 RW 권한 부여
ALTER DEFAULT PRIVILEGES IN SCHEMA hr
GRANT SELECT, INSERT, UPDATE, DELETE
ON TABLES TO flex_rw_role;

-- hr 스키마 내 시퀀스 권한도 같이 부여 (BIGSERIAL 대비)
GRANT USAGE, SELECT
ON ALL SEQUENCES IN SCHEMA hr
TO flex_rw_role;

ALTER DEFAULT PRIVILEGES IN SCHEMA hr
GRANT USAGE, SELECT
ON SEQUENCES TO flex_rw_role;

-- flex_app 계정에 flex_rw_role 부여
GRANT flex_rw_role TO flex_app;

SELECT has_schema_privilege('flex_app', 'hr', 'USAGE');

SELECT
    has_schema_privilege('flex_app', 'hr', 'USAGE')  AS has_usage,
    has_schema_privilege('flex_app', 'hr', 'CREATE') AS has_create;
