# heygent

`heygent`는 지정된 경로의 파일을 주기적으로 동기화하고 결과를 알림으로 보내주는 .NET 8 기반 콘솔 애플리케이션입니다. 로컬 파일 시스템, Azure Blob Storage 등 다양한 파일 소스를 지원하며, 설정 파일(YAML)로 동작을 제어합니다.

## 주요 기능

- 주기적 파일 동기화(Cron 표현식 기반)
- YAML 기반 유연한 설정
- 로컬 및 Azure Blob(SFTP) 지원
- 작업 결과 알림 전송(Lark 등)
- 확장 가능한 구조(의존성 주입 기반)
- 모니터링 및 자동 복구(`heygent.Awaker`)

## 기술 스택

- .NET 8
- Cronos (Cron 파싱)
- Microsoft.Extensions.Hosting (호스팅/DI)
*** End Patch
│   ├── Notification/         # 알림 관련 서비스
│   ├── SFTP/                 # SFTP 연동 로직
│   └── ...
└── heygent.Awaker/         # heygent 프로세스 상태 감시 및 재실행
```

## 동작 방식

heygent는 다음과 같은 순서로 동작합니다。

1. **설정 로드**: 애플리케이션 실행 시 `heygentConfig.yaml` 파일을 읽어 스케줄, 동기화 대상, 알림 등 모든 설정을 불러옵니다。
2. **스케줄러 실행**: `CronPollingService`가 설정된 Cron 주기에 맞춰 백그라운드에서 주기적으로 실행됩니다。
3. **파일 감시 및 동기화**: 지정된 소스 경로에서 변경 사항을 감지하고, 대상 스토리지로 파일을 동기화합니다。
4. **결과 알림**: 동기화 작업이 완료되면 그 결과를 설정된 알림 채널을 통해 전송합니다。
5. **상태 감시 및 자동 복구 (by heygent.Awaker)**：
   - `heygent.Awaker`는 설정된 간격(`ping_interval_min`)마다 Named Pipe(IPC)를 통해 `heygent` 프로세스에게 'Ping' 메시지를 보냅니다。
   - `heygent`는 'Ping'을 받으면 'Pong' 메시지로 응답하여 자신의 동작 상태를 알립니다。
   - 만약 `heygent.Awaker`가 정해진 시간 내에 'Pong' 응답을 받지 못하면, `heygent` 프로세스가 비정상적으로 종료된 것으로 간주하고 즉시 재시작하여 서비스 다운타임을 최소화합니다。

## 시작하기

### 설정

1. 프로젝트를 클론하거나 다운로드합니다。
2. `heygentConfig.Example.yaml` 파일을 복사하여 `heygentConfig.yaml` 파일을 생성합니다。
3. 자신의 환경에 맞게 `heygentConfig.yaml` 파일의 내용을 수정합니다。

**설정 예시 (`heygentConfig.yaml`)**
# heygent

간단: README에서 이전 명칭을 현재 명칭인 'heygent'로 통일했습니다.

프로젝트 관련 자세한 내용은 코드와 각 프로젝트 폴더의 문서를 참고하세요.

---
MIT


## 라이선스

이 프로젝트는 MIT 라이선스를 따릅니다.

---
`eod`