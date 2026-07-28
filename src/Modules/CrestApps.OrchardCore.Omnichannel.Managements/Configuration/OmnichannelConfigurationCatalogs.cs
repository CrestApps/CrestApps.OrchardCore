namespace CrestApps.OrchardCore.Omnichannel.Managements.Configuration;

/// <summary>
/// Names the Omnichannel configuration catalogs as they appear in recipes and deployment plans.
/// </summary>
public static class OmnichannelConfigurationCatalogs
{
    /// <summary>
    /// The identifier of the catalog group that the Omnichannel deployment step exports.
    /// </summary>
    public const string Group = "Omnichannel";

    /// <summary>
    /// The recipe step that carries dispositions.
    /// </summary>
    public const string Disposition = "OmnichannelDisposition";

    /// <summary>
    /// The recipe step that carries channel endpoints.
    /// </summary>
    public const string ChannelEndpoint = "OmnichannelChannelEndpoint";

    /// <summary>
    /// The recipe step that carries campaign groups.
    /// </summary>
    public const string CampaignGroup = "OmnichannelCampaignGroup";

    /// <summary>
    /// The recipe step that carries campaigns.
    /// </summary>
    public const string Campaign = "OmnichannelCampaign";

    /// <summary>
    /// The recipe step that carries subject flow settings.
    /// </summary>
    public const string SubjectFlowSettings = "OmnichannelSubjectFlowSettings";

    /// <summary>
    /// The recipe step that carries the actions a subject disposition triggers.
    /// </summary>
    public const string SubjectAction = "OmnichannelSubjectAction";
}
