using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Recipes;

// Each schema is registered under the same feature that registers the recipe step it describes, so a tenant never
// advertises a step it cannot run, and never runs a step that no schema describes.

/// <summary>
/// Registers recipe step schemas for the Contact Center base feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.ContactCenter")]
public sealed class ContactCenterRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ContactCenterVoiceMediaRecipeStep>();
        services.AddScoped<ISiteSettingsSchemaDefinition, ContactCenterExternalTransferSettingsSchema>();
    }
}

/// <summary>
/// Registers site settings schemas for the Contact Center call recording feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.ContactCenter.Recording")]
public sealed class ContactCenterRecordingRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ContactCenterRecordingSettingsSchema>();
    }
}

/// <summary>
/// Registers site settings schemas for the Contact Center secure capture feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.ContactCenter.SecureCapture")]
public sealed class ContactCenterSecureCaptureRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, SecureCaptureSettingsSchema>();
    }
}

/// <summary>
/// Registers recipe step schemas for the Contact Center queues feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.ContactCenter.Queues")]
public sealed class ContactCenterQueuesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ContactCenterSkillRecipeStep>();
        services.AddScoped<IRecipeStep, ContactCenterQueueGroupRecipeStep>();
        services.AddScoped<IRecipeStep, ContactCenterQueueRecipeStep>();
    }
}

/// <summary>
/// Registers recipe step schemas for the Contact Center business hours feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.ContactCenter.BusinessHours")]
public sealed class ContactCenterBusinessHoursRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ContactCenterBusinessHoursCalendarRecipeStep>();
    }
}

/// <summary>
/// Registers recipe step schemas for the Contact Center agent entitlements feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.ContactCenter.AgentEntitlements")]
public sealed class ContactCenterAgentEntitlementsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ContactCenterAgentEntitlementRecipeStep>();
    }
}

/// <summary>
/// Registers recipe step schemas for the Contact Center agents feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.ContactCenter.Agents")]
public sealed class ContactCenterAgentsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AgentStateReasonCodeRecipeStep>();
    }
}

/// <summary>
/// Registers recipe step schemas for the Contact Center inbound voice feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.ContactCenter.InboundVoice")]
public sealed class ContactCenterInboundVoiceRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ContactCenterEntryPointRecipeStep>();
    }
}

/// <summary>
/// Registers recipe step schemas for the Contact Center dialer feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.ContactCenter.Dialer")]
public sealed class ContactCenterDialerRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ContactCenterDialerProfileRecipeStep>();
    }
}

/// <summary>
/// Registers recipe step schemas for the Omnichannel activities feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Omnichannel.Activities")]
public sealed class OmnichannelActivitiesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, OmnichannelCampaignGroupRecipeStep>();
        services.AddScoped<IRecipeStep, OmnichannelCampaignRecipeStep>();
        services.AddScoped<IRecipeStep, OmnichannelChannelEndpointRecipeStep>();
        services.AddScoped<IRecipeStep, OmnichannelDispositionRecipeStep>();
        services.AddScoped<IRecipeStep, OmnichannelCadenceRecipeStep>();
        services.AddScoped<IRecipeStep, OmnichannelSubjectActionRecipeStep>();
    }
}

/// <summary>
/// Registers recipe step schemas for the Omnichannel CRM feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Omnichannel.Crm")]
public sealed class OmnichannelCrmRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, OmnichannelLeadStatusRecipeStep>();
        services.AddScoped<IRecipeStep, OmnichannelOpportunityStageRecipeStep>();
    }
}

/// <summary>
/// Registers recipe step schemas for the Omnichannel messaging workspace feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Omnichannel.Messaging")]
public sealed class OmnichannelMessagingRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, OmnichannelMessageTemplateRecipeStep>();
    }
}

/// <summary>
/// Registers recipe step and site settings schemas for the Telephony feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Telephony")]
public sealed class TelephonyRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TelephonyExtensionRecipeStep>();
        services.AddScoped<ISiteSettingsSchemaDefinition, TelephonySettingsSchema>();
    }
}

/// <summary>
/// Registers site settings schemas for the soft phone widget feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Telephony.SoftPhone")]
public sealed class TelephonySoftPhoneRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, SoftPhoneWidgetSettingsSchema>();
    }
}

/// <summary>
/// Registers recipe step and site settings schemas for the Telnyx provider feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Telnyx")]
public sealed class TelnyxRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TelnyxSettingsRecipeStep>();
        services.AddScoped<ISiteSettingsSchemaDefinition, TelnyxSettingsSchema>();
    }
}

/// <summary>
/// Registers recipe step and site settings schemas for the Telnyx SMS feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Telnyx.Sms")]
public sealed class TelnyxSmsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TelnyxSmsSettingsRecipeStep>();
        services.AddScoped<ISiteSettingsSchemaDefinition, TelnyxSmsSettingsSchema>();
    }
}

/// <summary>
/// Registers recipe step and site settings schemas for the Asterisk provider feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Asterisk")]
public sealed class AsteriskRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AsteriskSettingsRecipeStep>();
        services.AddScoped<ISiteSettingsSchemaDefinition, AsteriskSettingsSchema>();
    }
}
