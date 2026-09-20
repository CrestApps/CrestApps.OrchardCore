using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Entities;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Framework.Entities;

/// <summary>
/// Pins that the framework property bag writes and reads what the host's entity helper did.
/// </summary>
/// <remarks>
/// <para>
/// Four models stopped deriving from the host's entity base and declare the property bag themselves, on the
/// stated ground that the stored document does not change. What is stored is not only the bag's key and
/// casing: it is every value inside it, and the serializer's own defaults disagree with the host on several
/// of them. An enum is written as a number rather than as its name, a null property is written rather than
/// omitted, and a <see cref="DateTime"/> is written in the round-trip shape rather than the shape the host's
/// own converter produced.
/// </para>
/// <para>
/// The null case is cosmetic. The others are not: a bag holds whatever any version wrote into it, so a read
/// that throws is treated as the aspect being absent, and an aspect stored by the host with an enum in it
/// would come back as a new instance with every value lost. These tests compare against the host helper
/// directly, so the compatibility is measured rather than described.
/// </para>
/// </remarks>
public sealed class JsonPropertyBagTests
{
    [Fact]
    public void Put_WritesTheTextTheHostEntityHelperWrote()
    {
        // Arrange
        var aspect = CreateAspect();

        var host = new HostEntity();
        host.Put(aspect);

        var bag = new JsonObject();

        // Act
        JsonPropertyBag.Put(bag, aspect);

        // Assert
        Assert.Equal(host.Properties.ToJsonString(), bag.ToJsonString());
    }

    /// <summary>
    /// The shapes a <see cref="DateTime"/> can arrive in, each written by both helpers and compared.
    /// </summary>
    /// <remarks>
    /// A whole-second UTC value is the one shape where the serializer's own format and the host's converter
    /// agree, so a fixture confined to it certifies the compatibility claim without testing it. These are the
    /// three shapes that disagree unless the framework reproduces the host's converter.
    /// </remarks>
    /// <param name="kind">The kind the value carries.</param>
    /// <param name="ticks">Extra sub-second ticks on the value.</param>
    [Theory]
    [InlineData(DateTimeKind.Utc, 0)]
    [InlineData(DateTimeKind.Utc, 1234567)]
    [InlineData(DateTimeKind.Unspecified, 0)]
    [InlineData(DateTimeKind.Local, 0)]
    public void Put_WritesEveryDateTimeShapeTheWayTheHostWroteIt(DateTimeKind kind, long ticks)
    {
        // Arrange
        var aspect = CreateAspect();
        aspect.When = new DateTime(2026, 1, 2, 3, 4, 5, kind).AddTicks(ticks);

        var host = new HostEntity();
        host.Put(aspect);

        var bag = new JsonObject();

        // Act
        JsonPropertyBag.Put(bag, aspect);

        // Assert
        Assert.Equal(
            host.Properties["SampleAspect"]["When"].ToJsonString(),
            bag["SampleAspect"]["When"].ToJsonString());
    }

    /// <summary>
    /// Pins that a value the host wrote reads back as the same instant.
    /// </summary>
    /// <param name="kind">The kind the value carries.</param>
    /// <param name="ticks">Extra sub-second ticks on the value.</param>
    [Theory]
    [InlineData(DateTimeKind.Utc, 0)]
    [InlineData(DateTimeKind.Utc, 1234567)]
    [InlineData(DateTimeKind.Unspecified, 0)]
    [InlineData(DateTimeKind.Local, 0)]
    public void TryGet_ReadsEveryDateTimeShapeTheHostWrote(DateTimeKind kind, long ticks)
    {
        // Arrange
        var aspect = CreateAspect();
        aspect.When = new DateTime(2026, 1, 2, 3, 4, 5, kind).AddTicks(ticks);

        var host = new HostEntity();
        host.Put(aspect);

        var hostRead = host.GetOrCreate<SampleAspect>();

        // Act
        Assert.True(JsonPropertyBag.TryGet<SampleAspect>(host.Properties, out var read));

        // Assert
        Assert.Equal(hostRead.When, read.When);
    }

    [Fact]
    public void TryGet_ReadsAnAspectTheHostEntityHelperWrote()
    {
        // Arrange
        var aspect = CreateAspect();

        var host = new HostEntity();
        host.Put(aspect);

        // Act
        var found = JsonPropertyBag.TryGet<SampleAspect>(host.Properties, out var read);

        // Assert
        Assert.True(found, "An aspect the host wrote must still be readable, or every value in it is lost.");
        Assert.Equal(aspect.Kind, read.Kind);
        Assert.Equal(aspect.When, read.When);
        Assert.Equal(aspect.Gap, read.Gap);
        Assert.Equal(aspect.Count, read.Count);
        Assert.Null(read.Absent);
    }

    [Fact]
    public void Put_WritesAnEnumByName()
    {
        // The one difference that loses data rather than bytes, pinned on its own so a change to the
        // serializer settings says which guarantee it broke.
        var bag = new JsonObject();

        JsonPropertyBag.Put(bag, CreateAspect());

        Assert.Equal("Second", bag["SampleAspect"]["Kind"].GetValue<string>());
    }

    [Fact]
    public void Put_OmitsANullProperty()
    {
        var bag = new JsonObject();

        JsonPropertyBag.Put(bag, CreateAspect());

        Assert.False(bag["SampleAspect"].AsObject().ContainsKey("Absent"));
    }

    [Fact]
    public void Alter_RoundTripsThroughTheSameSettings()
    {
        // Arrange
        var bag = new JsonObject();
        JsonPropertyBag.Put(bag, CreateAspect());

        // Act
        JsonPropertyBag.Alter<SampleAspect>(bag, aspect => aspect.Count++);

        // Assert
        Assert.True(JsonPropertyBag.TryGet<SampleAspect>(bag, out var read));
        Assert.Equal(4, read.Count);
        Assert.Equal(SampleKind.Second, read.Kind);
    }

    /// <summary>
    /// Pins that the stored shape is the bag's own, not whatever another feature configured.
    /// </summary>
    /// <remarks>
    /// <see cref="ExtensibleEntityExtensions.JsonSerializerOptions"/> is a settable process-wide static that a
    /// host, or the AI suite's own options initializer, may replace at startup. Durable tenant data must not
    /// change shape because an unrelated feature added a converter or a naming policy, so the bag holds its
    /// own read-only instance.
    /// </remarks>
    [Fact]
    public void Put_IsUnaffectedByAReconfiguredExtensibleEntitySerializer()
    {
        // Arrange
        var original = ExtensibleEntityExtensions.JsonSerializerOptions;
        var bag = new JsonObject();

        try
        {
            ExtensibleEntityExtensions.JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            };

            // Act
            JsonPropertyBag.Put(bag, CreateAspect());
        }
        finally
        {
            ExtensibleEntityExtensions.JsonSerializerOptions = original;
        }

        // Assert
        var stored = bag["SampleAspect"].AsObject();

        Assert.True(stored.ContainsKey("Kind"), "The stored property names must not follow another feature's naming policy.");
        Assert.False(stored.ContainsKey("Absent"), "A null property must stay omitted however the shared serializer is configured.");
    }

    private static SampleAspect CreateAspect()
    {
        return new SampleAspect
        {
            Kind = SampleKind.Second,
            When = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Gap = TimeSpan.FromMinutes(90),
            Absent = null,
            Count = 3,
        };
    }

    private sealed class HostEntity : Entity
    {
    }

    private sealed class SampleAspect
    {
        public SampleKind Kind { get; set; }

        public DateTime When { get; set; }

        public TimeSpan Gap { get; set; }

        public string Absent { get; set; }

        public int Count { get; set; }
    }

    private enum SampleKind
    {
        First,
        Second,
    }
}
