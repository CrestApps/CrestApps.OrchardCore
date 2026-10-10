using System.Reflection;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// Cloning a batch lets an operator run a load that worked again without filling in its form a second time. The copy
/// must carry every setting of the source and nothing of its load: a copy that kept the source's status or counts
/// would look loaded, or report a load it never ran.
/// </summary>
public sealed class ActivityBatchClonerTests
{
    // The identity of the copy comes from the freshly initialized batch; the load state is reset. Every other public
    // property is a setting and must be copied as is -- a property added to the batch later is covered here too.
    private static readonly HashSet<string> _notCopied =
    [
        nameof(OmnichannelActivityBatch.ItemId),
        nameof(OmnichannelActivityBatch.DisplayText),
        nameof(OmnichannelActivityBatch.CreatedUtc),
        nameof(OmnichannelActivityBatch.ModifiedUtc),
        nameof(OmnichannelActivityBatch.OwnerId),
        nameof(OmnichannelActivityBatch.Author),
        nameof(OmnichannelActivityBatch.Status),
        nameof(OmnichannelActivityBatch.TotalLoaded),
        nameof(OmnichannelActivityBatch.TotalMatched),
        nameof(OmnichannelActivityBatch.TotalSkippedAsDuplicate),
        nameof(OmnichannelActivityBatch.TotalSkippedAsOptedOut),
        nameof(OmnichannelActivityBatch.TotalSkippedAsSharedNumberOptedOut),
        nameof(OmnichannelActivityBatch.TotalSkippedForNoDestination),
        nameof(OmnichannelActivityBatch.TotalSkippedAsNotInService),
        nameof(OmnichannelActivityBatch.TotalSkippedByLimit),
        nameof(OmnichannelActivityBatch.TotalSkippedAsConverted),
        nameof(OmnichannelActivityBatch.TotalSkippedAsExistingContact),
        nameof(OmnichannelActivityBatch.Properties),
    ];

    [Fact]
    public void CreateCopy_CopiesEverySetting()
    {
        var source = LoadedSource();

        var copy = ActivityBatchCloner.CreateCopy(source, NewBatch(), "Copy");

        var settings = SettableProperties().Where(property => !_notCopied.Contains(property.Name)).ToList();

        Assert.NotEmpty(settings);

        foreach (var property in settings)
        {
            Assert.Equal(property.GetValue(source), property.GetValue(copy));
        }
    }

    [Fact]
    public void CreateCopy_CopiesThePropertyBag_WithoutSharingIt()
    {
        var source = LoadedSource();

        var copy = ActivityBatchCloner.CreateCopy(source, NewBatch(), "Copy");

        Assert.True(copy.TryGet<LeadBatchFilter>(out var filter));
        Assert.Equal("purchased-list-import", filter.ImportEntryId);
        Assert.Equal(["status-1"], filter.StatusIds);
        Assert.True(copy.TryGet<LeadAIConversionSettings>(out var conversion));
        Assert.True(conversion.Enabled);
        Assert.NotSame(source.Properties, copy.Properties);
    }

    [Fact]
    public void CreateCopy_CopiesArraysWithoutSharingThem()
    {
        var source = LoadedSource();

        var copy = ActivityBatchCloner.CreateCopy(source, NewBatch(), "Copy");

        Assert.NotSame(source.UserIds, copy.UserIds);
        Assert.NotSame(source.TimeZoneIds, copy.TimeZoneIds);
    }

    [Fact]
    public void CreateCopy_TakesItsIdentityFromTheNewBatch()
    {
        var source = LoadedSource();
        var newBatch = NewBatch();

        var copy = ActivityBatchCloner.CreateCopy(source, newBatch, "Source (copy)");

        Assert.Equal(newBatch.ItemId, copy.ItemId);
        Assert.Equal("Source (copy)", copy.DisplayText);
        Assert.Equal(newBatch.CreatedUtc, copy.CreatedUtc);
        Assert.Null(copy.ModifiedUtc);
        Assert.Equal(newBatch.OwnerId, copy.OwnerId);
        Assert.Equal(newBatch.Author, copy.Author);
        Assert.NotSame(source, copy);
    }

    [Theory]
    [InlineData(OmnichannelActivityBatchStatus.New)]
    [InlineData(OmnichannelActivityBatchStatus.Started)]
    [InlineData(OmnichannelActivityBatchStatus.Loading)]
    [InlineData(OmnichannelActivityBatchStatus.Loaded)]
    public void CreateCopy_ResetsTheLoadState_WhateverTheStatusOfTheSource(OmnichannelActivityBatchStatus status)
    {
        var source = LoadedSource();
        source.Status = status;

        var copy = ActivityBatchCloner.CreateCopy(source, NewBatch(), "Copy");

        Assert.Equal(OmnichannelActivityBatchStatus.New, copy.Status);
        Assert.Null(copy.TotalLoaded);
        Assert.Null(copy.TotalMatched);
        Assert.Equal(0, copy.TotalSkippedAsDuplicate);
        Assert.Equal(0, copy.TotalSkippedAsOptedOut);
        Assert.Equal(0, copy.TotalSkippedAsSharedNumberOptedOut);
        Assert.Equal(0, copy.TotalSkippedForNoDestination);
        Assert.Equal(0, copy.TotalSkippedAsNotInService);
        Assert.Equal(0, copy.TotalSkippedByLimit);
        Assert.Equal(0, copy.TotalSkippedAsConverted);
        Assert.Equal(0, copy.TotalSkippedAsExistingContact);
    }

    [Fact]
    public void CreateCopy_LeavesTheSourceUntouched()
    {
        var source = LoadedSource();

        ActivityBatchCloner.CreateCopy(source, NewBatch(), "Copy");

        Assert.Equal("source-1", source.ItemId);
        Assert.Equal(OmnichannelActivityBatchStatus.Loaded, source.Status);
        Assert.Equal(TotalLoaded, source.TotalLoaded);
    }

    private const long TotalLoaded = 42;

    private static OmnichannelActivityBatch NewBatch()
        => new()
        {
            ItemId = "new-1",
            CreatedUtc = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc),
            OwnerId = "user-2",
            Author = "cloner",
        };

    // A batch with every property set to a value no default could match, then loaded.
    private static OmnichannelActivityBatch LoadedSource()
    {
        var defaults = new OmnichannelActivityBatch();
        var source = new OmnichannelActivityBatch();
        var index = 0;

        foreach (var property in SettableProperties().Where(property => property.Name != nameof(OmnichannelActivityBatch.Properties)))
        {
            property.SetValue(source, NonDefaultValue(property.PropertyType, property.GetValue(defaults), ++index));
        }

        source.ItemId = "source-1";
        source.DisplayText = "Source";
        source.Status = OmnichannelActivityBatchStatus.Loaded;
        source.TotalLoaded = TotalLoaded;
        source.TotalMatched = 50;

        source.Put(new LeadBatchFilter
        {
            ImportEntryId = "purchased-list-import",
            StatusIds = ["status-1"],
        });
        source.Put(new LeadAIConversionSettings
        {
            Enabled = true,
        });

        return source;
    }

    private static IEnumerable<PropertyInfo> SettableProperties()
        => typeof(OmnichannelActivityBatch)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0);

    private static object NonDefaultValue(Type type, object defaultValue, int seed)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string))
        {
            return $"value-{seed}";
        }

        if (underlying == typeof(bool))
        {
            // Some flags default to true, so the opposite of the default is what proves the copy.
            return !(defaultValue as bool? ?? false);
        }

        if (underlying == typeof(int))
        {
            return 1000 + seed;
        }

        if (underlying == typeof(long))
        {
            return 1000L + seed;
        }

        if (underlying == typeof(DateTime))
        {
            return new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(seed);
        }

        if (underlying.IsEnum)
        {
            return Enum.GetValues(underlying).Cast<object>().First(value => !value.Equals(defaultValue));
        }

        if (underlying == typeof(string[]))
        {
            return new[] { $"a-{seed}", $"b-{seed}" };
        }

        throw new NotSupportedException($"Add a non-default value for {type} so the clone test covers it.");
    }
}
