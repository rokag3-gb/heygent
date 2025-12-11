namespace heygent.Core.Flex.Dto;

public class FlexAuthResponseDto
{
    public string access_token { get; set; } = "";
    public int expires_in { get; set; } = 0;
    public int not_before_policy { get; set; } = 0;
    public int refresh_expires_in { get; set; } = 0;
    public string refresh_token { get; set; } = "";
    public string scope { get; set; } = "";
    public string session_state { get; set; } = "";
    public string token_type { get; set; } = "";
}