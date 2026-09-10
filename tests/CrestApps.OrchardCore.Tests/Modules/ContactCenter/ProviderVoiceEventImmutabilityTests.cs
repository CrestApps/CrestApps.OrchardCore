using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins that a provider voice event cannot be changed once it has been handed over. It is simultaneously a
/// public provider contract and something ingestion has to adjust — the provider identity is canonicalized and
/// the idempotency key is scoped by it — and while it was mutable those adjustments were applied to the
/// caller's own instance, so ingestion defended itself with a hand-written copy. That copy was one more thing
/// to keep complete, and it was not: it dropped <c>HangupCause</c>, and because a session infers a cause when
/// none is supplied, every call reported the inferred cause instead of the one the provider gave, with nothing
/// anywhere to say the real one had been lost. Copying is now the language's, which is complete by
/// construction — provided the type stays immutable, which is what these tests hold.
/// </summary>
public sealed class ProviderVoiceEventImmutabilityTests
{
    [Fact]
    public void TheProviderVoiceEvent_IsARecord_SoCopyingItCopiesEveryMember()
    {
        // Arrange
        // A record's synthesized clone copies every field, including any added later, which is exactly the
        // guarantee the hand-written copy failed to provide. Losing the record-ness silently reinstates the
        // class of defect this test exists for.
        var clone = typeof(ProviderVoiceEvent).GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // Assert
        Assert.NotNull(clone);
    }

    [Fact]
    public void NoProviderVoiceEventProperty_CanBeAssignedAfterConstruction()
    {
        // Arrange
        var settable = typeof(ProviderVoiceEvent)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.SetMethod is not null && !IsInitOnly(property.SetMethod))
            .Select(property => property.Name);

        // Assert
        Assert.Empty(settable);
    }

    [Fact]
    public void EveryProviderVoiceEventProperty_IsEitherAValueOrAReadOnlyCollection()
    {
        // Arrange
        // An init-only property still aliases whatever the caller passed, so a mutable collection would leave
        // the event changeable through the reference the caller kept.
        var mutable = typeof(ProviderVoiceEvent)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => !IsValueLike(property.PropertyType) && !IsReadOnlyCollection(property.PropertyType))
            .Select(property => $"{property.Name} ({property.PropertyType.Name})");

        // Assert
        Assert.Empty(mutable);
    }

    [Fact]
    public void Metadata_IsSnapshotted_WhenTheCallerKeepsItsOwnReference()
    {
        // Arrange
        var supplied = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["reason"] = "normal-clearing",
        };

        var providerEvent = new ProviderVoiceEvent
        {
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            Metadata = supplied,
        };

        // Act
        supplied["reason"] = "rewritten";
        supplied["added"] = "after-the-fact";

        // Assert
        Assert.Equal("normal-clearing", providerEvent.Metadata["reason"]);
        Assert.False(providerEvent.Metadata.ContainsKey("added"));
    }

    [Fact]
    public void EveryCollectionProperty_IsSnapshotted_NotAliasedFromTheCaller()
    {
        // Arrange
        // Declaring a collection through a read-only interface says nothing about aliasing: the caller can
        // still hold the mutable instance it passed. This walks every collection property the type declares,
        // so a property added later without a snapshot fails here instead of relying on someone remembering
        // to write a test named after it.
        var collections = typeof(ProviderVoiceEvent)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => !IsValueLike(property.PropertyType))
            .ToList();

        Assert.NotEmpty(collections);

        var aliased = new List<string>();

        foreach (var property in collections)
        {
            var supplied = CreateMutableInstance(property);

            var providerEvent = new ProviderVoiceEvent
            {
                ProviderName = "Asterisk",
                ProviderCallId = "call-1",
            };

            property.SetValue(providerEvent, supplied.Instance);

            var before = Count(property.GetValue(providerEvent));

            // Act
            supplied.Grow();

            // Assert
            if (Count(property.GetValue(providerEvent)) != before)
            {
                aliased.Add($"{property.Name} ({property.PropertyType.Name})");
            }
        }

        Assert.Empty(aliased);
    }

    [Fact]
    public void Metadata_KeepsTheComparerTheProviderSupplied()
    {
        // Arrange
        // Providers key their metadata case-insensitively. A snapshot that quietly became case-sensitive would
        // change what consumers can find without changing anything they wrote.
        var providerEvent = new ProviderVoiceEvent
        {
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AsteriskState"] = "Up",
            },
        };

        // Assert
        Assert.True(providerEvent.Metadata.ContainsKey("asteriskstate"));
    }

    [Fact]
    public void Metadata_KeepsTheComparer_WhenItIsCarriedOverFromAnotherEvent()
    {
        // Arrange
        // Deriving one event from another is the documented way to adjust a provider event, and the property
        // hands back its own snapshot type rather than the dictionary the provider passed. If only a
        // `Dictionary<string, string>` were recognized, every derivation would silently downgrade a
        // case-insensitive snapshot to a case-sensitive one, which changes what consumers can find without
        // changing anything they wrote.
        var original = new ProviderVoiceEvent
        {
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AsteriskState"] = "Up",
            },
        };

        // Act
        var derived = new ProviderVoiceEvent
        {
            ProviderName = original.ProviderName,
            ProviderCallId = original.ProviderCallId,
            Metadata = original.Metadata,
        };

        // Assert
        Assert.True(derived.Metadata.ContainsKey("asteriskstate"));
    }

    [Fact]
    public void Metadata_KeepsTheComparer_WhenTheSourceIsNotADictionary()
    {
        // Arrange
        var supplied = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
        supplied["AsteriskState"] = "Up";

        // Act
        var providerEvent = new ProviderVoiceEvent
        {
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            Metadata = supplied.ToImmutable(),
        };

        // Assert
        Assert.True(providerEvent.Metadata.ContainsKey("asteriskstate"));
    }

    [Fact]
    public void Metadata_IsAnEmptyDictionary_WhenNoneWasSupplied()
    {
        // Act
        var providerEvent = new ProviderVoiceEvent
        {
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
        };

        // Assert
        Assert.NotNull(providerEvent.Metadata);
        Assert.Empty(providerEvent.Metadata);
    }

    [Fact]
    public void AdjustingAProviderVoiceEvent_LeavesTheCallersInstanceUntouched()
    {
        // Arrange
        var original = new ProviderVoiceEvent
        {
            ProviderName = "Default Asterisk",
            ProviderCallId = "call-1",
            IdempotencyKey = "delivery-1",
            HangupCause = HangupCause.NormalClearing,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["leg"] = "a",
            },
        };

        // Act
        var adjusted = original with
        {
            ProviderName = "Asterisk",
            IdempotencyKey = "asterisk:delivery-1",
        };

        // Assert
        Assert.Equal("Default Asterisk", original.ProviderName);
        Assert.Equal("delivery-1", original.IdempotencyKey);
        Assert.Equal("Asterisk", adjusted.ProviderName);
        Assert.Equal("asterisk:delivery-1", adjusted.IdempotencyKey);
        Assert.Equal(HangupCause.NormalClearing, adjusted.HangupCause);
        Assert.Equal("a", adjusted.Metadata["leg"]);
    }

    private static bool IsInitOnly(MethodInfo setter)
        => setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    private static int Count(object value)
    {
        if (value is null)
        {
            return 0;
        }

        var count = 0;

        foreach (var _ in (IEnumerable)value)
        {
            count++;
        }

        return count;
    }

    private static MutableCollection CreateMutableInstance(PropertyInfo property)
    {
        var type = property.PropertyType;

        // A property whose shape this cannot build is reported rather than skipped, because skipping is how a
        // sweep like this quietly stops covering the member that needed it most.
        Assert.True(type.IsGenericType, $"{property.Name} is a reference-typed property of an unsupported shape ({type.Name}); extend this gate to cover it.");

        var definition = type.GetGenericTypeDefinition();
        var arguments = type.GetGenericArguments();

        if (definition == typeof(IReadOnlyDictionary<,>))
        {
            var dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments));
            dictionary.Add(CreateSample(arguments[0], property, 0), CreateSample(arguments[1], property, 0));

            return new MutableCollection(dictionary, () => dictionary.Add(CreateSample(arguments[0], property, 1), CreateSample(arguments[1], property, 1)));
        }

        if (definition == typeof(IReadOnlyList<>) || definition == typeof(IReadOnlyCollection<>) || definition == typeof(IEnumerable<>))
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(arguments[0]));
            list.Add(CreateSample(arguments[0], property, 0));

            return new MutableCollection(list, () => list.Add(CreateSample(arguments[0], property, 1)));
        }

        Assert.Fail($"{property.Name} is a reference-typed property of an unsupported shape ({type.Name}); extend this gate to cover it.");

        return null;
    }

    private static object CreateSample(Type type, PropertyInfo property, int ordinal)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string))
        {
            return $"sample-{ordinal}";
        }

        if (underlying.IsEnum)
        {
            return Enum.GetValues(underlying).GetValue(0);
        }

        if (underlying.IsValueType)
        {
            return Activator.CreateInstance(underlying);
        }

        var parameterless = underlying.GetConstructor(Type.EmptyTypes);

        Assert.True(parameterless is not null, $"{property.Name} holds {underlying.Name}, which this gate cannot sample; extend it to cover that type.");

        return parameterless.Invoke(null);
    }

    private sealed record MutableCollection(object Instance, Action Grow);

    private static bool IsValueLike(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        return underlying.IsValueType || underlying == typeof(string);
    }

    private static bool IsReadOnlyCollection(Type type)
    {
        if (!type.IsInterface || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            return false;
        }

        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;

        return definition == typeof(IReadOnlyDictionary<,>) ||
            definition == typeof(IReadOnlyCollection<>) ||
            definition == typeof(IReadOnlyList<>) ||
            definition == typeof(IEnumerable<>);
    }
}
