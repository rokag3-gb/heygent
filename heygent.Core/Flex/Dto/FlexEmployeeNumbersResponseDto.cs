namespace heygent.Core.Flex.Dto;

public class FlexEmployeeNumbersResponseDto
{
    public List<string> employeeNumbers { get; set; } = new();
    public string? nextPageKey { get; set; }
    public bool hasNext { get; set; }
}