using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using CrestApps.OrchardCore.Users;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Transactions.Drivers;

/// <summary>
/// Builds the row of a transaction in the administration transactions report, for both admin list layouts.
/// </summary>
public sealed class TransactionDisplayDriver : DisplayDriver<Transaction>
{
    private readonly IUserService _userService;
    private readonly IDisplayNameProvider _displayNameProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionDisplayDriver"/> class.
    /// </summary>
    /// <param name="userService">The user service used to resolve transaction owners.</param>
    /// <param name="displayNameProvider">The display name provider used to describe owners.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionDisplayDriver(
        IUserService userService,
        IDisplayNameProvider displayNameProvider,
        IStringLocalizer<TransactionDisplayDriver> stringLocalizer)
    {
        _userService = userService;
        _displayNameProvider = displayNameProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(Transaction transaction, BuildDisplayContext context)
    {
        // A transaction is only displayed as a row of the report; its detail page is a view of its own.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = transaction.Title?.ToLowerInvariant() ?? string.Empty;
        }

        var ownerName = await ResolveOwnerNameAsync(transaction.OwnerId) ?? TransactionOwnerNames.GuestName(transaction, S["{0} (guest)"].Value);

        return Combine(
            View("Transaction_Fields_SummaryAdmin", transaction)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Title:1"),
            View("Transaction_Status_SummaryAdmin", transaction)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            Dynamic("Transaction_Owner_SummaryAdmin", (shape, name) =>
            {
                shape.OwnerName = name;

                return ValueTask.CompletedTask;
            }, ownerName)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Owner:5"),
            View("Transaction_Source_SummaryAdmin", transaction)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Source:5"),
            View("Transaction_Amount_SummaryAdmin", transaction)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:15")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:5"),
            View("Transaction_Due_SummaryAdmin", transaction)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:20")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Due:5"),
            View("Transaction_Buttons_SummaryAdmin", transaction).Location("Actions:5"));
    }

    private async Task<string> ResolveOwnerNameAsync(string ownerId)
    {
        if (string.IsNullOrEmpty(ownerId))
        {
            return null;
        }

        var user = await _userService.GetUserByUniqueIdAsync(ownerId);

        if (user is null)
        {
            return null;
        }

        return await _displayNameProvider.GetAsync(user);
    }
}
