using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Services;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The ticket service depends on the timeline, and the timeline reaches its access providers and the record directory
/// (for mentions and names). So the ticket's timeline access and record source never take the ticket service in their
/// constructors: a site would otherwise fail every request that builds the timeline with a circular dependency.
/// </summary>
public sealed class TicketServiceGraphTests
{
    [Theory]
    [InlineData(typeof(TicketTimelineAccess))]
    [InlineData(typeof(TicketRecordSource))]
    public void WhatTheTimelineReaches_DoesntTakeTheTicketService(Type type)
    {
        // Act
        var parameters = type.GetConstructors().SelectMany(constructor => constructor.GetParameters()).Select(parameter => parameter.ParameterType).ToList();

        // Assert
        Assert.NotEmpty(parameters);
        Assert.DoesNotContain(typeof(ITicketService), parameters);
    }
}
