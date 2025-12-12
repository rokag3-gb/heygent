namespace heygent.Core.Flex.Dto;

public class FlexDepartmentHeadResponseDto
{
    public string departmentCode { get; set; } = "";
    public List<FlexEmployeeNumberDto> directHeadUsers { get; set; } = new();
    public List<FlexEmployeeNumberDto> resolvedHeadUsers { get; set; } = new();
}

public class FlexEmployeeNumberDto
{
    public string employeeNumber { get; set; } = "";
}