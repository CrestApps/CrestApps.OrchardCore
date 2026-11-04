using System.Reflection;
using CrestApps.OrchardCore.Bookings;
using CrestApps.OrchardCore.Accounts.B2B;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules.Manifest;
using BookingsStartup = CrestApps.OrchardCore.Bookings.Startup;
using B2BAccountsStartup = CrestApps.OrchardCore.Accounts.B2B.Startup;
using IStartup = OrchardCore.Modules.IStartup;
using RequireFeaturesAttribute = OrchardCore.Modules.RequireFeaturesAttribute;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// A feature whose services are built from the Transactions ledger has to enable Transactions with it. Otherwise the
/// tenant runs with a service nothing can construct, and the first background task or page that reaches it fails with
/// "Unable to resolve service for type ITransactionManager".
/// </summary>
public sealed class CommerceTransactionsDependencyTests
{
    public static TheoryData<Type, string> FeaturesThatOweOnTheLedger => new()
    {
        { typeof(B2BAccountsStartup), B2BAccountsConstants.Feature },
        { typeof(BookingsStartup), BookingsConstants.Feature },
    };

    [Theory]
    [MemberData(nameof(FeaturesThatOweOnTheLedger))]
    public void AFeatureWhoseServicesNeedTheLedger_DependsOnTransactions(Type startupType, string featureId)
    {
        // The startup is not behind [RequireFeatures], so what it registers is there whenever the feature is on.
        Assert.Null(startupType.GetCustomAttribute<RequireFeaturesAttribute>());

        var moduleAssembly = startupType.Assembly;
        var services = new ServiceCollection();
        ((IStartup)Activator.CreateInstance(startupType)).ConfigureServices(services);

        var transactionsAssembly = typeof(ITransactionManager).Assembly;

        var needTheLedger = services
            .Select(descriptor => descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType)
            .Where(type => type is not null && type.Assembly == moduleAssembly)
            .Where(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType.Assembly == transactionsAssembly))
            .ToArray();

        Assert.NotEmpty(needTheLedger);

        var feature = moduleAssembly
            .GetCustomAttributes<FeatureAttribute>()
            .Single(attribute => attribute is not ModuleAttribute && attribute.Id == featureId);

        Assert.Contains(TransactionsConstants.Features.Area, feature.Dependencies);
    }

    [Fact]
    public void AccountTerms_AreRegisteredWithTheAccountsB2BFeature()
    {
        // The terms tender and the account account page need the terms service, so it can't move behind
        // [RequireFeatures(Transactions)] on its own; B2BAccounts declares Transactions instead.
        var services = new ServiceCollection();
        new B2BAccountsStartup().ConfigureServices(services);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AccountTermsService));
        Assert.Null(typeof(B2BAccountsStartup).GetCustomAttribute<RequireFeaturesAttribute>());
    }
}
