using System.Text.Json;
using System.Text.Json.Nodes;

namespace heygent.Core.Notification;

public static class LarkCardTemplates
{
    private const string FlexUrl = "https://flex.team";

    private static string BuildJson(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return node?.ToJsonString() ?? json;
        }
        catch
        {
            return json;
        }
    }

    /// <summary>
    /// 기능 1) 출근/퇴근 알림 메시지
    /// </summary>
    /// <param name="userName">직원 이름</param>
    /// <param name="isCheckIn">true: 출근, false: 퇴근</param>
    /// <param name="averageTime">평균 시간 (예: 09:30)</param>
    public static string GetAttendanceReminder(string userName, bool isCheckIn, string averageTime)
    {
        var typeText = isCheckIn ? "출근" : "퇴근";
        var emoji = isCheckIn ? "☀️" : "🌙";
        var color = isCheckIn ? "blue" : "purple";
        var message = isCheckIn 
            ? $"좋은 아침입니다, **{userName}**님! ☕\\nflex 출근 체크하셨나요?\\n\\n(최근 평균 출근 시간 {averageTime}이 지나도 출근시간이 확인되지 않아서 알려드려요)"
            : $"오늘 하루도 고생 많으셨습니다, **{userName}**님! 🏠\\nflex 퇴근 체크하시고 남은 하루 잘 마무리하세요.\\n\\n(최근 평균 퇴근 시간 {averageTime}이 지나도 퇴근시간이 확인되지 않아서 알려드려요)";
        var flex_url = "https://flex.team/time-tracking/my-work-record";

        return BuildJson($@"{{
  ""config"": {{ ""wide_screen_mode"": true }},
  ""header"": {{
    ""title"": {{ ""tag"": ""plain_text"", ""content"": ""{emoji} flex {typeText} 체크 알림"" }},
    ""template"": ""{color}""
  }},
  ""elements"": [
    {{
      ""tag"": ""div"",
      ""text"": {{ ""tag"": ""lark_md"", ""content"": ""{message}"" }}
    }},
    {{ ""tag"": ""hr"" }},
    {{
      ""tag"": ""action"",
      ""actions"": [
        {{
          ""tag"": ""button"",
          ""text"": {{ ""tag"": ""plain_text"", ""content"": ""👉 flex {typeText}하러 가기"" }},
          ""type"": ""primary"",
          ""url"": ""{flex_url}""
        }}
      ]
    }}
  ]
}}");
    }

    /// <summary>
    /// 기능 4-1) 입사 n주년 기념 메시지
    /// </summary>
    /// <param name="userName">직원 이름 (본인 또는 대상자)</param>
    /// <param name="joinDate">실제 입사일</param>
    /// <param name="years">근속 연수</param>
    /// <param name="department">부서</param>
    /// <param name="jobTitle">직무</param>
    /// <param name="isForManager">true: 매니저에게 보내는 알림, false: 본인 축하 메시지</param>
    public static string GetWorkAnniversary(string userName, DateTime joinDate, int years, string department, string jobTitle, bool isForManager = false)
    {
        var today = DateTime.Today;
        var thisYearAnniversary = new DateTime(today.Year, joinDate.Month, joinDate.Day);
        var dDay = (thisYearAnniversary - today).Days;
        
        // D-Day 텍스트 (예: D-3, D-Day, D+1)
        var dDayText = dDay == 0 ? "D-Day" : (dDay > 0 ? $"D-{dDay}" : $"D+{Math.Abs(dDay)}");
        var joinDateText = joinDate.ToString("yyyy.MM.dd");

        if (isForManager)
        {
            var dayDescription = dDay == 0 ? "오늘" : (dDay > 0 ? $"{dDay}일 뒤" : $"{Math.Abs(dDay)}일 전");

            return BuildJson($@"{{
  ""config"": {{ ""wide_screen_mode"": true }},
  ""header"": {{
    ""title"": {{ ""tag"": ""plain_text"", ""content"": ""📅 팀원 입사기념일 알림 ({dDayText})"" }},
    ""template"": ""orange""
  }},
  ""elements"": [
    {{
      ""tag"": ""div"",
      ""text"": {{
        ""tag"": ""lark_md"",
        ""content"": ""**{dayDescription}**는 **{department}** **{userName}**님의 입사 **{years}주년**입니다.\n따뜻한 축하의 한마디를 준비해보세요! 👏""
      }}
    }},
    {{ ""tag"": ""hr"" }},
    {{
      ""tag"": ""div"",
      ""fields"": [
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""**입사일**:\n{joinDateText}"" }}
        }},
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""**소속**:\n{department}"" }}
        }},
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""**직무**:\n{jobTitle}"" }}
        }}
      ]
    }}
  ]
}}");
        }
        else
        {
            return BuildJson($@"{{
  ""config"": {{ ""wide_screen_mode"": true }},
  ""header"": {{
    ""title"": {{ ""tag"": ""plain_text"", ""content"": ""🎉 입사 {years}주년을 축하합니다!"" }},
    ""template"": ""red""
  }},
  ""elements"": [
    {{
      ""tag"": ""div"",
      ""text"": {{
        ""tag"": ""lark_md"",
        ""content"": ""**{userName}**님, 넥스트증권과 함께해주신 **{years}년**이라는 시간 동안\n**{department}**에서 보여주신 열정에 깊이 감사드립니다. 🏆\n\n앞으로도 멋진 활약을 기대하겠습니다!""
      }}
    }},
    {{ ""tag"": ""hr"" }},
    {{
      ""tag"": ""div"",
      ""fields"": [
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""**입사일**:\n{joinDateText}"" }}
        }},
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""**소속**:\n{department}"" }}
        }},
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""**직무**:\n{jobTitle}"" }}
        }}
      ]
    }}
  ]
}}");
        }
    }

    /// <summary>
    /// 기능 4-2) 생일 축하 메시지 + 기프트카드 (본인용)
    /// </summary>
    public static string GetBirthdayMessage(string userName, DateTime birthDate)
    {
        // 기프트카드 URL은 예시입니다. 실제 URL이나 이미지 키로 대체 필요
        return BuildJson($@"{{
  ""config"": {{ ""wide_screen_mode"": true }},
  ""header"": {{
    ""title"": {{ ""tag"": ""plain_text"", ""content"": ""🎂 생일 축하합니다!"" }},
    ""template"": ""wathet""
  }},
  ""elements"": [
    {{
      ""tag"": ""div"",
      ""text"": {{
        ""tag"": ""lark_md"",
        ""content"": ""**{userName}**님, {birthDate.ToString("MM/dd")} 생일을 진심으로 축하드립니다! 🥳\n행복하고 즐거운 하루 보내세요.""
      }}
    }},
    {{ ""tag"": ""hr"" }},
    {{
      ""tag"": ""div"",
      ""text"": {{
        ""tag"": ""lark_md"",
        ""content"": ""🎁 **생일 선물 도착**\n회사가 준비한 작은 선물을 확인해보세요!""
      }}
    }},
    {{
      ""tag"": ""action"",
      ""actions"": [
        {{
          ""tag"": ""button"",
          ""text"": {{ ""tag"": ""plain_text"", ""content"": ""🎁 기프트카드 확인하기"" }},
          ""type"": ""primary"",
          ""url"": ""https://www.starbucks.co.kr/""
        }}
      ]
    }}
  ]
}}");
    }

    /// <summary>
    /// 기능 4-2) 팀원 생일 알림 (조직장용)
    /// </summary>
    public static string GetBirthdayForManager(string employeeName, DateTime birthday)
    {
        return BuildJson($@"{{
  ""config"": {{ ""wide_screen_mode"": true }},
  ""header"": {{
    ""title"": {{ ""tag"": ""plain_text"", ""content"": ""📅 팀원 생일 알림"" }},
    ""template"": ""yellow""
  }},
  ""elements"": [
    {{
      ""tag"": ""div"",
      ""text"": {{
        ""tag"": ""lark_md"",
        ""content"": ""내일은 **{employeeName}**님의 생일입니다! 🎂\\n팀원들과 함께 축하해주세요.""
      }}
    }},
    {{
      ""tag"": ""div"",
      ""text"": {{
        ""tag"": ""lark_md"",
        ""content"": ""생일: {birthday:MM월 dd일}""
      }}
    }}
  ]
}}");
    }

    /// <summary>
    /// 기능 6) 팀 단위 오늘 근무현황
    /// </summary>
    public static string GetDailyTeamStatus(string teamName, int wfh, int halfOffAm, int halfOffPm, int pto, int outside)
    {
        int totalAway = wfh + halfOffAm + halfOffPm + pto + outside;

        return BuildJson($@"{{
  ""config"": {{ ""wide_screen_mode"": true }},
  ""header"": {{
    ""title"": {{ ""tag"": ""plain_text"", ""content"": ""📊 {teamName} 오늘 근무 현황"" }},
    ""template"": ""blue""
  }},
  ""elements"": [
    {{
      ""tag"": ""div"",
      ""text"": {{
        ""tag"": ""lark_md"",
        ""content"": ""현재 자리에 없는 팀원은 총 **{totalAway}명**입니다.""
      }}
    }},
    {{ ""tag"": ""hr"" }},
    {{
      ""tag"": ""div"",
      ""fields"": [
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""🏠 **재택근무:**\\n{wfh}명"" }}
        }},
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""🏖️ **연차:**\\n{pto}명"" }}
        }},
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""🌓 **오전반차:**\\n{halfOffAm}명"" }}
        }},
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""🌗 **오후반차:**\\n{halfOffPm}명"" }}
        }},
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""🏢 **외근/출장:**\\n{outside}명"" }}
        }}
      ]
    }},
    {{
      ""tag"": ""note"",
      ""elements"": [
        {{ ""tag"": ""plain_text"", ""content"": ""업데이트 시각: {DateTime.Now:HH:mm}"" }}
      ]
    }}
  ]
}}");
    }

    /// <summary>
    /// 기능 9) 금주 누적근무시간 알림
    /// </summary>
    public static string GetWeeklyWorkHours(string userName, double currentHours, double remainingHours)
    {
        // 40시간 기준 or 52시간 기준 등 정책에 따라 색상 변경 가능
        string color = remainingHours < 0 ? "red" : (remainingHours < 5 ? "orange" : "green");
        
        return BuildJson($@"{{
  ""config"": {{ ""wide_screen_mode"": true }},
  ""header"": {{
    ""title"": {{ ""tag"": ""plain_text"", ""content"": ""⏱️ 금주 누적 근무시간 알림"" }},
    ""template"": ""{color}""
  }},
  ""elements"": [
    {{
      ""tag"": ""div"",
      ""text"": {{
        ""tag"": ""lark_md"",
        ""content"": ""**{userName}**님의 이번 주 근무 기록입니다.""
      }}
    }},
    {{ ""tag"": ""hr"" }},
    {{
      ""tag"": ""div"",
      ""fields"": [
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""**누적 근무시간:**\\n{currentHours:F1} 시간"" }}
        }},
        {{
          ""is_short"": true,
          ""text"": {{ ""tag"": ""lark_md"", ""content"": ""**남은 시간 (40H):**\\n{remainingHours:F1} 시간"" }}
        }}
      ]
    }},
    {{
      ""tag"": ""action"",
      ""actions"": [
        {{
          ""tag"": ""button"",
          ""text"": {{ ""tag"": ""plain_text"", ""content"": ""상세 내역 확인"" }},
          ""type"": ""default"",
          ""url"": ""{FlexUrl}""
        }}
      ]
    }}
  ]
}}");
    }
}

