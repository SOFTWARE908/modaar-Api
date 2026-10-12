using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Common.Dtos
{
    public class SelectRoleRequestDto
    {
        public required AccountType AccountType { get; init; }
    }
}
