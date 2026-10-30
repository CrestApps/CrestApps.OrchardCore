using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Telnyx.Endpoints;

/// <summary>
/// The carrier's numbers over the AI API: search the numbers for sale, buy them (and add each as a channel address),
/// list and release the account's numbers, and set the caller name a number shows.
/// </summary>
internal static class TelnyxNumbersApiEndpoints
{
    public static IEndpointRouteBuilder MapTelnyxNumbersApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("api/ai/v1/carrier-numbers")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AIApiKeys.ApiSchemes })
            .DisableAntiforgery()
            .WithTags("Phone Numbers");

        group.MapGet("available", SearchAsync)
            .WithName("SearchCarrierNumbers")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Searches the carrier's numbers for sale by country, area code, city, state, digits and features.");

        group.MapPost("orders", OrderAsync)
            .WithName("OrderCarrierNumbers")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Buys numbers on the voice connection, and adds each as a channel address when asked. Billed by the carrier.");

        group.MapGet(string.Empty, ListAsync)
            .WithName("ListCarrierNumbers")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the numbers the carrier account has.");

        group.MapDelete("{numberId}", ReleaseAsync)
            .WithName("ReleaseCarrierNumber")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Releases a number: it stops working at once and anyone can buy it.");

        group.MapPut("{numberId}/caller-name", SetCallerNameAsync)
            .WithName("SetCarrierNumberCallerName")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Sets the caller name (CNAM) the number shows, at most 15 letters, digits and spaces; empty stops it.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<TelnyxAvailableNumber>>, ProblemHttpResult>> SearchAsync(
        string countryCode,
        string areaCode,
        string city,
        string region,
        string contains,
        bool? sms,
        int? limit,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        TelnyxApiClient client)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Numbers, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbidden();
        }

        var (result, numbers) = await client.SearchAvailableNumbersAsync(new TelnyxNumberSearch
        {
            CountryCode = countryCode,
            AreaCode = areaCode,
            City = city,
            Region = region,
            Contains = contains,
            Features = sms == true ? ["voice", "sms"] : ["voice"],
            Limit = limit ?? 20,
        }, httpContext.RequestAborted);

        return result.Succeeded ? TypedResults.Ok(numbers) : CarrierFailed(result);
    }

    private static async Task<Results<Ok<ApiCarrierOrder>, ValidationProblem, ProblemHttpResult>> OrderAsync(
        ApiCarrierOrderRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        TelnyxApiClient client)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Numbers, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbidden();
        }

        var numbers = (request?.PhoneNumbers ?? []).Where(number => !string.IsNullOrWhiteSpace(number)).Select(number => number.Trim()).Distinct(StringComparer.Ordinal).ToList();

        if (numbers.Count is 0 or > 20)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["phoneNumbers"] = ["Give between 1 and 20 numbers from a search."] });
        }

        var (result, orderId, status) = await client.OrderNumbersAsync(numbers, request.ConnectionId, request.MessagingProfileId, httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return CarrierFailed(result);
        }

        var added = new List<string>();

        if (request.AddAsAddresses && httpContext.RequestServices.GetService<ICatalogManager<OmnichannelChannelEndpoint>>() is { } endpoints)
        {
            foreach (var number in numbers)
            {
                var endpoint = await endpoints.NewAsync(cancellationToken: httpContext.RequestAborted);
                endpoint.DisplayText = number;
                endpoint.Value = number;
                endpoint.Channel = OmnichannelConstants.Channels.Phone;
                endpoint.Capabilities = string.IsNullOrWhiteSpace(request.MessagingProfileId)
                    ? [OmnichannelConstants.Channels.Phone]
                    : [OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms];
                endpoint.ProviderName = TelnyxConstants.ProviderTechnicalName;

                if ((await endpoints.ValidateAsync(endpoint, httpContext.RequestAborted)).Succeeded)
                {
                    await endpoints.CreateAsync(endpoint, httpContext.RequestAborted);
                    added.Add(endpoint.ItemId);
                }
            }
        }

        return TypedResults.Ok(new ApiCarrierOrder(orderId, status, added));
    }

    private static async Task<Results<Ok<IReadOnlyList<TelnyxOwnedNumber>>, ProblemHttpResult>> ListAsync(
        int? page,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        TelnyxApiClient client,
        TelnyxCallerNumberReputation reputation)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Numbers, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbidden();
        }

        var (result, numbers) = await client.ListOwnedNumbersAsync(page ?? 1, 100, httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return CarrierFailed(result);
        }

        await reputation.ApplyToAsync(numbers);

        return TypedResults.Ok(numbers);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ReleaseAsync(
        string numberId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        TelnyxApiClient client)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Numbers, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbidden();
        }

        var result = await client.ReleaseNumberAsync(numberId, httpContext.RequestAborted);

        return result.Succeeded ? TypedResults.NoContent() : CarrierFailed(result);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> SetCallerNameAsync(
        string numberId,
        ApiCallerNameRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        TelnyxApiClient client)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Numbers, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbidden();
        }

        var result = await client.SetCallerNameAsync(numberId, request?.CallerName, httpContext.RequestAborted);

        return result.Succeeded ? TypedResults.NoContent() : CarrierFailed(result);
    }

    private static ProblemHttpResult CarrierFailed(TelnyxApiResult result)
        => TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "The carrier refused the request", detail: $"Telnyx answered {(int?)result.StatusCode}: {result.ErrorBody}");

    private static ProblemHttpResult Forbidden()
        => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
}

/// <summary>
/// Numbers to buy.
/// </summary>
public sealed class ApiCarrierOrderRequest
{
    /// <summary>Gets or sets the numbers, from a search.</summary>
    public List<string> PhoneNumbers { get; set; } = [];

    /// <summary>Gets or sets the voice connection; empty uses the configured one.</summary>
    public string ConnectionId { get; set; }

    /// <summary>Gets or sets the messaging profile, so the numbers can text.</summary>
    public string MessagingProfileId { get; set; }

    /// <summary>Gets or sets a value indicating whether each number is added as a channel address.</summary>
    public bool AddAsAddresses { get; set; } = true;
}

/// <summary>
/// The caller name a number shows.
/// </summary>
public sealed class ApiCallerNameRequest
{
    /// <summary>Gets or sets the name.</summary>
    public string CallerName { get; set; }
}

/// <summary>
/// A number order.
/// </summary>
/// <param name="OrderId">The carrier's order.</param>
/// <param name="Status">The order's status, such as pending or success.</param>
/// <param name="AddressIds">The channel addresses added.</param>
public sealed record ApiCarrierOrder(string OrderId, string Status, IReadOnlyList<string> AddressIds);
