using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Handles filtering for bulk manage activity queries by adding JOINs and WHERE
/// clauses to the SQL builder. The resulting query executes as a single server-side
/// statement with no in-memory materialization or parameter limit concerns.
/// </summary>
public sealed class BulkManageActivityFilterHandler : IBulkManageActivityFilterHandler
{
    private const string ContactAlias = "oci";
    private const string ContactPhoneAlias = "ocpi";
    private const string DncAlias = "dnc";

    /// <inheritdoc/>
    public Task FilteringAsync(BulkManageActivityFilterContext context, CancellationToken cancellationToken = default)
    {
        var filter = context.Filter;
        var builder = context.SqlBuilder;
        var dialect = context.Dialect;
        var actAlias = context.ActivityTableAlias;

        if (filter.UrgencyLevel.HasValue)
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.UrgencyLevel))}";
            builder.Parameters["@UrgencyLevel"] = (int)filter.UrgencyLevel.Value;
            builder.WhereAnd($"{col} = @UrgencyLevel");
        }

        if (!string.IsNullOrEmpty(filter.SubjectContentType))
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.SubjectContentType))}";
            builder.Parameters["@SubjectContentType"] = filter.SubjectContentType;
            builder.WhereAnd($"{col} = @SubjectContentType");
        }

        if (!string.IsNullOrEmpty(filter.Channel))
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.Channel))}";
            builder.Parameters["@Channel"] = filter.Channel;
            builder.WhereAnd($"{col} = @Channel");
        }

        if (!string.IsNullOrEmpty(filter.Source))
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.Source))}";
            builder.Parameters["@Source"] = filter.Source;
            builder.WhereAnd($"{col} = @Source");
        }

        if (filter.InteractionType.HasValue)
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.InteractionType))}";
            builder.Parameters["@InteractionType"] = (int)filter.InteractionType.Value;
            builder.WhereAnd($"{col} = @InteractionType");
        }

        if (filter.Status.HasValue)
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.Status))}";
            builder.Parameters["@Status"] = (int)filter.Status.Value;
            builder.WhereAnd($"{col} = @Status");
        }

        if (filter.AssignmentStatus.HasValue)
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.AssignmentStatus))}";
            builder.Parameters["@AssignmentStatus"] = (int)filter.AssignmentStatus.Value;
            builder.WhereAnd($"{col} = @AssignmentStatus");
        }

        if (!string.IsNullOrEmpty(filter.CampaignId))
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.CampaignId))}";
            builder.Parameters["@CampaignId"] = filter.CampaignId;
            builder.WhereAnd($"{col} = @CampaignId");
        }

        if (filter.AssignedToUserIds is { Length: > 0 })
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.AssignedToId))}";
            var placeholders = new string[filter.AssignedToUserIds.Length];

            for (var i = 0; i < filter.AssignedToUserIds.Length; i++)
            {
                var paramName = $"@AssignedTo{i}";
                placeholders[i] = paramName;
                builder.Parameters[paramName] = filter.AssignedToUserIds[i];
            }

            builder.WhereAnd($"{col} IN ({string.Join(", ", placeholders)})");
        }

        if (!string.IsNullOrEmpty(filter.AttemptFilter))
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.Attempts))}";
            ApplyAttemptFilter(builder, col, filter.AttemptFilter);
        }

        if (filter.ScheduledFrom.HasValue)
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.ScheduledUtc))}";
            builder.Parameters["@ScheduledFrom"] = filter.ScheduledFrom.Value;
            builder.WhereAnd($"{col} >= @ScheduledFrom");
        }

        if (filter.ScheduledTo.HasValue)
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.ScheduledUtc))}";
            builder.Parameters["@ScheduledTo"] = filter.ScheduledTo.Value;
            builder.WhereAnd($"{col} <= @ScheduledTo");
        }

        if (filter.CreatedFrom.HasValue)
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.CreatedUtc))}";
            builder.Parameters["@CreatedFrom"] = filter.CreatedFrom.Value;
            builder.WhereAnd($"{col} >= @CreatedFrom");
        }

        if (filter.CreatedTo.HasValue)
        {
            var col = $"{dialect.QuoteForAliasName(actAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.CreatedUtc))}";
            builder.Parameters["@CreatedTo"] = filter.CreatedTo.Value;
            builder.WhereAnd($"{col} <= @CreatedTo");
        }

        // Apply contact-level filters via JOINs.
        ApplyContactFilters(context, filter);

        return Task.CompletedTask;
    }

    private static void ApplyContactFilters(BulkManageActivityFilterContext context, BulkManageActivityFilter filter)
    {
        var hasPhoneFilter = !string.IsNullOrEmpty(filter.PhoneNumber);
        var hasTimeZoneFilter = filter.TimeZoneIds is { Length: > 0 };
        var hasDncFilter = filter.DoNotCallFrom.HasValue || filter.DoNotCallTo.HasValue;

        if (!hasPhoneFilter && !hasTimeZoneFilter && !hasDncFilter)
        {
            return;
        }

        var builder = context.SqlBuilder;
        var dialect = context.Dialect;
        var actAlias = context.ActivityTableAlias;

        if (hasPhoneFilter)
        {
            var actContactCol = nameof(OmnichannelActivityIndex.ContactContentItemId);
            var phoneTable = context.TableNameConvention.GetIndexTable(typeof(OmnichannelContactIndex));
            var contactItemIdCol = nameof(OmnichannelContactIndex.ContentItemId);

            builder.Join(
                JoinType.Inner,
                phoneTable,
                ContactPhoneAlias,
                contactItemIdCol,
                actAlias,
                actContactCol,
                context.Schema,
                ContactPhoneAlias,
                actAlias);

            var latestCol = $"{dialect.QuoteForAliasName(ContactPhoneAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelContactIndex.Latest))}";
            builder.Parameters["@PhoneLatest"] = true;
            builder.WhereAnd($"{latestCol} = @PhoneLatest");

            if (!PhoneNumberSearchTerm.TryParse(filter.PhoneNumber, out var searchTerm))
            {
                builder.WhereAnd("1 = 0");
            }
            else
            {
                var cellColumnName = searchTerm.IsE164
                    ? nameof(OmnichannelContactIndex.NormalizedPrimaryCellPhoneNumber)
                    : nameof(OmnichannelContactIndex.PrimaryCellPhoneNumber);
                var homeColumnName = searchTerm.IsE164
                    ? nameof(OmnichannelContactIndex.NormalizedPrimaryHomePhoneNumber)
                    : nameof(OmnichannelContactIndex.PrimaryHomePhoneNumber);
                var cellCol = $"{dialect.QuoteForAliasName(ContactPhoneAlias)}.{dialect.QuoteForColumnName(cellColumnName)}";
                var homeCol = $"{dialect.QuoteForAliasName(ContactPhoneAlias)}.{dialect.QuoteForColumnName(homeColumnName)}";
                var comparison = filter.PhoneNumberMatchType == PhoneNumberMatchType.Exact ? "=" : "LIKE";

                builder.Parameters["@PhonePattern"] = searchTerm.GetPattern(filter.PhoneNumberMatchType);
                builder.WhereAnd($"({cellCol} {comparison} @PhonePattern OR {homeCol} {comparison} @PhonePattern)");
            }
        }

        if (hasTimeZoneFilter)
        {
            var actContactCol = nameof(OmnichannelActivityIndex.ContactContentItemId);
            var contactTable = context.TableNameConvention.GetIndexTable(typeof(OmnichannelContactIndex));
            var contactItemIdCol = nameof(OmnichannelContactIndex.ContentItemId);

            builder.Join(
                JoinType.Inner,
                contactTable,
                ContactAlias,
                contactItemIdCol,
                actAlias,
                actContactCol,
                context.Schema,
                ContactAlias,
                actAlias);

            var latestCol = $"{dialect.QuoteForAliasName(ContactAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelContactIndex.Latest))}";
            var tzCol = $"{dialect.QuoteForAliasName(ContactAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelContactIndex.TimeZoneId))}";
            var placeholders = new string[filter.TimeZoneIds.Length];

            builder.Parameters["@TimeZoneLatest"] = true;
            builder.WhereAnd($"{latestCol} = @TimeZoneLatest");

            for (var i = 0; i < filter.TimeZoneIds.Length; i++)
            {
                var paramName = $"@TZ{i}";
                placeholders[i] = paramName;
                builder.Parameters[paramName] = filter.TimeZoneIds[i];
            }

            builder.WhereAnd($"{tzCol} IN ({string.Join(", ", placeholders)})");
        }

        // JOIN the DNC preference index for do-not-call date range filtering.
        if (hasDncFilter)
        {
            var actContactCol = nameof(OmnichannelActivityIndex.ContactContentItemId);
            var dncTable = context.TableNameConvention.GetIndexTable(typeof(OmnichannelContactCommunicationPreferenceIndex));
            var dncItemIdCol = nameof(OmnichannelContactCommunicationPreferenceIndex.ContentItemId);

            builder.Join(
                JoinType.Inner,
                dncTable,
                DncAlias,
                dncItemIdCol,
                actAlias,
                actContactCol,
                context.Schema,
                DncAlias,
                actAlias);

            var doNotCallCol = $"{dialect.QuoteForAliasName(DncAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelContactCommunicationPreferenceIndex.DoNotCall))}";
            builder.WhereAnd($"{doNotCallCol} = 1");

            if (filter.DoNotCallFrom.HasValue)
            {
                var dncUtcCol = $"{dialect.QuoteForAliasName(DncAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelContactCommunicationPreferenceIndex.DoNotCallUtc))}";
                builder.Parameters["@DncFrom"] = filter.DoNotCallFrom.Value;
                builder.WhereAnd($"{dncUtcCol} >= @DncFrom");
            }

            if (filter.DoNotCallTo.HasValue)
            {
                var dncUtcCol = $"{dialect.QuoteForAliasName(DncAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelContactCommunicationPreferenceIndex.DoNotCallUtc))}";
                builder.Parameters["@DncTo"] = filter.DoNotCallTo.Value;
                builder.WhereAnd($"{dncUtcCol} <= @DncTo");
            }
        }
    }

    private static void ApplyAttemptFilter(ISqlBuilder builder, string col, string attemptFilter)
    {
        if (attemptFilter.EndsWith('+'))
        {
            if (int.TryParse(attemptFilter.TrimEnd('+'), out var minAttempts))
            {
                if (minAttempts <= 1)
                {
                    minAttempts = 2;
                }

                builder.Parameters["@MinAttempts"] = minAttempts;
                builder.WhereAnd($"{col} >= @MinAttempts");
            }
        }
        else if (attemptFilter.EndsWith('-'))
        {
            if (int.TryParse(attemptFilter.TrimEnd('-'), out var maxAttempts))
            {
                builder.Parameters["@MaxAttempts"] = maxAttempts;
                builder.WhereAnd($"{col} <= @MaxAttempts");
            }
        }
        else if (int.TryParse(attemptFilter, out var exactAttempts))
        {
            if (exactAttempts <= 1)
            {
                builder.WhereAnd($"{col} <= 1");
            }
            else
            {
                builder.Parameters["@ExactAttempts"] = exactAttempts;
                builder.WhereAnd($"{col} = @ExactAttempts");
            }
        }
    }
}
