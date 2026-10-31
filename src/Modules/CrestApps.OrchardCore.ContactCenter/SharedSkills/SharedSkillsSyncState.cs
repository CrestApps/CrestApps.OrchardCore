namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Kept on an agent profile: a fingerprint of the proficiencies last matched with the shared catalog, so a save that
/// didn't change the skills (most saves are presence changes) costs nothing.
/// </summary>
internal sealed class SharedSkillsSyncState
{
    public string Fingerprint { get; set; }
}
