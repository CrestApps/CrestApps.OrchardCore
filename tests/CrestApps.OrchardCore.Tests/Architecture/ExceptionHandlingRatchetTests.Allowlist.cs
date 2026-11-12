namespace CrestApps.OrchardCore.Tests.Architecture;

public sealed partial class ExceptionHandlingRatchetTests
{
    /// <summary>
    /// The files with broad catches that neither logged, rethrew, returned nor used the exception when the ratchet was
    /// set, with their number. Lower a count or delete an entry as they are fixed; never add one.
    /// </summary>
    private static readonly Dictionary<string, int> _knownSilentCatches = new(StringComparer.Ordinal)
    {
        ["src/Core/CrestApps.OrchardCore.ContactCenter.Core/HealthChecks/ContactCenterNodeServingHealthCheck.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/AnswerProviderCommandTypeExecutor.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/DialerPacingScheduler.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.Core/FileStorage/TenantFolder.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.Geo.Core/GeoShape.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.Handover.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.Recovery.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.Telnyx.Core/Services/TelnyxContactCenterVoiceMediaSession.cs"] = 4,
        ["src/Core/CrestApps.OrchardCore.Telnyx.Core/Services/TelnyxRecordingIngestJobStore.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.YesSql.Core/WorkQueues/YesSqlLeasedWorkQueue.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI/Migrations/AIChatSessionPromptDataMigrations.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI/Migrations/AIChatSessionV1PromptDataMigrations.cs"] = 2,
        ["src/Modules/CrestApps.OrchardCore.AI/Providers/CustomLlm/CustomLlmChatClient.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskPjsipCredentialIssuer.cs"] = 6,
        ["src/Modules/CrestApps.OrchardCore.ContactCenter/HealthChecks/ContactCenterBackplaneHealthCheck.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.ContentFields/Drivers/PhoneFieldSettingsDriver.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.EventGrid/Endpoints/AzureEventGridEndpoint.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Reports.Pdf/Services/ReportPdfFontResolver.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Telephony/Services/DefaultTelephonyInteractionStore.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Wizard/Services/WizardResumeCookieManager.cs"] = 1,
        ["src/Startup/CrestApps.OrchardCore.Samples.A2AClient/Pages/Agents.cshtml.cs"] = 1,
        ["src/Startup/CrestApps.OrchardCore.Samples.McpClient/Pages/Prompts.cshtml.cs"] = 1,
        ["src/Startup/CrestApps.OrchardCore.Samples.McpClient/Pages/ResourceTemplates.cshtml.cs"] = 1,
        ["src/Startup/CrestApps.OrchardCore.Samples.McpClient/Pages/Resources.cshtml.cs"] = 1,
        ["src/Startup/CrestApps.OrchardCore.Samples.McpClient/Pages/Tools.cshtml.cs"] = 1,
    };

    /// <summary>
    /// The files that returned an exception's message as a failure or put it into a localized text when the ratchet was
    /// set, with their number. Lower a count or delete an entry as they are fixed; never add one.
    /// </summary>
    private static readonly Dictionary<string, int> _knownExceptionText = new(StringComparer.Ordinal)
    {
        ["src/Core/CrestApps.OrchardCore.DataConnectors.Core/Connectors/Elasticsearch/ElasticsearchDataConnector.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.DataConnectors.Core/Connectors/Http/HttpApiDataConnector.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.DataConnectors.Core/Relational/RelationalDataConnector.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.Reports.Core/Designer/ReportQueryPlanner.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI/Controllers/AIVoiceLibraryController.cs"] = 3,
        ["src/Modules/CrestApps.OrchardCore.ContactCenter/ModelBinders/IvrFlowJsonModelBinder.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.DataConnectors/Controllers/ConnectedDataSetsController.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.DataConnectors/Controllers/DataConnectionsController.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Marketplace/Controllers/VendorPortalController.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.Sms/Handlers/SmsOmnichannelEventHandler.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Reports/Designer/Recipes/ReportDesignsRecipeStep.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Stripe/Services/StripeCheckoutPaymentProvider.cs"] = 7,
        ["src/Modules/CrestApps.OrchardCore.Stripe/Services/StripeRecurringPaymentProvider.cs"] = 4,
        ["src/Modules/CrestApps.OrchardCore.Stripe/Services/StripeTerminalPaymentProvider.cs"] = 1,
    };
}
