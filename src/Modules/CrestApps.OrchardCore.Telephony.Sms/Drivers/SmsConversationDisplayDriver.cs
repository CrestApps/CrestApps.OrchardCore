using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Telephony.Sms.Drivers;

/// <summary>
/// The display-management driver for an <see cref="SmsConversation"/> row in the inbox list. Other modules can
/// attach badges (contact tags, CRM links) by adding shapes to the same display type.
/// </summary>
public sealed class SmsConversationDisplayDriver : DisplayDriver<SmsConversation>
{
    public override IDisplayResult Display(SmsConversation conversation, BuildDisplayContext context)
    {
        return View("SmsConversation_Fields_SummaryAdmin", conversation)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1");
    }
}
