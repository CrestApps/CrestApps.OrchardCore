using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// One record of an AI chat report data set: a stored document and the name of the AI profile it belongs to.
/// </summary>
/// <typeparam name="TDocument">The stored document type.</typeparam>
public sealed class AIChatReportRecord<TDocument>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatReportRecord{TDocument}"/> class.
    /// </summary>
    /// <param name="document">The stored document.</param>
    /// <param name="profileName">The display name of the AI profile, when it was asked for and still exists.</param>
    public AIChatReportRecord(TDocument document, string profileName)
    {
        Document = document;
        ProfileName = profileName;
    }

    /// <summary>
    /// Gets the stored document.
    /// </summary>
    public TDocument Document { get; }

    /// <summary>
    /// Gets the display name of the AI profile, or <see langword="null"/>.
    /// </summary>
    public string ProfileName { get; }
}

/// <summary>
/// Resolves the display names of AI profiles for the AI chat data sets.
/// </summary>
internal static class AIChatReportProfileNames
{
    /// <summary>
    /// The technical name of the profile name field.
    /// </summary>
    public const string ProfileNameField = "ProfileName";

    /// <summary>
    /// Loads the display name of every AI profile, keyed by profile identifier, when the query uses the profile name.
    /// </summary>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The names, empty when the query does not use them.</returns>
    public static async Task<IReadOnlyDictionary<string, string>> LoadAsync(
        IAIProfileManager profileManager,
        ReportDataSourceQuery query,
        CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        if (query.Fields is not null && query.Fields.Count > 0 && !query.Fields.Contains(ProfileNameField))
        {
            return names;
        }

        foreach (var profile in await profileManager.GetAllAsync(cancellationToken) ?? [])
        {
            if (!string.IsNullOrEmpty(profile?.ItemId))
            {
                names[profile.ItemId] = profile.ToString();
            }
        }

        return names;
    }

    /// <summary>
    /// Gets the name of a profile.
    /// </summary>
    /// <param name="names">The loaded names.</param>
    /// <param name="profileId">The profile identifier.</param>
    /// <returns>The name, or <see langword="null"/>.</returns>
    public static string Find(IReadOnlyDictionary<string, string> names, string profileId)
    {
        return profileId is not null && names.TryGetValue(profileId, out var name) ? name : null;
    }
}
