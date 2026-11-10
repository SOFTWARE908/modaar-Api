using modaar.api.Common.Localization;
using modaar.api.Features.Contracts.Enums;

namespace modaar.api.Features.Contracts;

// The display title on a row in a contract's request history. Derived from the type rather than
// stored, so an existing row picks up a wording change without a data fix.
public static class ContractRequestLabels
{
    public static string Title(ContractRequestType type, IRequestLanguage language) => type switch
    {
        ContractRequestType.Renewal => language.Pick("طلب تجديد العقد", "Renewal request"),
        ContractRequestType.Termination => language.Pick("طلب إنهاء العقد", "Termination request"),
        _ => type.ToString()
    };
}