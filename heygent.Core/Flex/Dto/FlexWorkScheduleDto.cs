namespace heygent.Core.Flex.Dto;

public class FlexWorkScheduleDto
{
    public string scheduleId { get; set; } = "";
    public string userId { get; set; } = "";
    public DateTime date { get; set; }
    public string startTime { get; set; } = "";
    public string endTime { get; set; } = "";
    // 필요한 필드 추가
}

