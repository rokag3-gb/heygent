namespace heygent.Core.Flex.Dto;

public class FlexDepartmentDto
{
    public string departmentCode { get; set; } = "";
    public string name { get; set; } = "";
    public string? parentDepartmentCode { get; set; }
    public int displayOrder { get; set; }
    public bool visible { get; set; }
    public DateTime? beginDate { get; set; }
    public DateTime? endDate { get; set; }
    public string? sortOrder { get; set; }
}