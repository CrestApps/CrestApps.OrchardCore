namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Remembers, for the life of the tenant, that the Contact Center skills were copied into the shared catalog.
/// </summary>
internal sealed class SharedSkillsCatalogBackfillState
{
    public volatile bool Done;
}
