namespace CrestApps.OrchardCore.Parties.Models;

/// <summary>
/// The well-known kinds of <see cref="PartyRef"/>. The kind is an open string, so a module can add its own.
/// </summary>
public static class PartyKinds
{
    /// <summary>
    /// A person who signs in; the id is an Orchard Core user id.
    /// </summary>
    public const string User = "User";

    /// <summary>
    /// A commerce buyer; the id is a Customers module customer record id (not a user id).
    /// </summary>
    public const string Customer = "Customer";

    /// <summary>
    /// An organization; the id is an Accounts module account content item id.
    /// </summary>
    public const string Account = "Account";

    /// <summary>
    /// A CRM contact; the id is a contact content item id.
    /// </summary>
    public const string Contact = "Contact";

    /// <summary>
    /// A buyer who checked out without an account; the id is the guest's id.
    /// </summary>
    public const string Guest = "Guest";
}
