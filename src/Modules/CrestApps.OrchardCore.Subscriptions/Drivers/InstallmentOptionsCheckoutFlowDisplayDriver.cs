using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Renders the "Choose how to pay" checkout step and turns the customer's choice into an installment plan when they
/// choose installments.
/// </summary>
/// <remarks>
/// Only the identifier of the chosen option is posted. The options are read again from the providers before the plan
/// is made, and the amounts come from the transaction, so the browser can't change what is charged.
/// </remarks>
public sealed class InstallmentOptionsCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    private const string OptionIdKey = "optionId";
    private const string PlanIdKey = "planId";

    private readonly ITransactionManager _transactionManager;
    private readonly InstallmentOptionResolver _optionResolver;
    private readonly IInstallmentPlanService _planService;
    private readonly IInstallmentPlanStore _planStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentOptionsCheckoutFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="optionResolver">The resolver of the options offered for a transaction.</param>
    /// <param name="planService">The installment plan service.</param>
    /// <param name="planStore">The installment plan store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InstallmentOptionsCheckoutFlowDisplayDriver(
        ITransactionManager transactionManager,
        InstallmentOptionResolver optionResolver,
        IInstallmentPlanService planService,
        IInstallmentPlanStore planStore,
        IStringLocalizer<InstallmentOptionsCheckoutFlowDisplayDriver> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _optionResolver = optionResolver;
        _planService = planService;
        _planStore = planStore;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string StepKey
        => SubscriptionConstants.InstallmentPlans.CheckoutStepKey;

    /// <inheritdoc/>
    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        if (flow.Session is not CheckoutSession session)
        {
            return null;
        }

        var (savedOptionId, savedPlanId) = ReadChoice(flow);
        var plan = string.IsNullOrEmpty(savedPlanId) ? null : await _planStore.FindByIdAsync(savedPlanId);

        if (plan is not null && plan.Status != InstallmentPlanStatus.Canceled)
        {
            var chosen = DescribePlan(plan);

            return Initialize<InstallmentOptionsStepViewModel>("InstallmentOptionsStep", model =>
            {
                model.Title = plan.Title;
                model.Currency = plan.Currency;
                model.FullAmount = plan.TotalAmount;
                model.IsChosen = true;
                model.ChosenPlan = chosen;
                model.SelectedOptionId = savedOptionId;
            }).Location("Content:5");
        }

        var transaction = await _transactionManager.FindByIdAsync(session.ReferenceId);

        if (transaction is null)
        {
            return null;
        }

        var options = await _optionResolver.GetOptionsAsync(transaction);
        var choices = new List<InstallmentOptionViewModel>
        {
            new()
            {
                Id = SubscriptionConstants.InstallmentPlans.PayInFullOptionId,
                Title = S["Pay in full"],
                Description = S["{0} today.", Money(transaction.OutstandingAmount, transaction.Currency)],
                DueToday = transaction.OutstandingAmount,
            },
        };

        foreach (var option in options)
        {
            choices.Add(DescribeOption(option, transaction.OutstandingAmount, transaction.Currency));
        }

        return Initialize<InstallmentOptionsStepViewModel>("InstallmentOptionsStep", model =>
        {
            model.Title = transaction.Title;
            model.Currency = transaction.Currency;
            model.FullAmount = transaction.OutstandingAmount;
            model.Options = choices;
            model.SelectedOptionId = savedOptionId ?? SubscriptionConstants.InstallmentPlans.PayInFullOptionId;
        }).Location("Content:5");
    }

    /// <inheritdoc/>
    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (flow.Session is not CheckoutSession session)
        {
            return null;
        }

        var (_, savedPlanId) = ReadChoice(flow);

        if (!string.IsNullOrEmpty(savedPlanId))
        {
            var existing = await _planStore.FindByIdAsync(savedPlanId);

            if (existing is not null && existing.Status != InstallmentPlanStatus.Canceled)
            {
                // The plan is made and this checkout already collects its first payment. Posting the step again, from
                // the back button for example, keeps that choice rather than making a second plan.
                return await EditStepAsync(flow, context);
            }
        }

        var model = new InstallmentOptionsStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var optionId = model.SelectedOptionId?.Trim();

        if (string.IsNullOrEmpty(optionId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SelectedOptionId), S["Choose how you want to pay."]);

            return await EditStepAsync(flow, context);
        }

        if (string.Equals(optionId, SubscriptionConstants.InstallmentPlans.PayInFullOptionId, StringComparison.Ordinal))
        {
            SaveChoice(flow, optionId, planId: null);

            return await EditStepAsync(flow, context);
        }

        var transaction = await _transactionManager.FindByIdAsync(session.ReferenceId);
        var option = transaction is null ? null : await _optionResolver.FindOptionAsync(transaction, optionId);

        if (option is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SelectedOptionId), S["That way of paying is no longer available. Choose another."]);

            return await EditStepAsync(flow, context);
        }

        var result = await _planService.CreateFromTransactionAsync(new CreateInstallmentPlanFromTransactionRequest
        {
            TransactionId = transaction.ItemId,
            CheckoutSessionId = session.SessionId,
            Option = option,
        });

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.SelectedOptionId), error.Value);
            }

            return await EditStepAsync(flow, context);
        }

        SaveChoice(flow, optionId, result.Plan.ItemId);

        return await EditStepAsync(flow, context);
    }

    private InstallmentOptionViewModel DescribeOption(InstallmentOption option, decimal total, string currency)
    {
        var amounts = InstallmentScheduleCalculator.SplitAmounts(total, option.DownPaymentAmount, option.InstallmentCount, currency);
        var regular = amounts.Count > 0 ? amounts[0] : 0m;
        var last = amounts.Count > 0 ? amounts[amounts.Count - 1] : 0m;

        return new InstallmentOptionViewModel
        {
            Id = option.Id,
            Title = string.IsNullOrWhiteSpace(option.Title) ? DescribeCount(option.InstallmentCount, option.Frequency) : option.Title,
            Description = Describe(option.DownPaymentAmount, option.InstallmentCount, regular, last, currency),
            DueToday = option.DownPaymentAmount > 0m ? option.DownPaymentAmount : regular,
        };
    }

    private InstallmentOptionViewModel DescribePlan(InstallmentPlan plan)
    {
        var scheduled = plan.Payments.Where(payment => payment.Number > 0).OrderBy(payment => payment.Number).ToArray();
        var regular = scheduled.Length > 0 ? scheduled[0].Amount : 0m;
        var last = scheduled.Length > 0 ? scheduled[scheduled.Length - 1].Amount : 0m;

        return new InstallmentOptionViewModel
        {
            Id = plan.ItemId,
            Title = DescribeCount(plan.InstallmentCount, plan.Frequency),
            Description = Describe(plan.DownPaymentAmount, plan.InstallmentCount, regular, last, plan.Currency),
            DueToday = plan.FirstPayment?.Amount ?? 0m,
        };
    }

    private string DescribeCount(int count, InstallmentFrequency frequency)
        => frequency switch
        {
            InstallmentFrequency.Weekly => S["{0} weekly payments", count],
            InstallmentFrequency.BiWeekly => S["{0} payments every two weeks", count],
            InstallmentFrequency.Quarterly => S["{0} quarterly payments", count],
            _ => S["{0} monthly payments", count],
        };

    private string Describe(decimal downPayment, int count, decimal regular, decimal last, string currency)
    {
        if (downPayment > 0m)
        {
            return regular == last
                ? S["{0} today, then {1} payments of {2}, charged to your card automatically.", Money(downPayment, currency), count, Money(regular, currency)]
                : S["{0} today, then {1} payments of {2} (the last one {3}), charged to your card automatically.", Money(downPayment, currency), count, Money(regular, currency), Money(last, currency)];
        }

        if (count == 1)
        {
            return S["{0} today.", Money(regular, currency)];
        }

        return regular == last
            ? S["{0} today, then {1} more payments of {0}, charged to your card automatically.", Money(regular, currency), count - 1]
            : S["{0} today, then {1} more payments (the last one {2}), charged to your card automatically.", Money(regular, currency), count - 1, Money(last, currency)];
    }

    private static string Money(decimal amount, string currency)
        => $"{currency} {CurrencyScale.Format(amount, currency)}";

    private (string OptionId, string PlanId) ReadChoice(CheckoutFlow flow)
    {
        if (!flow.Session.SavedSteps.TryGetPropertyValue(StepKey, out var node) || node is not JsonObject saved)
        {
            return (null, null);
        }

        return (Read(saved, OptionIdKey), Read(saved, PlanIdKey));
    }

    private void SaveChoice(CheckoutFlow flow, string optionId, string planId)
    {
        var saved = new JsonObject
        {
            [OptionIdKey] = optionId,
        };

        if (!string.IsNullOrEmpty(planId))
        {
            saved[PlanIdKey] = planId;
        }

        flow.Session.SavedSteps[StepKey] = saved;
    }

    private static string Read(JsonObject saved, string key)
        => saved.TryGetPropertyValue(key, out var value) && value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : null;
}
