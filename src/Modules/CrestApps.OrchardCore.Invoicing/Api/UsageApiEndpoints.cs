using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.Api;

/// <summary>
/// Maps the usage endpoint when the Invoicing API and Invoicing Billing features are both on.
/// </summary>
[Feature(InvoicingConstants.Features.Api)]
[RequireFeatures(InvoicingConstants.Features.Billing)]
public sealed class UsageApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapUsageApi();
}

/// <summary>
/// Usage over the commerce JSON API: a system records what a customer used on a meter, once per event id, for a
/// recurring plan to bill.
/// </summary>
internal static class UsageApiEndpoints
{
    public static IEndpointRouteBuilder MapUsageApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(InvoicingApiEndpoints.Tag);

        group.MapPost("usage", RecordAsync)
            .WithName("RecordUsage")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Records usage of a meter (by its code) for a customer (a party such as User:42 or Account:7). Sending an event id again returns the first record and records nothing new.");

        return routes;
    }

    internal static async Task<Results<Ok<ApiUsageRecord>, ValidationProblem, ProblemHttpResult>> RecordAsync(
        ApiUsageRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IUsageService usage)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.ManageInvoices))
        {
            return ApiProblems.Forbidden();
        }

        if (request is null)
        {
            return ApiProblems.Validation("meter", "Send the meter, the customer and the quantity.");
        }

        if (!PartyRef.TryParse(request.Customer?.Trim(), out var party))
        {
            return ApiProblems.Validation("customer", "The customer must be a party such as User:42 or Account:7.");
        }

        var result = await usage.RecordAsync(new UsageRecordRequest
        {
            Meter = request.Meter,
            Party = party,
            Quantity = request.Quantity,
            OccurredUtc = request.OccurredAt?.UtcDateTime,
            EventId = request.EventId,
        }, httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]> { ["usage"] = [.. result.Errors] });
        }

        var record = result.Record;

        return TypedResults.Ok(new ApiUsageRecord(
            record.ItemId,
            record.MeterId,
            record.Party.ToString(),
            record.Quantity,
            new DateTimeOffset(DateTime.SpecifyKind(record.OccurredUtc, DateTimeKind.Utc)),
            record.EventId,
            result.AlreadyRecorded));
    }
}

/// <summary>
/// Usage to record.
/// </summary>
public sealed class ApiUsageRequest
{
    /// <summary>
    /// Gets or sets the meter's code, such as <c>api-calls</c>.
    /// </summary>
    public string Meter { get; set; }

    /// <summary>
    /// Gets or sets whose usage it is: a party such as <c>User:42</c> or <c>Account:7</c>.
    /// </summary>
    public string Customer { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>
    /// Gets or sets when it happened; now when not given.
    /// </summary>
    public DateTimeOffset? OccurredAt { get; set; }

    /// <summary>
    /// Gets or sets the sender's id of the event, which makes sending it again record nothing new.
    /// </summary>
    public string EventId { get; set; }
}

/// <summary>
/// A usage record.
/// </summary>
public sealed record ApiUsageRecord(
    string Id,
    string MeterId,
    string Customer,
    decimal Quantity,
    DateTimeOffset OccurredAt,
    string EventId,
    bool AlreadyRecorded);
