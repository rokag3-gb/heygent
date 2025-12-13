using Newtonsoft.Json;

namespace heygent.Core.Flex.Dto;

public class FlexUserMasterResponseDto
{
    public List<FlexUserMasterDto> users { get; set; } = new();
    
    // Pagination fields might exist, adding them just in case based on other APIs, 
    // but relying on the user provided sample mainly.
    // If the API supports pagination keys like 'cursor' or 'nextPageKey', they should be here.
    // For now, adhering to the sample.
}

public class FlexUserMasterDto
{
    public string employeeNumber { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string? nameInOffice { get; set; }
    public FlexUserEnglishNameDto? englishName { get; set; }
    public string? email { get; set; }
    public List<FlexUserPhoneNumberDto>? phoneNumbers { get; set; }
    public string? ssn { get; set; }
    public string? birthday { get; set; }
    public string? gender { get; set; }
    public string? profileImageUrl { get; set; }
    public string? companyGroupJoinDate { get; set; }
    public string? companyJoinDate { get; set; }
    public string? companyLeaveDate { get; set; }
    public string? employmentContract { get; set; }
    public FlexUserHomeAddressDto? homeAddress { get; set; }
    public FlexUserDepartmentDto? primaryDepartment { get; set; }
    public FlexUserJobRoleDto? primaryJobRole { get; set; }
    public FlexUserJobRankDto? primaryJobRank { get; set; }
    public FlexUserJobTitleDto? primaryJobTitle { get; set; }
    public List<FlexUserCustomPropertyDto>? customProperties { get; set; }
}

public class FlexUserEnglishNameDto
{
    public string? firstName { get; set; }
    public string? lastName { get; set; }
}

public class FlexUserPhoneNumberDto
{
    public string? type { get; set; } // e.g., "PERSONAL"
    public string? value { get; set; }
}

public class FlexUserHomeAddressDto
{
    public string? addressCountry { get; set; }
    public string? addressState { get; set; }
    public string? addressCity { get; set; }
    public string? address1 { get; set; }
    public string? address2 { get; set; }
    public string? address3 { get; set; }
    public string? addressZipCode { get; set; }
}

public class FlexUserDepartmentDto
{
    public string? departmentCode { get; set; }
    public string? parentDepartmentCode { get; set; }
    public string? name { get; set; }
    public int displayOrder { get; set; }
    public string? sortOrder { get; set; }
    public bool visible { get; set; }
    public string? beginDate { get; set; }
    public string? endDate { get; set; }
}

public class FlexUserJobRoleDto
{
    public string? jobRoleCode { get; set; }
}

public class FlexUserJobRankDto
{
    public string? jobRankCode { get; set; }
}

public class FlexUserJobTitleDto
{
    public string? jobTitleCode { get; set; }
}

public class FlexUserCustomPropertyDto
{
    public string? key { get; set; }
    public string? value { get; set; }
}