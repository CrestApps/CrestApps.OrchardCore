using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.AuditTrail.Models;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.AuditTrail.ViewModels;
using YesSql;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Tests.Agent.AuditTrail;

/// <summary>
/// Stands in for the Audit Trail services in a tool test: events kept in memory behind the audit trail manager, a
/// filter parser built with the filter syntax of the admin page and the filter names of the Audit Trail module, and a
/// query service that runs the parsed filter and records the value each filter got. The stored data of the events
/// holds values that start with <see cref="Canary"/> in every field a tool must not return.
/// </summary>
internal sealed class AuditTrailToolDoubles
{
    /// <summary>
    /// The text every value a tool must not return starts with.
    /// </summary>
    public const string Canary = "CANARY";

    public AuditTrailToolDoubles()
    {
        Events.Add(Create("event-login", "User", "LoggedIn", "user-jane", "jane", "user-jane", new DateTime(2026, 11, 16, 9, 30, 0, DateTimeKind.Utc),
            """
            {
              "AuditTrailUserEvent": {
                "Name": "User",
                "UserName": "jane",
                "UserId": "user-jane",
                "Snapshot": null
              }
            }
            """));

        // An administrator changed the user Jane. The snapshot holds what a site could be set to record.
        Events.Add(Create("event-user-updated", "User", "Updated", "user-owner", "owner", "user-jane", new DateTime(2026, 11, 15, 14, 0, 0, DateTimeKind.Utc),
            $$"""
            {
              "AuditTrailUserEvent": {
                "Name": "User",
                "UserName": "jane",
                "UserId": "user-jane",
                "Snapshot": {
                  "UserId": "user-jane",
                  "UserName": "jane",
                  "Email": "j***@contoso.com",
                  "IsEnabled": "True",
                  "RoleNames": "[\"Editor\"]",
                  "SecurityStamp": "{{Canary}}-SECURITY-STAMP",
                  "Passkeys": "{{Canary}}-PASSKEY",
                  "LoginInfos": "{{Canary}}-PROVIDER-KEY",
                  "UserClaims": "{{Canary}}-CLAIM"
                }
              }
            }
            """));

        // An event about the caller's own account.
        Events.Add(Create("event-own", "User", "PasswordReset", AgentToolTestContext.UserId, AgentToolTestContext.UserName, AgentToolTestContext.UserId, new DateTime(2026, 11, 14, 8, 0, 0, DateTimeKind.Utc),
            $$"""
            {
              "AuditTrailUserEvent": {
                "Name": "User",
                "UserName": "{{AgentToolTestContext.UserName}}",
                "UserId": "{{AgentToolTestContext.UserId}}",
                "Snapshot": null
              }
            }
            """));

        Events.Add(Create("event-content", "Content", "Published", "user-jane", "jane", "item-1", new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc),
            $$"""
            {
              "AuditTrailContentEvent": {
                "Name": "Content",
                "VersionNumber": 3,
                "Comment": "Fixed a typo.",
                "ContentItem": {
                  "ContentItemId": "item-1",
                  "ContentItemVersionId": "version-3",
                  "ContentType": "Article",
                  "DisplayText": "About us",
                  "Published": true,
                  "Latest": true,
                  "Author": "jane",
                  "ModifiedUtc": "2026-11-10T10:00:00Z",
                  "IntegrationPart": {
                    "ApiKey": {
                      "Text": "{{Canary}}-CONTENT-FIELD"
                    }
                  }
                }
              }
            }
            """));

        // An event of a module the tools do not know.
        Events.Add(Create("event-custom", "Billing", "InvoiceSent", "user-jane", "jane", "invoice-7", new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc),
            $$"""
            {
              "BillingEvent": {
                "AccessToken": "{{Canary}}-CUSTOM-EVENT-DATA"
              }
            }
            """));
    }

    /// <summary>
    /// Gets the events of the fixture, newest first.
    /// </summary>
    public List<AuditTrailEvent> Events { get; } = [];

    /// <summary>
    /// Gets the value each filter got when the list was last queried, by filter name. The text without a filter
    /// name is under "username".
    /// </summary>
    public Dictionary<string, string> FilterValues { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the filter text of the last query.
    /// </summary>
    public string FilterText { get; private set; }

    /// <summary>
    /// Gets the page and page size of the last query.
    /// </summary>
    public (int Page, int PageSize) Paging { get; private set; }

    /// <summary>
    /// Adds the doubles to the services of a tool call.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    public void AddTo(IServiceCollection services)
    {
        var manager = new Mock<IAuditTrailManager>();

        manager
            .Setup(m => m.GetEventAsync(It.IsAny<string>()))
            .ReturnsAsync((string eventId) => Events.FirstOrDefault(auditTrailEvent => auditTrailEvent.EventId == eventId));
        manager
            .Setup(m => m.DescribeEvent(It.IsAny<AuditTrailEvent>()))
            .Returns((AuditTrailEvent auditTrailEvent) => Describe(auditTrailEvent.Name, auditTrailEvent.Category));
        manager
            .Setup(m => m.DescribeCategories())
            .Returns(() =>
            [
                new AuditTrailCategoryDescriptor("User", _ => new LocalizedString("User", "User"), new Dictionary<string, AuditTrailEventDescriptor>
                {
                    ["LoggedIn"] = Describe("LoggedIn", "User"),
                    ["LogInFailed"] = Describe("LogInFailed", "User"),
                    ["Updated"] = Describe("Updated", "User"),
                }),
                new AuditTrailCategoryDescriptor("Content", _ => new LocalizedString("Content", "Content"), new Dictionary<string, AuditTrailEventDescriptor>
                {
                    ["Published"] = Describe("Published", "Content"),
                    ["Saved"] = Describe("Saved", "Content"),
                }),
            ]);

        services.AddSingleton(manager.Object);
        services.AddSingleton<IAuditTrailAdminListFilterParser>(new FilterParser(BuildParser()));
        services.AddSingleton<IAuditTrailAdminListQueryService>(provider => new QueryService(this, provider));
    }

    private static AuditTrailEventDescriptor Describe(string name, string category)
    {
        return new AuditTrailEventDescriptor(
            name,
            category,
            _ => new LocalizedString(name, $"{name} (title)"),
            _ => new LocalizedString(category, category),
            _ => new LocalizedString(name, name));
    }

    private static AuditTrailEvent Create(string eventId, string category, string name, string userId, string userName, string correlationId, DateTime createdUtc, string properties)
    {
        return new AuditTrailEvent
        {
            EventId = eventId,
            Category = category,
            Name = name,
            UserId = userId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            CorrelationId = correlationId,
            ClientIpAddress = "203.0.113.7",
            CreatedUtc = createdUtc,
            Properties = JsonNode.Parse(properties).AsObject(),
        };
    }

    // The filter names of the Audit Trail module, each with a condition that records the value it got. The syntax
    // (names, quotes, what ends a value) is the one of the admin page: the same query engine parses the text.
    private IQueryParser<AuditTrailEvent> BuildParser()
    {
        var builder = new QueryEngineBuilder<AuditTrailEvent>();

        foreach (var name in new[] { "id", "category", "event", "date", "sort", "userid" })
        {
            builder.WithNamedTerm(name, term => term.OneCondition((value, query) =>
            {
                FilterValues[name] = value;

                return query;
            }));
        }

        builder.WithDefaultTerm("username", term => term.ManyCondition(
            (value, query) =>
            {
                FilterValues["username"] = value;

                return query;
            },
            (value, query) => query));

        return builder.Build();
    }

    private sealed class FilterParser : IAuditTrailAdminListFilterParser
    {
        private readonly IQueryParser<AuditTrailEvent> _parser;

        public FilterParser(IQueryParser<AuditTrailEvent> parser)
        {
            _parser = parser;
        }

        public QueryFilterResult<AuditTrailEvent> Parse(string text)
            => _parser.Parse(text);
    }

    private sealed class QueryService : IAuditTrailAdminListQueryService
    {
        private readonly AuditTrailToolDoubles _doubles;
        private readonly IServiceProvider _services;

        public QueryService(AuditTrailToolDoubles doubles, IServiceProvider services)
        {
            _doubles = doubles;
            _services = services;
        }

        public async Task<AuditTrailEventQueryResult> QueryAsync(int page, int pageSize, AuditTrailIndexOptions options)
        {
            _doubles.FilterValues.Clear();
            _doubles.FilterText = options.FilterResult.ToString();
            _doubles.Paging = (page, pageSize);

            await options.FilterResult.ExecuteAsync(new AuditTrailQueryContext(_services, new Mock<IQuery<AuditTrailEvent>>().Object));

            return new AuditTrailEventQueryResult
            {
                Events = _doubles.Events.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                TotalCount = _doubles.Events.Count,
            };
        }
    }
}
