using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Common.Dtos
{
    public class AccountTypesResponseDto
    {
        public required IReadOnlyList<AccountType> AccountTypes { get; init; }

        // The one the app should preselect. Not a default the server applies — nothing happens
        // until the user confirms.
        public required AccountType Suggested { get; init; }
    }
}
