using System.Collections;
using System.Reflection;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Gates the conversion of stored Contact Center events against a real database, and requires it on every
/// path that reads one. The durable event log is read from post-commit dispatch, outbox redelivery, projection
/// replay and reporting; a path that skipped the conversion would not fail, it would return a stale payload as
/// though it were current, so the coverage is discovered by reflection rather than listed by hand.
/// </summary>
public sealed class InteractionEventUpcastPersistenceTests
{
    private const string SeededInteractionId = "interaction-1";
    private const string SeededEventId = "event-seeded-1";
    private const string SeededAggregateType = "AgentProfile";

    private static readonly DateTime _occurredUtc = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EveryStoreReadPath_AppliesTheUpcastToWhatItReturns()
    {
        // Arrange
        // The seeded event claims a schema version this release does not understand, which the conversion must
        // refuse. Any read path that returns it without failing is a path that never converted it.
        var databasePath = DatabasePath("coverage");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var storedVersion = ContactCenterConstants.CurrentEventSchemaVersion + 5;

            await SeedAsync(store, schemaVersion: storedVersion);

            var readMethods = ReadMethods().ToArray();

            Assert.True(
                readMethods.Length >= 5,
                $"Expected the event store to expose at least five read paths; found {readMethods.Length}. The gate cannot have stopped discovering them.");

            // Act & Assert
            foreach (var method in readMethods)
            {
                // First prove the arguments this gate generates actually reach the seeded row. Without this the
                // gate would be satisfied by a read path that returns nothing, which is how a filtered path
                // added later could escape the coverage it is supposed to be under.
                await using (var reachable = store.CreateSession())
                {
                    var permissive = new InteractionEventStore(
                        reachable,
                        new DefaultInteractionEventUpcastService([], storedVersion));

                    var returned = await InvokeAsync(method, permissive);

                    Assert.True(
                        Returns(returned),
                        $"'{method.Name}' did not return the seeded event with the arguments this gate supplies, so requiring it to convert that event would prove nothing. Teach the gate to reach the row rather than leaving the read path uncovered.");
                }

                await using var session = store.CreateSession();
                var eventStore = new InteractionEventStore(session, new DefaultInteractionEventUpcastService([]));

                InteractionEventUpcastException failure = null;

                try
                {
                    await InvokeAsync(method, eventStore);
                }
                catch (InteractionEventUpcastException exception)
                {
                    failure = exception;
                }

                Assert.True(
                    failure is not null,
                    $"'{method.Name}' returned the seeded event without converting it. Every path that reads a stored event has to convert it, because a stale payload deserializes into today's type without complaint.");

                Assert.Contains("written by a newer release", failure.Message, StringComparison.Ordinal);
            }
        }
        finally
        {
            store.Dispose();
            TemporarySqliteDatabase.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ReadingAnEventStoredAtAnOlderVersion_ReturnsThePayloadInTodaysShape()
    {
        // Arrange
        var databasePath = DatabasePath("convert");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, schemaVersion: 1, data: """{"Reason":"break"}""");

            await using var session = store.CreateSession();
            var eventStore = new InteractionEventStore(
                session,
                new DefaultInteractionEventUpcastService([new RenameReasonUpcaster()], 2));

            // Act
            var loaded = await eventStore.FindByIdAsync(SeededEventId, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, loaded.SchemaVersion);
            Assert.Equal("break", loaded.GetData<PresencePayload>().PresenceReason);
        }
        finally
        {
            store.Dispose();
            TemporarySqliteDatabase.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ConvertingOnRead_DoesNotRewriteTheStoredEvent()
    {
        // Arrange
        // The conversion serves the reader. Persisting it would rewrite history under an operator who is
        // reading it, and would do so from whichever node happened to read the row first.
        var databasePath = DatabasePath("readonly");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, schemaVersion: 1, data: """{"Reason":"break"}""");

            await using (var session = store.CreateSession())
            {
                var eventStore = new InteractionEventStore(
                    session,
                    new DefaultInteractionEventUpcastService([new RenameReasonUpcaster()], 2));

                await eventStore.FindByIdAsync(SeededEventId, TestContext.Current.CancellationToken);

                // The converted event is read in a session that goes on to persist unrelated work, because a
                // read that quietly enrolled the converted document in the unit of work would be flushed by
                // whatever the caller saved next rather than by the read itself.
                await eventStore.CreateAsync(
                    new InteractionEvent
                    {
                        ItemId = "event-unrelated-1",
                        InteractionId = SeededInteractionId,
                        EventType = ContactCenterConstants.Events.AgentReserved,
                        AggregateId = SeededInteractionId,
                        AggregateType = SeededAggregateType,
                        OccurredUtc = _occurredUtc,
                        SchemaVersion = 2,
                    },
                    TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var verification = store.CreateSession();
            var stored = await verification
                .Query<InteractionEvent, InteractionEventIndex>(
                    index => index.ItemId == SeededEventId,
                    collection: ContactCenterConstants.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, stored.SchemaVersion);
            Assert.Equal("""{"Reason":"break"}""", stored.Data);
        }
        finally
        {
            store.Dispose();
            TemporarySqliteDatabase.Delete(databasePath);
        }
    }

    private static IEnumerable<MethodInfo> ReadMethods()
    {
        return typeof(InteractionEventStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.DeclaringType != typeof(object) && ReturnsEvents(method))
            .OrderBy(method => method.Name, StringComparer.Ordinal);
    }

    private static bool ReturnsEvents(MethodInfo method)
    {
        var returnType = method.ReturnType;

        if (returnType.IsGenericType)
        {
            var definition = returnType.GetGenericTypeDefinition();

            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
            {
                returnType = returnType.GetGenericArguments()[0];
            }
        }

        return Mentions(returnType);
    }

    private static bool Mentions(Type type)
    {
        if (type == typeof(InteractionEvent))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(Mentions);
    }

    private static bool Returns(object result)
    {
        if (result is InteractionEvent single)
        {
            return string.Equals(single.ItemId, SeededEventId, StringComparison.Ordinal);
        }

        // A read path may wrap its events in a result object, so any member carrying them is unwrapped rather
        // than the result being read as empty.
        if (result is not null && result is not IEnumerable)
        {
            foreach (var property in result.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (Mentions(property.PropertyType) && Returns(property.GetValue(result)))
                {
                    return true;
                }
            }
        }

        if (result is IEnumerable sequence)
        {
            foreach (var item in sequence)
            {
                if (item is InteractionEvent stored && string.Equals(stored.ItemId, SeededEventId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static async Task<object> InvokeAsync(MethodInfo method, InteractionEventStore eventStore)
    {
        var target = method;

        if (method.IsGenericMethodDefinition)
        {
            var parameters = method.GetGenericArguments();

            Assert.True(
                parameters.Length == 1 && parameters[0].GetGenericParameterConstraints().Contains(typeof(QueryContext)),
                $"'{method.Name}' is a generic read path this gate does not know how to close. Teach the gate rather than removing it from discovery.");

            target = method.MakeGenericMethod(typeof(QueryContext));
        }

        var arguments = target.GetParameters().Select(Argument).ToArray();
        var result = target.Invoke(eventStore, arguments);

        switch (result)
        {
            case Task task:
                await task;

                return Completed(task);

            case ValueTask valueTask:
                await valueTask;

                return null;
        }

        // A ValueTask<T> is neither, so it is awaited through its own AsTask.
        var asTask = result.GetType().GetMethod(nameof(ValueTask<int>.AsTask));

        Assert.NotNull(asTask);

        var awaited = (Task)asTask.Invoke(result, null);

        await awaited;

        return Completed(awaited);
    }

    private static object Completed(Task task)
    {
        var resultProperty = task.GetType().GetProperty(nameof(Task<int>.Result));

        return resultProperty is null || resultProperty.PropertyType == typeof(void)
            ? null
            : resultProperty.GetValue(task);
    }

    private static object Argument(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;

        if (type == typeof(CancellationToken))
        {
            return TestContext.Current.CancellationToken;
        }

        if (type == typeof(string))
        {
            // Every string parameter on a read path identifies something, and the seeded event answers to its
            // own identifier, its interaction's, and the aggregate type it was recorded against.
            if (parameter.Name.Contains("aggregateType", StringComparison.OrdinalIgnoreCase))
            {
                return SeededAggregateType;
            }

            return parameter.Name.Contains("interaction", StringComparison.OrdinalIgnoreCase)
                ? SeededInteractionId
                : SeededEventId;
        }

        if (type == typeof(int))
        {
            // A skip past the seeded row, or a page beyond the first, would return nothing and let an
            // unconverted read path pass by returning an empty result rather than by converting anything.
            if (parameter.Name.Contains("skip", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return parameter.Name.Equals("page", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 100;
        }

        if (type == typeof(DateTime))
        {
            return _occurredUtc.AddYears(1);
        }

        if (type == typeof(IEnumerable<string>))
        {
            // A set-valued filter is supplied every value the seeded event answers to, so a read path that
            // filters on identifiers and one that filters on event types both reach it. Supplying a set that
            // matches nothing would let an unconverted path pass by returning an empty result.
            return new[] { SeededEventId, SeededInteractionId, ContactCenterConstants.Events.AgentReserved };
        }

        if (type == typeof(QueryContext))
        {
            return null;
        }

        Assert.Fail(
            $"The event store read path takes a '{type.FullName}' parameter named '{parameter.Name}' that this gate does not know how to supply. Teach the gate so the new read path is covered.");

        return null;
    }

    private static async Task SeedAsync(IStore store, int schemaVersion, string data = null)
    {
        await using var session = store.CreateSession();

        session.Save(
            new InteractionEvent
            {
                ItemId = SeededEventId,
                InteractionId = SeededInteractionId,
                EventType = ContactCenterConstants.Events.AgentReserved,
                AggregateId = SeededInteractionId,
                AggregateType = SeededAggregateType,
                OccurredUtc = _occurredUtc,
                SchemaVersion = schemaVersion,
                Data = data,
            },
            collection: ContactCenterConstants.CollectionName);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new InteractionEventIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            ContactCenterConstants.CollectionName,
            TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new InteractionEventIndexMigrations(store)
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        };

        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static string DatabasePath(string name)
    {
        return Path.Combine(Path.GetTempPath(), $"cc-upcast-{name}-{Guid.NewGuid():N}.db");
    }

    private sealed class PresencePayload
    {
        public string PresenceReason { get; set; }
    }

    private sealed class RenameReasonUpcaster : IInteractionEventUpcaster
    {
        public string EventType => null;

        public int FromVersion => 1;

        public System.Text.Json.Nodes.JsonNode Upcast(System.Text.Json.Nodes.JsonNode payload)
        {
            if (payload is not System.Text.Json.Nodes.JsonObject json)
            {
                return payload;
            }

            if (json.Remove("Reason", out var reason))
            {
                json["PresenceReason"] = reason;
            }

            return json;
        }
    }
}
