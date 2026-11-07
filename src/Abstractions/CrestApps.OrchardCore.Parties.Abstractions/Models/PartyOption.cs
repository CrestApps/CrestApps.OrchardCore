namespace CrestApps.OrchardCore.Parties.Models;

/// <summary>
/// A party a picker can offer.
/// </summary>
/// <param name="Id">The party's id in its kind.</param>
/// <param name="Name">The party's name.</param>
public sealed record PartyOption(string Id, string Name);
