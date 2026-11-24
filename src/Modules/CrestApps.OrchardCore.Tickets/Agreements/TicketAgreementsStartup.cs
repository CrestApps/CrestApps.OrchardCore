using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.Agreements;

/// <summary>
/// With Agreements on, tickets work under the agreement with their account that covers them: its ticket terms on the
/// agreement editor, its service level for its tickets, and its hours on the ticket's page.
/// </summary>
[RequireFeatures(AgreementsConstants.Features.Agreements)]
public sealed class TicketAgreementsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITicketAgreementResolver, TicketAgreementResolver>();
        services.AddDisplayDriver<Agreement, TicketAgreementTermsDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgreementTermsValidator, TicketAgreementTermsValidator>());
        services.AddContentPart<TicketPart>()
            .UseDisplayDriver<TicketAgreementDisplayDriver>();
    }
}

/// <summary>
/// With Time Tracker on too, the billable time logged on an agreement's tickets counts against its hours: each entry
/// is charged to the agreement its ticket worked under when the time was logged.
/// </summary>
[RequireFeatures(AgreementsConstants.Features.Agreements, TimeTrackerConstants.Features.TimeTracker)]
public sealed class TicketAgreementUsageStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgreementUsageProvider, TicketAgreementUsageProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITimeEntryChargeResolver, TicketTimeChargeResolver>());
    }
}
