using System.Text;

namespace heygent.Core.Notification;

public static class SlackCardTemplates
{
    /// <summary>
    /// 기능 6) 일일 근무 현황 템플릿
    /// </summary>
    public static List<SlackAttachment> GetDailyWorkStatus(DateTime date, string deptName, int officeCount, int remoteCount, int leaveCount, int etcCount, List<string> leaveNames)
    {
        var title = $"📅 {date:MM/dd (ddd)} {deptName} 근무 현황";
        
        var blocks = new List<SlackBlock>
        {
            new SlackBlock
            {
                Type = "header",
                Text = new SlackTextObject { Type = "plain_text", Text = title, Emoji = true }
            },
            new SlackBlock { Type = "divider" },
            new SlackBlock
            {
                Type = "section",
                Fields = new List<SlackTextObject>
                {
                    new SlackTextObject { Type = "mrkdwn", Text = $"*🏢 On-site Work*\n{officeCount}" },
                    new SlackTextObject { Type = "mrkdwn", Text = $"*🏠 Remote Work*\n{remoteCount}" },
                    new SlackTextObject { Type = "mrkdwn", Text = $"*🏖️ OOO (Out of Office)*\n{leaveCount}" },
                    new SlackTextObject { Type = "mrkdwn", Text = $"*🏠 Field Work*\n{0}" },
                    new SlackTextObject { Type = "mrkdwn", Text = $"*🏠 Business Trip*\n{0}" },
                    new SlackTextObject { Type = "mrkdwn", Text = $"*💤 On extended PTO or other leave*\n{etcCount}" }
                }
            }
        };

        if (leaveNames.Any())
        {
            blocks.Add(new SlackBlock { Type = "divider" });
            blocks.Add(new SlackBlock
            {
                Type = "section",
                Text = new SlackTextObject 
                { 
                    Type = "mrkdwn", 
                    Text = $"*🏖️ 휴가자 명단:*\n{string.Join(", ", leaveNames)}" 
                }
            });
        }

        return new List<SlackAttachment>
        {
            new SlackAttachment
            {
                Color = "#36a64f", // Green
                Blocks = blocks
            }
        };
    }

    /// <summary>
    /// 기능 9) 팀원용 연장근무 현황 템플릿
    /// </summary>
    public static List<SlackAttachment> GetWeeklyWorkStatusForMember(string userName, double currentHours, double remainingHours)
    {
        var color = "#36a64f"; // Green
        var statusEmoji = "✅";
        
        // 주 45시간 이상이면 경고 (Yellow)
        if (currentHours >= 45 && currentHours < 50)
        {
            color = "#ecb22e"; // Yellow
            statusEmoji = "⚠️";
        }
        // 주 50시간 이상이면 위험 (Red)
        else if (currentHours >= 50)
        {
            color = "#e01e5a"; // Red
            statusEmoji = "🚨";
        }

        var blocks = new List<SlackBlock>
        {
            new SlackBlock
            {
                Type = "header",
                Text = new SlackTextObject { Type = "plain_text", Text = $"⏰ {userName}님, 금주 근무 현황 알림", Emoji = true }
            },
            new SlackBlock
            {
                Type = "section",
                Fields = new List<SlackTextObject>
                {
                    new SlackTextObject { Type = "mrkdwn", Text = $"*⏱️ 누적 근무 시간*\n{currentHours:F1}시간" },
                    new SlackTextObject { Type = "mrkdwn", Text = $"*⏳ 잔여 가능 시간*\n{remainingHours:F1}시간" },
                }
            },
            new SlackBlock
            {
                Type = "context",
                Elements = new List<object>
                {
                    new SlackTextObject { Type = "mrkdwn", Text = $"{statusEmoji} 주 52시간을 초과하지 않도록 유의해주세요." }
                }
            }
        };

        return new List<SlackAttachment>
        {
            new SlackAttachment
            {
                Color = color,
                Blocks = blocks
            }
        };
    }

    /// <summary>
    /// 기능 9) 매니저용 팀원 누적 근무시간 현황 (표 형태)
    /// </summary>
    public static List<SlackAttachment> GetWeeklyWorkStatusForManager(string teamName, List<(string Name, double Hours)> memberStats)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```");
        sb.AppendLine($"{"이름",-6} | {"누적(h)",-7} | {"상태",-4}");
        sb.AppendLine(new string('-', 25));

        // 근무 시간 많은 순 정렬
        var sortedStats = memberStats.OrderByDescending(x => x.Hours).ToList();

        foreach (var member in sortedStats)
        {
            var status = "✅";
            if (member.Hours >= 50) status = "🚨";
            else if (member.Hours >= 45) status = "⚠️";

            // 이름은 6글자(한글 3글자 가정) 제한하여 정렬 맞춤 시도
            // Slack Code Block 내에서 한글 폭 맞추기가 까다로우므로 탭 대신 공백 패딩 사용
            var name = member.Name.Length > 4 ? member.Name[..4] : member.Name;
            
            // 패딩 로직: 한글은 2칸 차지한다고 가정하고 계산해야 하지만, 간단히 포맷 문자열 사용
            // 단순 정렬이 깨질 수 있으나 최대한 맞춤
            sb.AppendLine($"{name,-6} | {member.Hours,7:F1}  | {status,-2}");
        }
        sb.AppendLine("```");

        var blocks = new List<SlackBlock>
        {
            new SlackBlock
            {
                Type = "header",
                Text = new SlackTextObject { Type = "plain_text", Text = $"📊 {teamName} 금주 누적 근무 현황", Emoji = true }
            },
            new SlackBlock
            {
                Type = "section",
                Text = new SlackTextObject { Type = "mrkdwn", Text = sb.ToString() }
            },
             new SlackBlock
            {
                Type = "context",
                Elements = new List<object>
                {
                    new SlackTextObject { Type = "mrkdwn", Text = "🚨: 50h+ / ⚠️: 45h+ / ✅: 양호" }
                }
            }
        };

        return new List<SlackAttachment>
        {
            new SlackAttachment
            {
                Color = "#2c2d30", // Grey
                Blocks = blocks
            }
        };
    }

    /// <summary>
    /// 장기 미사용 연차 알림 템플릿
    /// </summary>
    public static List<SlackAttachment> GetLongTermNoLeaveAlert(string userName, int noLeaveDays, double totalRate, double teamRate, double userRate)
    {
        var blocks = new List<SlackBlock>
        {
            new SlackBlock
            {
                Type = "header",
                Text = new SlackTextObject { Type = "plain_text", Text = $"👋 {userName}님, 휴가를 안 쓴지 {noLeaveDays}일째!", Emoji = true }
            },
            new SlackBlock
            {
                Type = "section",
                Text = new SlackTextObject 
                { 
                    Type = "mrkdwn", 
                    Text = "업무도 좋지만 적절히 리프레시를 하시어 보는 것은 어떨까요? 🌿" 
                }
            },
            new SlackBlock { Type = "divider" },
            new SlackBlock
            {
                Type = "section",
                Fields = new List<SlackTextObject>
                {
                    new SlackTextObject { Type = "mrkdwn", Text = $"*🏢 전체 임직원 연차소진율*\n{totalRate}%" },
                    new SlackTextObject { Type = "mrkdwn", Text = $"*👥 팀 연차소진율*\n{teamRate}%" },
                    new SlackTextObject { Type = "mrkdwn", Text = $"*👤 {userName}님 연차소진율*\n*{userRate}%*" },
                    new SlackTextObject { Type = "mrkdwn", Text = " " } // 레이아웃 맞춤용 공백
                }
            },
            new SlackBlock
            {
                Type = "actions",
                Elements = new List<object>
                {
                    new SlackButtonElement
                    {
                        Type = "button",
                        Text = new SlackTextObject { Type = "plain_text", Text = "flex 휴가 신청하러 가기 ✈️", Emoji = true },
                        Url = "https://flex.team/time-tracking/my-time-off/dashboard",
                        Style = "primary"
                    }
                }
            }
        };

        return new List<SlackAttachment>
        {
            new SlackAttachment
            {
                Color = "#ecb22e", // Yellow
                Blocks = blocks
            }
        };
    }
}
