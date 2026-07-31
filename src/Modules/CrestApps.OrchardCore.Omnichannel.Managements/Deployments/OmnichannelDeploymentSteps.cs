namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments;

/// <summary>
/// Names the recipe steps that carry Omnichannel configuration between environments.
/// </summary>
public static class OmnichannelDeploymentSteps
{
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
    /// The recipe step that carries the actions a subject disposition triggers.
    /// </summary>
    public const string SubjectAction = "OmnichannelSubjectAction";
}
