using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Users.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// Builds a real <see cref="DefaultTransactionCommandService"/> over the supplied manager, so tests run the same commands
/// the console runs without a database.
/// </summary>
internal static class TransactionCommandServiceFactory
{
    public static DefaultTransactionCommandService Create(
        ITransactionManager manager,
        TestClock clock,
        IServiceProvider services = null,
        IDistributedLock distributedLock = null)
        => new(
            manager,
            distributedLock ?? new LocalLock(NullLogger<LocalLock>.Instance),
            new Mock<ISession>().Object,
            new HttpContextAccessor(),
            Mock.Of<IUserService>(),
            Mock.Of<IDisplayNameProvider>(),
            new TransactionDisplayText([], Options.Create(new TransactionSourceOptions()), new PassThroughStringLocalizer<TransactionDisplayText>(), new SourceTextLocalizerFactory()),
            services ?? new ServiceCollection().BuildServiceProvider(),
            clock,
            NullLogger<DefaultTransactionCommandService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionCommandService>());
}
