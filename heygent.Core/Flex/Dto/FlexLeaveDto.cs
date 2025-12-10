namespace heygent.Core.Flex.Dto;

public class FlexLeaveDto
{
    public string leaveId { get; set; } = "";
    public string userId { get; set; } = "";
    public string type { get; set; } = ""; // 휴가, 휴직 등
    public DateTime startDate { get; set; }
    public DateTime endDate { get; set; }
    public string status { get; set; } = "";
    // 필요한 필드 추가
}

