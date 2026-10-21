using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Stripe.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Security.Permissions;
using Stripe;
using Stripe.Terminal;

namespace CrestApps.OrchardCore.Stripe.Api;

/// <summary>
/// The Stripe Terminal readers a till uses: listing them, pairing a new one to a store's address, and, in test mode,
/// presenting a test card on a simulated reader.
/// </summary>
internal static class StripeTerminalApiEndpoints
{
    // The Point of Sale module's permissions, by name: what lets staff sell at a register and manage registers.
    private static readonly Permission _sellAtRegister = new("SellAtRegister", "Sell at a register");
    private static readonly Permission _manageRegisters = new("ManageRegisters", "Manage registers and shifts");

    public static IEndpointRouteBuilder MapStripeTerminalApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi("Point of Sale");

        group.MapGet("pos/terminal/readers", ListAsync)
            .WithName("ListStripeReaders")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the store's Stripe Terminal readers, to pick the one at the register.");

        group.MapPost("pos/terminal/readers", RegisterAsync)
            .WithName("RegisterStripeReader")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Pairs a Stripe Terminal reader by the registration code it shows, at a store's address.");

        group.MapPost("pos/terminal/readers/{readerId}/test-card", PresentTestCardAsync)
            .WithName("PresentStripeTestCard")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("In test mode, presents a test card on a simulated reader that's waiting for a payment.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiStripeReader>>, ProblemHttpResult>> ListAsync(HttpContext httpContext, IAuthorizationService authorizationService, StripeClient stripeClient)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, _sellAtRegister))
        {
            return ApiProblems.Forbidden();
        }

        try
        {
            var readers = await new ReaderService(stripeClient).ListAsync(new ReaderListOptions { Limit = 100 }, cancellationToken: httpContext.RequestAborted);

            return TypedResults.Ok<IReadOnlyList<ApiStripeReader>>(readers.Data.Select(ToApi).ToList());
        }
        catch (StripeException exception)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, exception.StripeError?.Message ?? exception.Message);
        }
    }

    private static async Task<Results<Ok<ApiStripeReader>, ValidationProblem, ProblemHttpResult>> RegisterAsync(
        ApiRegisterReaderRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        StripeClient stripeClient)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, _manageRegisters))
        {
            return ApiProblems.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(request?.RegistrationCode))
        {
            return ApiProblems.Validation("registrationCode", "Enter the code the reader shows.");
        }

        if (string.IsNullOrEmpty(request.StripeLocationId) && (string.IsNullOrWhiteSpace(request.Address?.AddressLine1) || string.IsNullOrWhiteSpace(request.Address.City) || string.IsNullOrWhiteSpace(request.Address.Country)))
        {
            return ApiProblems.Validation("address", "Give the store's address, or the Stripe location the reader belongs to.");
        }

        try
        {
            var locationId = request.StripeLocationId;

            // A reader belongs to a location in Stripe; one is made from the store's address when none is named.
            if (string.IsNullOrEmpty(locationId))
            {
                var location = await new LocationService(stripeClient).CreateAsync(new LocationCreateOptions
                {
                    DisplayName = string.IsNullOrWhiteSpace(request.Label) ? request.Address.AddressLine1 : request.Label.Trim(),
                    Address = new AddressOptions
                    {
                        Line1 = request.Address.AddressLine1,
                        Line2 = request.Address.AddressLine2,
                        City = request.Address.City,
                        State = request.Address.Region,
                        PostalCode = request.Address.PostalCode,
                        Country = request.Address.Country,
                    },
                }, cancellationToken: httpContext.RequestAborted);

                locationId = location.Id;
            }

            var reader = await new ReaderService(stripeClient).CreateAsync(new ReaderCreateOptions
            {
                RegistrationCode = request.RegistrationCode.Trim(),
                Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim(),
                Location = locationId,
            }, cancellationToken: httpContext.RequestAborted);

            return TypedResults.Ok(ToApi(reader));
        }
        catch (StripeException exception)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, exception.StripeError?.Message ?? exception.Message);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> PresentTestCardAsync(
        string readerId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOptionsMonitor<StripeOptions> options,
        StripeClient stripeClient)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, _sellAtRegister))
        {
            return ApiProblems.Forbidden();
        }

        // Only a test account has simulated readers and test cards.
        if (options.CurrentValue.IsLive)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.Unsupported, "Test cards are only for a Stripe account in test mode.");
        }

        try
        {
            await new global::Stripe.TestHelpers.Terminal.ReaderService(stripeClient).PresentPaymentMethodAsync(readerId, new global::Stripe.TestHelpers.Terminal.ReaderPresentPaymentMethodOptions(), cancellationToken: httpContext.RequestAborted);

            return TypedResults.NoContent();
        }
        catch (StripeException exception)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, exception.StripeError?.Message ?? exception.Message);
        }
    }

    private static ApiStripeReader ToApi(Reader reader)
        => new(reader.Id, reader.Label, reader.DeviceType, reader.Status, reader.LocationId, reader.Action?.Type, reader.Action?.Status);
}

/// <summary>
/// A Stripe Terminal reader.
/// </summary>
/// <param name="Status"><c>online</c> or <c>offline</c>.</param>
/// <param name="Action">What it's doing, such as <c>process_payment_intent</c>.</param>
/// <param name="ActionStatus">How that's going: <c>in_progress</c>, <c>succeeded</c> or <c>failed</c>.</param>
public sealed record ApiStripeReader(string Id, string Label, string DeviceType, string Status, string LocationId, string Action, string ActionStatus);

public sealed class ApiRegisterReaderRequest
{
    /// <summary>
    /// The code the reader shows; <c>simulated-wpe</c> pairs a simulated reader in test mode.
    /// </summary>
    public string RegistrationCode { get; set; }

    public string Label { get; set; }

    /// <summary>
    /// The Stripe location the reader belongs to; one is made from <see cref="Address"/> when none is named.
    /// </summary>
    public string StripeLocationId { get; set; }

    public ApiAddress Address { get; set; }
}
