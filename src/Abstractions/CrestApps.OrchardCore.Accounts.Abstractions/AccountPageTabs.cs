using System.Globalization;

namespace CrestApps.OrchardCore.Accounts;

/// <summary>
/// The tabs of an account's page, the umbrella over everything that belongs to the account. A module adds a tab, or a
/// card in one, with a content display driver for the account's <c>DetailAdmin</c> display, placed with
/// <see cref="Location(string, string)"/>. Orchard Core groups the shapes by tab, in the tabs' order.
/// </summary>
/// <example>
/// <code>
/// return Initialize&lt;AccountWorkOrdersViewModel&gt;("AccountWorkOrders", model => ...)
///     .Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Service));
/// </code>
/// </example>
public static class AccountPageTabs
{
    /// <summary>
    /// The account at a glance: its members, its locations and the cards other modules add.
    /// </summary>
    public const string Overview = "Overview";

    /// <summary>
    /// The users who sign in for the account. A tab of the account's editor.
    /// </summary>
    public const string Members = "Members";

    /// <summary>
    /// The places the account has. A tab of the account's editor.
    /// </summary>
    public const string Locations = "Locations";

    /// <summary>
    /// The account's contacts.
    /// </summary>
    public const string Contacts = "Contacts";

    /// <summary>
    /// The deals in progress with the account.
    /// </summary>
    public const string Opportunities = "Opportunities";

    /// <summary>
    /// The leads that became the account's contacts.
    /// </summary>
    public const string Leads = "Leads";

    /// <summary>
    /// The calls, messages and tasks with the account's contacts.
    /// </summary>
    public const string Activities = "Activities";

    /// <summary>
    /// The orders placed for the account.
    /// </summary>
    public const string Orders = "Orders";

    /// <summary>
    /// How the account buys as a business: its terms, its buyers and what it owes.
    /// </summary>
    public const string Business = "Business";

    /// <summary>
    /// The invoices the account owes or has paid.
    /// </summary>
    public const string Invoices = "Invoices";

    /// <summary>
    /// The work done for the account: work orders, assets and service agreements.
    /// </summary>
    public const string Service = "Service";

    /// <summary>
    /// Every record the account holds, with the buttons that add one.
    /// </summary>
    public const string Records = "Records";

    private static readonly Dictionary<string, int> _positions = new(StringComparer.Ordinal)
    {
        [Overview] = 0,
        [Members] = 5,
        [Locations] = 6,
        [Contacts] = 10,
        [Opportunities] = 20,
        [Leads] = 30,
        [Activities] = 40,
        [Orders] = 50,
        [Business] = 60,
        [Invoices] = 70,
        [Service] = 80,
        [Records] = 100,
    };

    /// <summary>
    /// Gets where a tab goes among the others. A tab this class doesn't name goes after <see cref="Service"/>.
    /// </summary>
    /// <param name="tab">The tab's name.</param>
    public static int GetPosition(string tab)
        => tab is not null && _positions.TryGetValue(tab, out var position) ? position : 90;

    /// <summary>
    /// Returns the placement of a shape in a tab of the account's page, such as <c>Content:1#Orders;50</c>.
    /// </summary>
    /// <param name="tab">The tab's name, one of this class's constants or a module's own.</param>
    /// <param name="position">The shape's position in the tab.</param>
    public static string Location(string tab, string position = "1")
    {
        ArgumentException.ThrowIfNullOrEmpty(tab);

        return $"Content:{position}#{tab};{GetPosition(tab).ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Returns the placement of a part's editor in a tab of the account's editor, such as <c>Parts:1#Members;20</c>.
    /// </summary>
    /// <param name="tab">The tab's name.</param>
    /// <param name="position">The editor's position in the tab.</param>
    public static string EditorLocation(string tab, string position = "1")
    {
        ArgumentException.ThrowIfNullOrEmpty(tab);

        return $"Parts:{position}#{tab};{GetPosition(tab).ToString(CultureInfo.InvariantCulture)}";
    }
}
