using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Checks;
using CrestApps.OrchardCore.Banking.Disbursements;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// With Banking's Direct Deposit on, deposit rows of net pay become ACH disbursements.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(DisbursementsConstants.Features.DirectDeposit)]
public sealed class DirectDepositStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddPayrollDirectDepositCore();
}

/// <summary>
/// With Banking's Checks on, check rows of net pay become printed checks with the statement as their voucher.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(ChecksConstants.Features.Checks)]
public sealed class PaychecksStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddPayrollPaychecksCore();
}

/// <summary>
/// Tells a person that the bank returned a deposit of their pay.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(DisbursementsConstants.Features.DirectDeposit, OrchardCoreFeatureIds.Notifications)]
public sealed class DirectDepositNotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(
            PayrollConstants.DepositReturnedNotificationType,
            LocalizationSource.Create<DirectDepositNotificationsStartup>("Deposit of pay returned"),
            type => type.Description = LocalizationSource.Create<DirectDepositNotificationsStartup>("When the bank returns a deposit of your pay."));
        services.AddDisbursementObserver<DepositReturnedNotificationObserver>();
    }
}

/// <summary>
/// Tells a staff member that a bank account of theirs was added or its number changed.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(BankingConstants.Features.Banking, OrchardCoreFeatureIds.Notifications)]
public sealed class PayrollBankingNotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(
            PayrollConstants.BankAccountChangedNotificationType,
            LocalizationSource.Create<PayrollBankingNotificationsStartup>("Bank account changed"),
            type => type.Description = LocalizationSource.Create<PayrollBankingNotificationsStartup>("When a bank account of yours is added or its number changes: a security notice."));
        services.AddBankAccountObserver<BankAccountChangedNotificationObserver>();
    }
}

/// <summary>
/// Tells people their pay statement is ready, through Orchard Notifications.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class PayrollNotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(
            PayrollConstants.PayStatementReadyNotificationType,
            LocalizationSource.Create<PayrollNotificationsStartup>("Pay statement ready"),
            type => type.Description = LocalizationSource.Create<PayrollNotificationsStartup>("When a pay statement of yours is issued."));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayStatementObserver, PayStatementNotificationObserver>());
        services.AddNotificationType(
            PayrollConstants.PayDetailsChangedNotificationType,
            LocalizationSource.Create<PayrollNotificationsStartup>("Pay details changed"),
            type => type.Description = LocalizationSource.Create<PayrollNotificationsStartup>("When your tax elections or where your pay goes change: a security notice."));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayProfileObserver, PayDetailsChangedNotificationObserver>());
        services.AddNotificationType(
            PayrollConstants.TaxFormReadyNotificationType,
            LocalizationSource.Create<PayrollNotificationsStartup>("Tax form ready"),
            type => type.Description = LocalizationSource.Create<PayrollNotificationsStartup>("When a tax form of yours, such as a wage statement, is filed and ready in My pay."));
        services.AddNotificationType(
            PayrollConstants.PayReviewRequestedNotificationType,
            LocalizationSource.Create<PayrollNotificationsStartup>("Review your coming pay"),
            type => type.Description = LocalizationSource.Create<PayrollNotificationsStartup>("When your pay is calculated and ready to look at before it is approved."));
        services.AddNotificationType(
            PayrollConstants.PayReviewQuestionedNotificationType,
            LocalizationSource.Create<PayrollNotificationsStartup>("Pay questioned"),
            type => type.Description = LocalizationSource.Create<PayrollNotificationsStartup>("When an employee asks about their pay in a run you prepared."));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayRunObserver, PayReviewNotificationObserver>());
    }
}
