using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Customers.AI;

/// <summary>
/// Gives a customer loyalty points, or takes them away, as the customer page's Rewards card does: the same checks
/// (<see cref="CustomerBalanceRules.CheckPoints"/>: only a customer with an account, points that are not zero and a
/// reason), the same change under the checkout's lock (<see cref="LoyaltyService.ChangeAsync"/>, as an adjustment), and
/// the same record: the reason and the caller's user name are kept with the points' activity. Points never go below
/// zero, so the change made can be smaller than the one asked for; the result says what changed. It needs
/// <see cref="CustomerPermissions.ManageCustomers"/> and asks for approval.
/// </summary>
internal sealed class AdjustCustomerLoyaltyPointsTool : CustomersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "adjustCustomerLoyaltyPoints";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "customerId": {
          "type": "string",
          "description": "The customer's id, from listCustomers. Only a customer with an account earns points."
        },
        "points": {
          "type": "integer",
          "description": "The points to give, or a negative number to take away, such as 500 or -100. It cannot be zero."
        },
        "reason": {
          "type": "string",
          "description": "Why the points change, such as 'Survey reward'. It is kept with the points' activity."
        }
      },
      "required": ["customerId", "points", "reason"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds or removes loyalty points for a customer, with a reason shown in the points history. Points never go below zero.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => CustomerPermissions.ManageCustomers;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (customer, error) = await FindCustomerAsync(services, arguments, cancellationToken);

        if (customer is null)
        {
            return error;
        }

        var points = ReadInt(arguments, "points");
        var reason = ReadString(arguments, "reason");

        switch (CustomerBalanceRules.CheckPoints(customer, points, reason))
        {
            case CustomerBalanceProblem.NoAccount:
                return "Nothing was changed: only customers with an account can be given points.";
            case CustomerBalanceProblem.NoAmount:
                return "Nothing was changed: give a whole number of points to give, or a negative number to take away.";
            case CustomerBalanceProblem.NoReason:
                return "Nothing was changed: say why the points change. It's kept with their activity.";
        }

        var account = await services.GetRequiredService<LoyaltyService>().ChangeAsync(customer.ItemId, points.Value, LoyaltyActivityKind.Adjusted, reason, GetActor(caller), cancellationToken: cancellationToken);

        if (account is null)
        {
            return points < 0 && await services.GetRequiredService<ILoyaltyStore>().FindByCustomerAsync(customer.ItemId, cancellationToken) is null
                ? "Nothing was changed: the customer has no points to take away."
                : "Nothing was changed: the customer's points are being used at a checkout right now. Try again in a moment.";
        }

        var change = account.Activity?.LastOrDefault();
        var changed = change?.Points ?? 0;

        return Json(new
        {
            customerId = customer.ItemId,
            customer = customer.GetName(),
            asked = points.Value,
            changed,
            points = account.Points,
            reason,
            recordedBy = change?.Actor,
            when = await LocalTimeAsync(services, change?.CreatedUtc),
            message = changed == points.Value
                ? $"The customer now has {account.Points} points."
                : $"Points never go below zero, so {Math.Abs(changed)} points were taken away instead of {Math.Abs(points.Value)}. The customer now has {account.Points} points.",
        });
    }
}
