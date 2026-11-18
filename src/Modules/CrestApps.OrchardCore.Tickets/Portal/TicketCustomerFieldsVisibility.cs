using CrestApps.OrchardCore.ContentFields.Conditions;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// On a customer's form (<see cref="TicketCustomerEditor"/>), the fields customers don't fill in are hidden fields: what
/// was posted for them is dropped and their validation is ignored, so a required internal field (a root cause) never
/// refuses a customer's request with an error they can't see or fix.
/// </summary>
internal sealed class TicketCustomerFieldsVisibility : IContentFieldVisibilityProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TicketCustomerFieldsVisibility(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<IEnumerable<string>> GetHiddenFieldsAsync(ContentItem contentItem, ContentTypeDefinition type)
    {
        if (!TicketCustomerEditor.IsActive(_httpContextAccessor.HttpContext)
            || type is null
            || !type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
        {
            return Task.FromResult(Enumerable.Empty<string>());
        }

        return Task.FromResult(TicketCustomerEditor.HiddenFields(type));
    }
}
