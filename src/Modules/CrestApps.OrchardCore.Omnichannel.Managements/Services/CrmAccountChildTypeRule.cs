using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Lets contacts and opportunities belong to an account, and keeps leads out of accounts: a lead is not yet anyone's
/// contact, and gets an account when it is converted.
/// </summary>
internal sealed class CrmAccountChildTypeRule : IAccountChildTypeRule
{
    /// <inheritdoc/>
    public bool CanBelongToAccount(ContentTypeDefinition definition)
        => OmnichannelRecordKinds.IsAccountChild(definition);

    /// <inheritdoc/>
    public bool MustNotBelongToAccount(ContentTypeDefinition definition)
        => OmnichannelRecordKinds.HasPart(definition, OmnichannelConstants.ContentParts.Lead);
}
