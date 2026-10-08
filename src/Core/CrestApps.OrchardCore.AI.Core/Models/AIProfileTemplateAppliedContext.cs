using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Carries a profile built from a template to the <see cref="IAIProfileTemplateApplicationHandler"/>
/// implementations, just before that profile is persisted.
/// </summary>
public sealed class AIProfileTemplateAppliedContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateAppliedContext"/> class.
    /// </summary>
    /// <param name="profile">The profile built from the template.</param>
    /// <param name="template">The template the profile was built from.</param>
    public AIProfileTemplateAppliedContext(AIProfile profile, AIProfileTemplate template)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(template);

        Profile = profile;
        Template = template;
    }

    /// <summary>
    /// Gets the profile built from the template. It already has its identifier.
    /// </summary>
    public AIProfile Profile { get; }

    /// <summary>
    /// Gets the template the profile was built from.
    /// </summary>
    public AIProfileTemplate Template { get; }
}
