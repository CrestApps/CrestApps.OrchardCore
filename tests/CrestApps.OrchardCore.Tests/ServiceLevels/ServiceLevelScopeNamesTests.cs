using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.ServiceLevels;

/// <summary>
/// The name a scope is shown by is its localized display name, never the text of its localization source (a bug the
/// policy editor once had), and an unknown scope is shown by its stored name.
/// </summary>
public sealed class ServiceLevelScopeNamesTests
{
    [Fact]
    public void EachKindOfPolicy_SaysWhatItIsFor_InTheAddPolicyDialog()
    {
        // Arrange
        IServiceLevelScopeProvider[] scopes =
        [
            new global::CrestApps.OrchardCore.Tickets.Core.Services.TicketServiceLevelScope(global::Moq.Mock.Of<global::CrestApps.Core.Services.ICatalog<global::CrestApps.OrchardCore.Tickets.Models.TicketPriority>>(), Doubles.RecipeRoundTrip.Localizer<global::CrestApps.OrchardCore.Tickets.Core.Services.TicketServiceLevelScope>()),
            new global::CrestApps.OrchardCore.WorkOrders.Core.Services.WorkOrderServiceLevelScope(Doubles.RecipeRoundTrip.Localizer<global::CrestApps.OrchardCore.WorkOrders.Core.Services.WorkOrderServiceLevelScope>()),
        ];

        // Act and assert
        Assert.All(scopes, scope => Assert.NotNull(scope.Description));
    }

    [Fact]
    public void AScope_IsShownByItsLocalizedName()
    {
        var name = ServiceLevelScopeNames.Get(new SourceTextLocalizerFactory(), [new TicketsScope()], "tickets");

        Assert.Equal("Tickets", name);
    }

    [Fact]
    public void AnUnknownScope_IsShownByItsStoredName()
    {
        var name = ServiceLevelScopeNames.Get(new SourceTextLocalizerFactory(), [new TicketsScope()], "Retired");

        Assert.Equal("Retired", name);
    }

    private sealed class TicketsScope : IServiceLevelScopeProvider
    {
        public string Name => "Tickets";

        public LocalizationSource DisplayName => LocalizationSource.Create<TicketsScope>("Tickets");

        public Task<IReadOnlyList<ServiceLevelOption>> GetMetricsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelOption>>([]);

        public Task<IReadOnlyList<ServiceLevelOption>> GetPrioritiesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelOption>>([]);
    }
}
