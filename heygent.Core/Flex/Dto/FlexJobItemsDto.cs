namespace heygent.Core.Flex.Dto;

public class FlexJobItemsResponseDto
{
    public List<FlexJobRoleDto> jobRoles { get; set; } = new();
    public List<FlexJobRankDto> jobRanks { get; set; } = new();
    public List<FlexJobTitleDto> jobTitles { get; set; } = new();
}

public class FlexJobRoleDto
{
    public string jobRoleCode { get; set; } = "";
    public string name { get; set; } = "";
}

public class FlexJobRankDto
{
    public string jobRankCode { get; set; } = "";
    public string name { get; set; } = "";
}

public class FlexJobTitleDto
{
    public string jobTitleCode { get; set; } = "";
    public string name { get; set; } = "";
}
