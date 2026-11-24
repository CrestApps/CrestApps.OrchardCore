using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using CrestApps.Core;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Records;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// How the generic record tools find, make, list, check, save and delete the AI alert rules, QA cohorts and test cases.
/// These records are kept in <see cref="ICatalog{T}"/> stores, not behind a catalog manager, so each hook does what the
/// record's admin controller does with its store.
/// </summary>
internal static class AIQualityRecordKinds
{
    /// <summary>
    /// Finds a record in its store by its ID.
    /// </summary>
    /// <typeparam name="TStore">The store of the record.</typeparam>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="id">The ID of the record.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The record, or <see langword="null"/> when there is none.</returns>
    public static ValueTask<T> FindAsync<TStore, T>(IServiceProvider services, string id, CancellationToken cancellationToken)
        where TStore : ICatalog<T>
        where T : CatalogItem
        => services.GetRequiredService<TStore>().FindByIdAsync(id, cancellationToken);

    /// <summary>
    /// Lists a page of records by name, as the admin list does.
    /// </summary>
    /// <typeparam name="TStore">The store of the record.</typeparam>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="query">The page and the text the names must contain.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The page.</returns>
    public static ValueTask<PageResult<T>> ListAsync<TStore, T>(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
        where TStore : ICatalog<T>
        where T : CatalogItem
        => services.GetRequiredService<TStore>().PageAsync(query.Page, query.PageSize, new QueryContext
        {
            Name = string.IsNullOrWhiteSpace(query.Text) ? null : query.Text.Trim(),
            Sorted = true,
        }, cancellationToken);

    /// <summary>
    /// Saves a record in its store: a new record is created, an existing one updated.
    /// </summary>
    /// <typeparam name="TStore">The store of the record.</typeparam>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="record">The record.</param>
    /// <param name="isNew">Whether the record is new.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>A task that completes when the record is saved.</returns>
    public static async ValueTask SaveAsync<TStore, T>(IServiceProvider services, T record, bool isNew, CancellationToken cancellationToken)
        where TStore : ICatalog<T>
        where T : CatalogItem
    {
        var store = services.GetRequiredService<TStore>();

        if (isNew)
        {
            await store.CreateAsync(record, cancellationToken);

            return;
        }

        await store.UpdateAsync(record, cancellationToken);
    }

    /// <summary>
    /// Deletes a record from its store.
    /// </summary>
    /// <typeparam name="TStore">The store of the record.</typeparam>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="record">The record.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns><see langword="false"/> when nothing was deleted.</returns>
    public static ValueTask<bool> DeleteAsync<TStore, T>(IServiceProvider services, T record, CancellationToken cancellationToken)
        where TStore : ICatalog<T>
        where T : CatalogItem
        => services.GetRequiredService<TStore>().DeleteAsync(record, cancellationToken);

    /// <summary>
    /// Makes a new alert rule as the Create action of the alert rules page does.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The new rule.</returns>
    public static ValueTask<AIAlertRule> NewAlertRuleAsync(IServiceProvider services)
        => ValueTask.FromResult(new AIAlertRule
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = services.GetRequiredService<IClock>().UtcNow,
        });

    /// <summary>
    /// Makes a new QA cohort as the Create action of the QA cohorts page does: it scores the conversations that close
    /// from now on.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The new cohort.</returns>
    public static ValueTask<AIQACohort> NewCohortAsync(IServiceProvider services)
    {
        var utcNow = services.GetRequiredService<IClock>().UtcNow;

        return ValueTask.FromResult(new AIQACohort
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = utcNow,
            CursorUtc = utcNow,
        });
    }

    /// <summary>
    /// Makes a new test case as the Create action of the test cases page does.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The new test case.</returns>
    public static ValueTask<AITestCase> NewTestCaseAsync(IServiceProvider services)
        => ValueTask.FromResult(new AITestCase
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = services.GetRequiredService<IClock>().UtcNow,
        });

    /// <summary>
    /// Saves a test case, stamping a change as the Edit action of the test cases page does.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="testCase">The test case.</param>
    /// <param name="isNew">Whether the test case is new.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>A task that completes when the test case is saved.</returns>
    public static ValueTask SaveTestCaseAsync(IServiceProvider services, AITestCase testCase, bool isNew, CancellationToken cancellationToken)
    {
        if (!isNew)
        {
            testCase.ModifiedUtc = services.GetRequiredService<IClock>().UtcNow;
        }

        return SaveAsync<IAITestCaseStore, AITestCase>(services, testCase, isNew, cancellationToken);
    }

    /// <summary>
    /// Checks that the profile a rule watches exists and that the notified addresses are email addresses. The editor
    /// offers the profiles in a list and its email box is checked by the browser, so a form post cannot send either, but
    /// a chat can; the driver checks every other rule of the editor.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="rule">The rule, after its editor bound the values.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The result.</returns>
    public static async ValueTask<ValidationResultDetails> ValidateAlertRuleAsync(IServiceProvider services, AIAlertRule rule, CancellationToken cancellationToken)
    {
        var result = new ValidationResultDetails();

        if (!string.IsNullOrEmpty(rule.ProfileId) && !await ProfileExistsAsync(services, rule.ProfileId, cancellationToken))
        {
            result.Fail(new ValidationResult(
                $"No AI profile has the ID '{rule.ProfileId}'. Use listAIProfiles to find it, or leave ProfileId empty to watch every profile.",
                [nameof(AIAlertRule.ProfileId)]));
        }

        var invalid = SplitEmails(rule.NotifyEmails)
            .Where(email => !MailAddress.TryCreate(email, out var address) || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (invalid.Count > 0)
        {
            result.Fail(new ValidationResult(
                $"{string.Join(", ", invalid.Select(email => $"'{email}'"))} {(invalid.Count == 1 ? "is not an email address" : "are not email addresses")}. Separate the addresses with commas.",
                [nameof(AIAlertRule.NotifyEmails)]));
        }

        return result;
    }

    /// <summary>
    /// Checks that every profile a cohort samples exists.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="cohort">The cohort, after its editor bound the values.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The result.</returns>
    public static async ValueTask<ValidationResultDetails> ValidateCohortAsync(IServiceProvider services, AIQACohort cohort, CancellationToken cancellationToken)
    {
        var result = new ValidationResultDetails();
        var unknown = new List<string>();

        foreach (var profileId in cohort.ProfileIds ?? [])
        {
            if (!await ProfileExistsAsync(services, profileId, cancellationToken))
            {
                unknown.Add(profileId);
            }
        }

        if (unknown.Count > 0)
        {
            result.Fail(new ValidationResult(
                $"No AI profile has the ID {string.Join(", ", unknown.Select(id => $"'{id}'"))}. Use listAIProfiles to find them.",
                [nameof(AIQACohort.ProfileIds)]));
        }

        return result;
    }

    /// <summary>
    /// Checks that the profile a test case runs against exists.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="testCase">The test case, after its editor bound the values.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The result.</returns>
    public static async ValueTask<ValidationResultDetails> ValidateTestCaseAsync(IServiceProvider services, AITestCase testCase, CancellationToken cancellationToken)
    {
        var result = new ValidationResultDetails();

        // An empty profile is reported by the editor's own checks.
        if (!string.IsNullOrEmpty(testCase.ProfileId) && !await ProfileExistsAsync(services, testCase.ProfileId, cancellationToken))
        {
            result.Fail(new ValidationResult(
                $"No AI profile has the ID '{testCase.ProfileId}'. Use listAIProfiles to find it.",
                [nameof(AITestCase.ProfileId)]));
        }

        return result;
    }

    /// <summary>
    /// Splits the stored list of notified addresses.
    /// </summary>
    /// <param name="emails">The stored addresses, separated by commas or semicolons.</param>
    /// <returns>The addresses.</returns>
    public static List<string> SplitEmails(string emails)
        => (emails ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static async ValueTask<bool> ProfileExistsAsync(IServiceProvider services, string profileId, CancellationToken cancellationToken)
        => await services.GetRequiredService<IAIProfileManager>().FindByIdAsync(profileId, cancellationToken) is not null;
}
