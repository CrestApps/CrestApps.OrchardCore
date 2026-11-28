using CrestApps.Core.AI;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications;

/// <summary>
/// Registers the phone number verification queue AI tools whenever the Phone Number Verifications and AI features are
/// on. Listing needs <c>RunPhoneNumberVerificationsReport</c> and retrying <c>VerifyPhoneNumbers</c>, as the queue page
/// does; retrying asks for approval, because every lookup is a billable provider call.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListPhoneNumberVerificationsTool>(ListPhoneNumberVerificationsTool.TheName)
            .WithTitle(S["List Phone Number Verifications"])
            .WithDescription(S["Lists the phone numbers queued for or checked by verification, with status, provider, attempts, last error and next due date, and the count per status; filter by number, status and sort. Use it to find failed or stuck verifications."])
            .WithCategory(S["Phone Numbers"])
            .Selectable();

        services.AddCoreAITool<RetryPhoneNumberVerificationsTool>(RetryPhoneNumberVerificationsTool.TheName)
            .WithTitle(S["Retry Phone Number Verifications"])
            .WithDescription(S["Re-queues phone verifications for the given content item ids, or for all failed ones, and runs them against the enabled lookup provider. Each lookup is a billable provider call; fails when no provider is enabled."])
            .WithCategory(S["Phone Numbers"])
            .AsExternal()
            .Selectable();
    }
}
