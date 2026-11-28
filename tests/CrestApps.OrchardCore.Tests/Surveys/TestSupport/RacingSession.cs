using Castle.DynamicProxy;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Wraps a YesSql session so a competing save runs at the moment a walk is most exposed to it: the first time the walk
/// reads index rows after it loaded documents, which is after it loaded its page and before it checks and writes it.
/// The competing save is whatever the test does (a real save through a store, on a session of its own), so the walk
/// meets a save as it happens in production, not a simulated one.
/// </summary>
internal static class RacingSession
{
    private static readonly ProxyGenerator _generator = new();

    /// <summary>
    /// Creates the session.
    /// </summary>
    /// <param name="inner">The walk's session.</param>
    /// <param name="competingSave">The competing save, committed on a session of its own; run once.</param>
    /// <returns>The session to give the walk.</returns>
    public static ISession Create(ISession inner, Func<Task> competingSave)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(competingSave);

        return _generator.CreateInterfaceProxyWithTarget(inner, new SessionInterceptor(new Race(competingSave)));
    }

    private sealed class Race
    {
        public Race(Func<Task> competingSave)
        {
            CompetingSave = competingSave;
        }

        public Func<Task> CompetingSave { get; }

        public bool Loaded { get; set; }

        public bool Ran { get; set; }
    }

    private sealed class SessionInterceptor : IInterceptor
    {
        private readonly Race _race;

        public SessionInterceptor(Race race)
        {
            _race = race;
        }

        public void Intercept(IInvocation invocation)
        {
            invocation.Proceed();

            if (invocation.Method.Name == nameof(ISession.Query) && invocation.ReturnValue is IQuery query)
            {
                invocation.ReturnValue = _generator.CreateInterfaceProxyWithTarget(query, new QueryInterceptor(_race));
            }
        }
    }

    private sealed class QueryInterceptor : IInterceptor
    {
        private readonly Race _race;

        public QueryInterceptor(Race race)
        {
            _race = race;
        }

        public void Intercept(IInvocation invocation)
        {
            switch (invocation.Method.Name)
            {
                case nameof(IQuery.For):
                    _race.Loaded = true;
                    break;

                case nameof(IQuery.ForIndex) when _race.Loaded && !_race.Ran:
                    _race.Ran = true;

                    // The interceptor is synchronous; the save runs on the thread pool so it never waits on the test's
                    // synchronization context.
                    Task.Run(_race.CompetingSave).GetAwaiter().GetResult();
                    break;
            }

            invocation.Proceed();
        }
    }
}
