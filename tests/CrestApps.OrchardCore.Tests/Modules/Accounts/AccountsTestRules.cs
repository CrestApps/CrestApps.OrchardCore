using CrestApps.OrchardCore.Accounts.Services;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Accounts;

/// <summary>
/// A child-type rule for the tests: a type with <c>MemberPart</c> belongs to an account, one with <c>ProspectPart</c>
/// never does.
/// </summary>
internal sealed class AccountsTestRules : IAccountChildTypeRule
{
    public const string MemberPart = "MemberPart";

    public const string ProspectPart = "ProspectPart";

    public bool CanBelongToAccount(ContentTypeDefinition definition)
        => AccountRecordKinds.HasPart(definition, MemberPart);

    public bool MustNotBelongToAccount(ContentTypeDefinition definition)
        => AccountRecordKinds.HasPart(definition, ProspectPart);
}
