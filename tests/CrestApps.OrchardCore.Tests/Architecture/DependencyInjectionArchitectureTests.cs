using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Guards how code obtains its collaborators. Two habits are refused because both hide a missing registration until
/// run time and in the wrong tenant. The rules began with the Contact Center, Telephony and Omnichannel code and now
/// cover all of <c>src</c> (CQ-7 in <c>docs/engineering/review-2026-11/04-code-quality.md</c>); the types outside that
/// first scope that already took the container are listed in <see cref="_knownServiceLocators"/>, which only shrinks.
/// <para>
/// The first is taking an <see cref="IServiceProvider"/> and resolving from it. A constructor that does this
/// tells the container nothing about what the type needs, so a tenant that has not enabled the feature that
/// supplies the collaborator still constructs the type happily and fails later, deep inside a call, usually on a
/// live interaction.
/// </para>
/// <para>
/// The second is injecting <c>IEnumerable&lt;TService&gt;</c> only to call <c>FirstOrDefault()</c> on it. That is
/// service location wearing a constructor parameter: it silently accepts zero implementations, and it silently
/// picks an arbitrary one when a second feature registers another. A contract that is genuinely optional gets a
/// null-object default registered by the feature that declares it, and is then injected like anything else.
/// </para>
/// Every exception is listed below with the reason it is an exception, because an unexplained exception is
/// indistinguishable from an oversight.
/// </summary>
public sealed class DependencyInjectionArchitectureTests
{
    private const string ServiceProviderRule = "IServiceProvider constructor injection";
    private const string OptionalEnumerableRule = "IEnumerable<T> injected only to take the first";

    /// <summary>
    /// The contracts that now have a null-object default, so no consumer has any reason to scan for them.
    /// </summary>
    private static readonly string[] _contractsWithNullObjectDefaults =
    [
        "IAgentWorkStateHealingService",
        "IBusinessHoursGate",
        "ICallbackService",
        "IDialerProfileReader",
        "IQueuedVoiceWorkOfferService",
    ];

    private static readonly Regex _constructorRegex = new(
        @"public\s+(?<name>[A-Z]\w*)\s*\((?<parameters>[^)]*)\)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex _serviceProviderParameterRegex = new(
        @"\bIServiceProvider\s+\w+",
        RegexOptions.Compiled);

    /// <summary>
    /// The types allowed to take an <see cref="IServiceProvider"/>, and why each one is not service location.
    /// </summary>
    private static readonly DependencyInjectionException[] _serviceProviderExceptions =
    [
        new DependencyInjectionException(
            "ContactCenterScopeExecutor",
            "Its whole purpose is to open a tenant scope and run work in it, so the container is the thing it operates on."),
        new DependencyInjectionException(
            "RealtimeCallCompletionRunner",
            "Like the scope executor, opening a scope and running work in it is the whole of what it does: the call it finishes outlives the request the session ran in."),
        new DependencyInjectionException(
            "ContactCenterFeatureLifecycleHandler",
            "It runs while features are being enabled and disabled, when the set of registered services is exactly what is changing."),
        new DependencyInjectionException(
            "DefaultTelephonyProviderResolver",
            "It activates a provider type chosen at run time from the provider catalog, which is what a container activator is for."),
        new DependencyInjectionException(
            "ProviderVoiceOfferSynchronizationService",
            "It resolves the presence manager and the agent state transition point per synchronization pass, because both record through the event publisher whose handlers construct it."),
        new DependencyInjectionException(
            "ActivityQueueHandler",
            "A queue handler that injected the queue-group manager would close a handler-to-manager-to-handler construction cycle."),
        new DependencyInjectionException(
            "RecordingMediaDeletionHandler",
            "An event handler that injected the event publisher would close a publisher-to-handler construction cycle."),
        new DependencyInjectionException(
            "AutomaticCallRecordingHandler",
            "Starting a recording records an event through the publisher, whose outbox constructs every event handler, so an injected recording service would close a publisher-to-handler construction cycle."),
        new DependencyInjectionException(
            "RecordingDisclosureService",
            "Recording a disclosure records an event through the publisher and may start a recording, and it is injected into the inbound call routing that event handlers reach; since the publisher's outbox constructs every event handler, injecting either would close a publisher-to-handler construction cycle."),
        new DependencyInjectionException(
            "SharedVoicemailProjectionHandler",
            "Filing a message records an event through the publisher, whose outbox constructs every event handler, so an injected filing service would close a publisher-to-handler construction cycle."),
        new DependencyInjectionException(
            "DialerNotInServiceHandler",
            "Completing a dead number's activity runs the disposition handlers, the agent presence manager and the queue service, which all record through the event publisher whose outbox constructs every event handler, so injecting them would close a publisher-to-handler construction cycle."),
        new DependencyInjectionException(
            "DialerAttemptOutcomeHandler",
            "Like DialerNotInServiceHandler, dispositioning an unconnected dialer attempt runs the disposition handlers, the agent presence manager and the queue service, which all record through the event publisher whose outbox constructs every event handler, so injecting them would close a publisher-to-handler construction cycle."),
        new DependencyInjectionException(
            "DialerAttemptFinalizer",
            "It runs from event handlers and the recovery sweep, and what it completes with belongs to features it cannot depend on: the disposition completers of activity management and the call reconciliation of Voice. Those, and the queue services, also record through the event publisher whose outbox constructs every event handler."),
        new DependencyInjectionException(
            "SharedAgentSkillsHandler",
            "The skill assignment service constructs every assignment handler, and the agent profile manager (whose handlers write back to the shared catalog) and the skills sync both lead back to the assignment service, so injecting them would close an assignment-service-to-handler construction cycle."),
        new DependencyInjectionException(
            "VoiceQueueOfferService",
            "It resolves the dialer attempt service only when it screens a preview record: that service places calls through the call router, which offers work through this service, so injecting it closed a construction cycle that hung every request at startup. The dialer attempt service is also absent when the dialer feature is off."),
        new DependencyInjectionException(
            "ContactCenterActivityOwnershipAuthorizationHandler",
            "An authorization handler that injected the authorization service would close a service-to-handler construction cycle, since the service constructs every handler; it resolves the service only when it checks the user's own-activity permission, and the interaction and agent managers only when the work state names nobody and the call has to say who handled it."),
        new DependencyInjectionException(
            "ContactActivityExportHandler",
            "An import handler that injected the import manager would close a manager-to-handler construction cycle."),
        new DependencyInjectionException(
            "DefaultContactMethodsAccessor",
            "Like DefaultTelephonyProviderResolver, it activates the contact-methods provider chosen at run time for a contact's content type from ContactMethodsOptions; AddContactMethodsProvider registers the provider in the same call that maps the type, so a mapped provider is never missing."),
        new DependencyInjectionException(
            "DefaultContactMethodsWriter",
            "It activates the writer chosen at run time for a contact's content type from ContactMethodsOptions, and it is itself the IContactMethodsWriter consumers inject, so the type-specific writers cannot be injected as that contract; AddContactMethodsWriter registers the writer in the same call that maps the type."),
    ];

    /// <summary>
    /// The types that took an <see cref="IServiceProvider"/> when the rule was widened to all of <c>src</c>, keyed by
    /// file and type. This list only shrinks: inject the collaborator itself (as a handler parameter in a minimal API),
    /// give an optional contract a null-object default, and delete the entry. Never add one; a type that genuinely has
    /// to take the container goes in <see cref="_serviceProviderExceptions"/> with its reason.
    /// </summary>
    private static readonly Dictionary<string, int> _knownServiceLocators = new(StringComparer.Ordinal)
    {
        ["src/Abstractions/CrestApps.OrchardCore.Abstractions/Configuration/TenantOptionsStartupValidator.cs: TenantOptionsStartupValidator"] = 1,
        ["src/Core/CrestApps.OrchardCore.AI.Core/Conversations/AIConversationMetrics.cs: AIConversationMetrics"] = 1,
        ["src/Core/CrestApps.OrchardCore.AI.Core/Services/DataSourceIndexingService.cs: DataSourceIndexingService"] = 1,
        ["src/Core/CrestApps.OrchardCore.AI.Core/Services/OrchardCoreSearchDocumentManager.cs: OrchardCoreSearchDocumentManager"] = 1,
        ["src/Core/CrestApps.OrchardCore.AI.Core/Services/OrchardCoreSearchIndexManager.cs: OrchardCoreSearchIndexManager"] = 1,
        ["src/Core/CrestApps.OrchardCore.AI.Core/Webhooks/AIWebhookPayloads.cs: AIWebhookConversationStartingHandler"] = 1,
        ["src/Core/CrestApps.OrchardCore.Checkout.Core/Services/CouponRedemptionService.cs: CouponRedemptionService"] = 1,
        ["src/Core/CrestApps.OrchardCore.Checkout.Core/Services/CouponScopeEvaluator.cs: CouponScopeEvaluator"] = 1,
        ["src/Core/CrestApps.OrchardCore.Checkout.Core/Services/DefaultCheckoutRefundReconciliationService.cs: DefaultCheckoutRefundReconciliationService"] = 1,
        ["src/Core/CrestApps.OrchardCore.Checkout.Core/Services/DefaultCheckoutRefundService.cs: DefaultCheckoutRefundService"] = 1,
        ["src/Core/CrestApps.OrchardCore.Commerce.Core/Emails/CommerceEmailSender.cs: CommerceEmailSender"] = 1,
        ["src/Core/CrestApps.OrchardCore.Commerce.Core/Emails/LiquidCommerceEmailTemplates.cs: LiquidCommerceEmailTemplates"] = 1,
        ["src/Core/CrestApps.OrchardCore.ContentTransfer.Core/ContentTransferEntryQueryContext.cs: ContentTransferEntryQueryContext"] = 1,
        ["src/Core/CrestApps.OrchardCore.ContentTransfer.Core/Services/ContentImportHandlerResolver.cs: ContentImportHandlerResolver"] = 1,
        ["src/Core/CrestApps.OrchardCore.ContentTransfer.Core/Services/DefaultContentTransferEntryAdminListQueryService.cs: DefaultContentTransferEntryAdminListQueryService"] = 1,
        ["src/Core/CrestApps.OrchardCore.Customers.Core/Services/DefaultCustomerOwnerNotifier.cs: DefaultCustomerOwnerNotifier"] = 1,
        ["src/Core/CrestApps.OrchardCore.DataConnectors.Core/DataConnectorManager.cs: DataConnectorManager"] = 1,
        ["src/Core/CrestApps.OrchardCore.PhoneNumbers.Core/Services/DefaultPhoneNumberVerificationManager.cs: DefaultPhoneNumberVerificationManager"] = 1,
        ["src/Core/CrestApps.OrchardCore.Recipes.Core/Services/RecipeSchemaExampleService.cs: RecipeSchemaExampleService"] = 1,
        ["src/Core/CrestApps.OrchardCore.Reports.Core/Services/ReportExportManager.cs: ReportExportManager"] = 1,
        ["src/Core/CrestApps.OrchardCore.Reports.Core/Services/ReportManager.cs: ReportManager"] = 1,
        ["src/Core/CrestApps.OrchardCore.Staff.Core/Handlers/StaffMemberCatalogHandler.cs: StaffMemberCatalogHandler"] = 1,
        ["src/Core/CrestApps.OrchardCore.Staff.Core/Services/StaffTeamReader.cs: StaffTeamReader"] = 1,
        ["src/Core/CrestApps.OrchardCore.Subscriptions.Core/Handlers/SubscriptionsPermissionsHandler.cs: SubscriptionsPermissionsHandler"] = 1,
        ["src/Core/CrestApps.OrchardCore.Subscriptions.Core/Services/TenantProvisioningService.cs: TenantProvisioningService"] = 1,
        ["src/Core/CrestApps.OrchardCore.Telnyx.Core/Services/TelnyxAutomaticCallRecorder.cs: TelnyxAutomaticCallRecorder"] = 1,
        ["src/Core/CrestApps.OrchardCore.TenantHierarchy.Core/Services/HierarchyAuditLog.cs: HierarchyAuditLog"] = 1,
        ["src/Core/CrestApps.OrchardCore.TenantHierarchy.Core/Services/TenantHierarchyBroker.cs: TenantHierarchyBroker"] = 1,
        ["src/Core/CrestApps.OrchardCore.Transactions.Core/Services/TransactionManager.cs: TransactionManager"] = 1,
        ["src/Core/CrestApps.OrchardCore.Transactions.Core/Services/TransactionSettlementService.cs: TransactionSettlementService"] = 1,
        ["src/Core/CrestApps.OrchardCore.Users.Core/Handlers/UserComponentsEventHandler.cs: UserComponentsEventHandler"] = 1,
        ["src/Core/CrestApps.OrchardCore.Webhooks.Core/Services/ShellProviderWebhookInboxScopeExecutor.cs: ShellProviderWebhookInboxScopeExecutor"] = 1,
        ["src/Core/CrestApps.OrchardCore.Workforce.Core/Services/DefaultPayrollExportService.cs: DefaultPayrollExportService"] = 1,
        ["src/Core/CrestApps.OrchardCore.Workforce.Core/Services/DefaultTimesheetService.cs: DefaultTimesheetService"] = 1,
        ["src/Core/CrestApps.OrchardCore.Workforce.Core/Services/TimeClockTimeTracker.cs: TimeClockTimeTrackerSync"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.DataSources/Services/SearchIndexProfileAIDataSourceSourceHandler.cs: SearchIndexProfileAIDataSourceSourceHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.Documents/Drivers/AICompletionWithConfigDocumentsDisplayDriver.cs: AICompletionWithConfigDocumentsDisplayDriver"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.Documents/Drivers/AIProfileDocumentsDisplayDriver.cs: AIProfileDocumentsDisplayDriver"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.Documents/Drivers/AIProfileTemplateDocumentsDisplayDriver.cs: AIProfileTemplateDocumentsDisplayDriver"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.Documents/Drivers/ChatInteractionDocumentsDisplayDriver.cs: ChatInteractionDocumentsDisplayDriver"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.Documents/Services/AIDocumentsIndexingService.cs: AIDocumentsIndexingService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.Memory/Services/AIMemoryIndexingService.cs: AIMemoryIndexingService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.WebMcp/Services/ContentItemWebMcpToolServiceFactory.cs: ContentItemWebMcpToolServiceFactory"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.WebMcp/Services/StorefrontWebMcpToolService.cs: StorefrontWebMcpToolService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI/Handlers/AIPersonaDeletionHandler.cs: AIPersonaDeletionHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI/Providers/CustomLlm/CustomLlmClientProvider.cs: CustomLlmClientProvider"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI/Services/AIAlertRuleNotifier.cs: AIAlertRuleNotifier"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI/Services/AIChatSessionCloser.cs: AIChatSessionCloser"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI/Workflows/Models/EvaluateConversationTask.cs: EvaluateConversationTask"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI/Workflows/Models/SimulateConversationTask.cs: SimulateConversationTask"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Assets/Recipes/AssetsRecipes.cs: AssetsDeploymentSource"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Assets/Recipes/AssetsRecipes.cs: AssetsRecipeStep"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Bookings/Handlers/BookingOrderEventHandler.cs: BookingOrderEventHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Bookings/Services/BookingBalanceTenderProvider.cs: BookingBalanceTenderProvider"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Bookings/Services/BookingStores.cs: BookingStore"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Checkout/Services/CurrencySetupStep.cs: CurrencySetupStep"] = 1,
        ["src/Modules/CrestApps.OrchardCore.ContentFields/Indexes/AddressFieldIndexProvider.cs: AddressFieldIndexProvider"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Customers/Handlers/CustomerCheckoutTaxProfileHandler.cs: CustomerCheckoutTaxProfileHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Customers/Handlers/CustomerMergeUserEvents.cs: CustomerMergeLoginFormEvent"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Customers/Handlers/CustomerMergeUserEvents.cs: CustomerMergeUserEventHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Customers/Handlers/LoyaltyOrderEventHandler.cs: LoyaltyOrderEventHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Customers/Handlers/ReferralHandlers.cs: ReferralOrderEventHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Dispatch/Mobile/FieldIntegrations.cs: InventoryFieldStockSource"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Dispatch/Reports/FieldServiceKpis.cs: FieldServiceKpiService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Geo/Recipes/GeoRecipes.cs: GeoDeploymentSource"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Geo/Recipes/GeoRecipes.cs: GeoRecipeStep"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Inventory/Services/InventoryAvailabilityProvider.cs: InventoryAvailabilityProvider"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Inventory/Services/StockAlertService.cs: StockAlertService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.NLWeb/AI/VectorNLWebRetriever.cs: VectorNLWebRetriever"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Customers/OrderAnonymizer.cs: OrderAnonymizer"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Customers/OrderCustomerMergeHandler.cs: OrderCustomerMergeHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Customers/OrderCustomerPrivacyHandler.cs: OrderCustomerPrivacyHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Handlers/GiftCardOrderEventHandler.cs: GiftCardOrderEventHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Handlers/OrderWebhookEventHandler.cs: OrderWebhookEventHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Services/CartCheckoutService.cs: CartCheckoutService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Services/DraftOrderService.cs: DraftOrderService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Services/OrderConfirmationEmailBuilder.cs: OrderConfirmationEmailBuilder"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Services/OrderDocumentBuilder.cs: OrderDocumentBuilder"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Services/OrderOperationsService.cs: OrderOperationsService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Services/OrderPayLinks.cs: OrderPayLinks"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Services/OrderRefundRecorder.cs: OrderRefundRecorder"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Orders/Services/QuoteService.cs: QuoteService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.PayLater/Services/OfflinePaymentLookup.cs: OfflinePaymentLookup"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Products/Drivers/ProductListingPartDisplayDriver.cs: ProductListingPartDisplayDriver"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Products/Drivers/ProductPartDisplayDriver.cs: ProductPartDisplayDriver"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Products/Feeds/ProductFeedBuilder.cs: ProductFeedBuilder"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Products/Indexing/ProductSearchIndexingService.cs: ProductSearchIndexingService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Products/Services/ProductSearchRouter.cs: ProductSearchRouter"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Reports/Designer/Workflows/Activities/ExportReportTask.cs: ExportReportTask"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Reviews/Services/ReviewPhotoStore.cs: ReviewPhotoStore"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Seo/SchemaOrg/SchemaOrgContentSources.cs: SchemaOrgContentSources"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Staff/Drivers/UserStaffDisplayDriver.cs: StaffUserEventHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Stripe/Controllers/AccountPaymentMethodsController.cs: AccountPaymentMethodsController"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Stripe/Services/StripePersonalDataContributor.cs: StripePersonalDataContributor"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Stripe/Workflows/StripeWorkflowNotifier.cs: StripeWorkflowNotifier"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Subscriptions/Drivers/ContentStepCheckoutFlowDisplayDriver.cs: ContentStepCheckoutFlowDisplayDriver"] = 1,
        ["src/Modules/CrestApps.OrchardCore.TenantHierarchy/Handlers/LinkedUserEventHandler.cs: LinkedUserEventHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.TenantHierarchy/Services/ChildTenantManager.cs: ChildTenantManager"] = 1,
        ["src/Modules/CrestApps.OrchardCore.TenantHierarchy/Services/TenantHierarchyPlatformService.cs: TenantHierarchyPlatformService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Transactions/Services/DefaultTransactionCommandService.cs: DefaultTransactionCommandService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Transactions/Services/DefaultTransactionReminderService.cs: DefaultTransactionReminderService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Transactions/Services/PaymentReceiptTransactionPaymentHandler.cs: PaymentReceiptTransactionPaymentHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Transactions/Services/TransactionCustomerPageService.cs: TransactionCustomerPageService"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Transactions/Services/TransactionReceiptBuilder.cs: TransactionReceiptBuilder"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Transactions/Services/TransactionRefundHandler.cs: TransactionRefundHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Transactions/Services/TransactionSettlementCheckoutHandler.cs: TransactionSettlementCheckoutHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Wizard/Contents/ContentWizardAccessPolicy.cs: ContentWizardAccessPolicy"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Wizard/Contents/ContentWizardHandler.cs: ContentWizardHandler"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Wizard/Contents/ContentWizardStepDisplayDriver.cs: ContentWizardStepDisplayDriver"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Wizard/Drivers/WizardPartDisplayDriver.cs: WizardPartDisplayDriver"] = 1,
        ["src/Modules/CrestApps.OrchardCore.WorkOrders/Drivers/WorkOrderDisplayDriver.cs: WorkOrderDisplayDriver"] = 1,
        ["src/Modules/CrestApps.OrchardCore.WorkOrders/Recipes/WorkOrdersRecipes.cs: WorkOrdersDeploymentSource"] = 1,
        ["src/Modules/CrestApps.OrchardCore.WorkOrders/Recipes/WorkOrdersRecipes.cs: WorkOrdersRecipeStep"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Workforce/Deployments/WorkforceSettingsDeploymentSource.cs: WorkforceSettingsDeploymentSource"] = 1,
    };

    /// <summary>
    /// The remaining places that inject a collection and take the first element, with the reason each is not one
    /// of the optional contracts above. Adding to this list is a decision, not a formality.
    /// </summary>
    private static readonly DependencyInjectionException[] _optionalEnumerableExceptions =
    [
        new DependencyInjectionException(
            "_contractsWithNullObjectDefaults",
            "Only the contracts listed in that array are guarded; every other collection injection is out of this rule's scope until it, too, is given a default."),
    ];

    [Fact]
    public void SourceCode_DoesNotResolveItsCollaboratorsFromTheContainer()
    {
        // Arrange
        var violations = FindServiceLocators().Where(violation => !_knownServiceLocators.ContainsKey(violation)).ToList();

        // Assert
        AssertNoViolations(
            ServiceProviderRule,
            violations,
            "Inject the collaborator itself. When it is genuinely optional, register a null-object default in the " +
            "feature that declares the contract and replace it in the feature that implements it; when taking the " +
            "container is unavoidable, add the type to the exception list with the reason.");
    }

    [Fact]
    public void EveryKnownServiceLocator_StillTakesTheContainer()
    {
        // Arrange
        var actual = FindServiceLocators().Distinct(StringComparer.Ordinal).ToDictionary(violation => violation, _ => 1, StringComparer.Ordinal);

        // Assert
        Ratchet.AssertNothingStale(actual, _knownServiceLocators, $"{nameof(DependencyInjectionArchitectureTests)}.{nameof(_knownServiceLocators)}");
    }

    private static List<string> FindServiceLocators()
    {
        var violations = new List<string>();

        foreach (var file in EnumerateInScopeSourceFiles())
        {
            var source = Ratchet.ReadAllText(file);

            if (!source.Contains("IServiceProvider", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in _constructorRegex.Matches(source))
            {
                if (!_serviceProviderParameterRegex.IsMatch(match.Groups["parameters"].Value))
                {
                    continue;
                }

                var typeName = match.Groups["name"].Value;

                if (IsExcepted(typeName, _serviceProviderExceptions))
                {
                    continue;
                }

                violations.Add($"{file.RelativePath}: {typeName}");
            }
        }

        return violations;
    }

    [Fact]
    public void ContractsWithNullObjectDefaults_AreInjectedDirectly_NotScannedFor()
    {
        // Arrange
        var files = EnumerateInScopeSourceFiles();
        var violations = new List<string>();

        // Act
        foreach (var file in files)
        {
            var source = Ratchet.ReadAllText(file);

            foreach (var contract in _contractsWithNullObjectDefaults)
            {
                if (source.Contains($"IEnumerable<{contract}>", StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}: IEnumerable<{contract}>");
                }

                if (source.Contains($"GetService<{contract}>", StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}: GetService<{contract}>()");
                }
            }
        }

        // Assert
        AssertNoViolations(
            OptionalEnumerableRule,
            violations,
            "These contracts have a null-object default, so the collaborator is always resolvable. Take it as a " +
            "plain constructor parameter and delete the null check that went with the scan.");
    }

    [Fact]
    public void EveryException_NamesTheReasonItIsAnException()
    {
        // Assert
        // An exception list that is allowed to grow without reasons stops being a record of decisions and becomes
        // a place to put anything that fails.
        Assert.All(
            _serviceProviderExceptions.Concat(_optionalEnumerableExceptions),
            exception =>
            {
                Assert.False(string.IsNullOrWhiteSpace(exception.TypeName));
                Assert.False(string.IsNullOrWhiteSpace(exception.Reason));
                Assert.True(exception.Reason.Length > 40, $"The reason recorded for '{exception.TypeName}' is too short to be one.");
            });
    }

    // An authorization handler resolves what it checks lazily: the authorization service constructs every handler, so an
    // injected collaborator that authorizes would close a construction cycle (AGENTS.md).
    private static bool IsExcepted(string typeName, DependencyInjectionException[] exceptions)
        => exceptions.Any(exception => string.Equals(exception.TypeName, typeName, StringComparison.Ordinal))
            || typeName.EndsWith("ScopeContext", StringComparison.Ordinal)
            || typeName.EndsWith("BackgroundTask", StringComparison.Ordinal)
            || typeName.EndsWith("Hub", StringComparison.Ordinal)
            || typeName.EndsWith("AuthorizationHandler", StringComparison.Ordinal);

    private static void AssertNoViolations(string rule, List<string> violations, string guidance)
    {
        if (violations.Count == 0)
        {
            return;
        }

        var message = new StringBuilder()
            .AppendLine($"{rule} — {violations.Count} violation(s):")
            .AppendLine();

        foreach (var violation in violations.OrderBy(violation => violation, StringComparer.Ordinal))
        {
            message.Append("    ").AppendLine(violation);
        }

        message.AppendLine().AppendLine(guidance);

        Assert.Fail(message.ToString());
    }

    private static List<Ratchet.SourceFile> EnumerateInScopeSourceFiles()
        => Ratchet.EnumerateFiles("src").ToList();

    private sealed record DependencyInjectionException(string TypeName, string Reason);
}
