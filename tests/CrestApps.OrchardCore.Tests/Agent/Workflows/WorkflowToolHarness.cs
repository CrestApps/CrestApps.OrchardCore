using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Tests.Agent.Indexing;
using Json.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Localization;
using OrchardCore.Locking.Distributed;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Agent.Workflows;

/// <summary>
/// Builds what the workflow tool tests need: in-memory workflow type, version and instance stores, a journal, an
/// activity library with a start event, a notify task and the Started by Workflow, HTTP Request, Signal and User Task
/// events, the activity schemas, the variable types, a distributed lock and a workflow manager double that records what
/// it was asked to run.
/// </summary>
internal sealed class WorkflowToolHarness
{
    public const string OrderWorkflowTypeId = "order-follow-up";
    public const string ManualWorkflowTypeId = "recalculate-totals";
    public const string HttpWorkflowTypeId = "inbound-lead";

    private int _nextId = 100;

    public WorkflowToolHarness()
    {
        AddWorkflowType(new WorkflowType
        {
            WorkflowTypeId = OrderWorkflowTypeId,
            Name = "Order follow-up",
            IsEnabled = true,
            Activities =
            [
                new ActivityRecord { ActivityId = "published", Name = "ContentPublishedEvent", IsStart = true },
                new ActivityRecord { ActivityId = "notify", Name = "NotifyTask", Properties = new JsonObject { ["Message"] = "Published" } },
            ],
            Transitions =
            [
                new Transition { SourceActivityId = "published", SourceOutcomeName = "Done", DestinationActivityId = "notify" },
            ],
        });

        AddWorkflowType(new WorkflowType
        {
            WorkflowTypeId = ManualWorkflowTypeId,
            Name = "Recalculate totals",
            IsEnabled = true,
            Activities =
            [
                new ActivityRecord { ActivityId = "start", Name = "StartedByWorkflowEvent", IsStart = true },
            ],
            Variables =
            [
                new WorkflowVariableDefinition { Name = "orderId", TypeName = "number", IsInput = true },
                new WorkflowVariableDefinition { Name = "total", TypeName = "number", IsOutput = true },
            ],
        });

        AddWorkflowType(new WorkflowType
        {
            WorkflowTypeId = HttpWorkflowTypeId,
            Name = "Inbound lead",
            IsEnabled = true,
            Activities =
            [
                new ActivityRecord
                {
                    ActivityId = "request",
                    Name = "HttpRequestEvent",
                    IsStart = true,
                    Properties = new JsonObject { ["Url"] = "/workflows/invoke?token=abc", ["HttpMethod"] = "POST" },
                },
            ],
        });

        Instances.Add(new Workflow
        {
            Id = 4812,
            WorkflowId = "wf-faulted",
            WorkflowTypeId = OrderWorkflowTypeId,
            WorkflowTypeVersionId = Types[OrderWorkflowTypeId].VersionId,
            Status = WorkflowStatus.Faulted,
            FaultMessage = "The SMTP server refused the message.",
            CreatedUtc = new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc),
            State = new JsonObject { ["Properties"] = new JsonObject { ["orderId"] = 1042 } },
        });

        Instances.Add(new Workflow
        {
            Id = 4813,
            WorkflowId = "wf-waiting",
            WorkflowTypeId = OrderWorkflowTypeId,
            CorrelationId = "article-1",
            Status = WorkflowStatus.Halted,
            ParentWorkflowId = "wf-parent",
            CreatedUtc = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc),
            BlockingActivities =
            [
                new BlockingActivity { ActivityId = "signal", Name = "SignalEvent" },
                new BlockingActivity { ActivityId = "review", Name = "UserTaskEvent" },
            ],
            // The User Task event keeps its actions and roles in the state of the instance.
            State = JsonNode.Parse("""
                { "ActivityStates": { "review": { "Actions": [ "Approve", "Reject" ], "Roles": [ "Editor" ] } } }
                """).AsObject(),
        });

        Instances.Add(new Workflow
        {
            Id = 4814,
            WorkflowId = "wf-finished",
            WorkflowTypeId = ManualWorkflowTypeId,
            Status = WorkflowStatus.Finished,
            CreatedUtc = new DateTime(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc),
        });

        Journal.Add(new WorkflowExecutionRecord
        {
            WorkflowId = "wf-faulted",
            Sequence = 1,
            ActivityId = "published",
            ActivityName = "ContentPublishedEvent",
            Status = WorkflowExecutionRecordStatus.Completed,
            Outcomes = ["Done"],
        });

        Journal.Add(new WorkflowExecutionRecord
        {
            WorkflowId = "wf-faulted",
            Sequence = 2,
            ActivityId = "notify",
            ActivityName = "NotifyTask",
            Status = WorkflowExecutionRecordStatus.Faulted,
            Error = "The SMTP server refused the message.",
            Data = new WorkflowExecutionData { LastResult = "\"refused\"" },
        });
    }

    public bool SignedIn { get; init; } = true;

    public Func<Permission, object, bool> Authorize { get; init; }

    public IList<string> Roles { get; init; } = [];

    public Dictionary<string, WorkflowType> Types { get; } = new(StringComparer.Ordinal);

    public List<WorkflowTypeVersion> Versions { get; } = [];

    public List<Workflow> Instances { get; } = [];

    public List<WorkflowExecutionRecord> Journal { get; } = [];

    public List<string> DeletedTypes { get; } = [];

    public List<long> DeletedInstances { get; } = [];

    public List<long> SavedInstances { get; } = [];

    public List<(string Name, IDictionary<string, object> Input, string CorrelationId)> TriggeredEvents { get; } = [];

    public List<(long InstanceId, string ActivityId, IDictionary<string, object> Input)> Resumed { get; } = [];

    public List<(long InstanceId, string ActivityId)> Retried { get; } = [];

    public List<long> Restarted { get; } = [];

    public List<(string WorkflowTypeId, string StartActivityId, IDictionary<string, object> Input)> Started { get; } = [];

    public List<long> ResumedParents { get; } = [];

    public FakeDistributedLock Lock { get; } = new();

    public ContentItem ContentItem { get; } = new()
    {
        ContentItemId = "article-1",
        ContentItemVersionId = "article-1-v2",
        ContentType = "Article",
    };

    public ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices(SignedIn, Authorize);

        if (SignedIn && Roles.Count > 0)
        {
            var accessor = (IHttpContextAccessor)services.First(descriptor => descriptor.ServiceType == typeof(IHttpContextAccessor)).ImplementationInstance;
            ((ClaimsIdentity)accessor.HttpContext.User.Identity).AddClaims(Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        }

        services.AddSingleton<IWorkflowTypeStore>(new TypeStore(this));
        services.AddSingleton<IWorkflowTypeVersionStore>(new VersionStore(this));
        services.AddSingleton<IWorkflowStore>(new InstanceStore(this));
        services.AddSingleton(CreateJournal());
        services.AddSingleton(CreateWorkflowManager());
        services.AddSingleton<IDistributedLock>(Lock);
        services.AddSingleton(CreateActivityLibrary());
        services.AddSingleton(CreateSchemaService());
        services.AddSingleton<IWorkflowVariableTypeProvider>(new VariableTypes());
        services.AddSingleton(CreateContentManager());

        var typeIds = new Mock<IWorkflowTypeIdGenerator>();
        typeIds.Setup(generator => generator.GenerateUniqueId(It.IsAny<WorkflowType>())).Returns(() => "generated-type-" + _nextId++);
        services.AddSingleton(typeIds.Object);

        var activityIds = new Mock<IActivityIdGenerator>();
        activityIds.Setup(generator => generator.GenerateUniqueId(It.IsAny<ActivityRecord>())).Returns(() => "generated-activity-" + _nextId++);
        services.AddSingleton(activityIds.Object);

        return services.BuildServiceProvider();
    }

    private void AddWorkflowType(WorkflowType workflowType)
    {
        workflowType.Id = _nextId++;
        Types[workflowType.WorkflowTypeId] = workflowType;
        CreateVersion(workflowType);
    }

    private void CreateVersion(WorkflowType workflowType)
    {
        var signature = Signature(workflowType.Activities, workflowType.Transitions, workflowType.Variables);
        var latest = Versions.Where(version => version.WorkflowTypeId == workflowType.WorkflowTypeId).MaxBy(version => version.Version);

        if (latest is not null && Signature(latest.Activities, latest.Transitions, latest.Variables) == signature)
        {
            workflowType.VersionId = latest.VersionId;

            return;
        }

        var version = new WorkflowTypeVersion
        {
            WorkflowTypeId = workflowType.WorkflowTypeId,
            VersionId = workflowType.WorkflowTypeId + "-v" + ((latest?.Version ?? 0) + 1),
            Version = (latest?.Version ?? 0) + 1,
            Name = workflowType.Name,
            CreatedUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc),
            CreatedByUserName = "admin",
            Activities = workflowType.Activities.Select(activity => activity.Clone()).ToList(),
            Transitions = workflowType.Transitions.Select(transition => transition.Clone()).ToList(),
            Variables = workflowType.Variables.Select(variable => variable.Clone()).ToList(),
        };

        Versions.Add(version);
        workflowType.VersionId = version.VersionId;
    }

    private static string Signature(IEnumerable<ActivityRecord> activities, IEnumerable<Transition> transitions, IEnumerable<WorkflowVariableDefinition> variables)
        => JsonSerializer.Serialize(new { activities, transitions, variables });

    private IWorkflowExecutionJournal CreateJournal()
    {
        var journal = new Mock<IWorkflowExecutionJournal>();
        journal.Setup(item => item.IsEnabled).Returns(true);
        journal.Setup(item => item.ListAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string workflowId, int count) => Journal.Where(record => record.WorkflowId == workflowId).OrderBy(record => record.Sequence).ToList());
        journal.Setup(item => item.GetAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string workflowId, int sequence) => Journal.FirstOrDefault(record => record.WorkflowId == workflowId && record.Sequence == sequence));

        return journal.Object;
    }

    private IWorkflowManager CreateWorkflowManager()
    {
        var manager = new Mock<IWorkflowManager>();

        manager.Setup(item => item.StartWorkflowAsync(It.IsAny<WorkflowType>(), It.IsAny<ActivityRecord>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>()))
            .ReturnsAsync((WorkflowType type, ActivityRecord start, IDictionary<string, object> input, string correlationId) =>
            {
                Started.Add((type.WorkflowTypeId, start?.ActivityId, input));

                var workflow = new Workflow { Id = 5000, WorkflowId = "wf-new", WorkflowTypeId = type.WorkflowTypeId, Status = WorkflowStatus.Finished };
                Instances.Add(workflow);

                var properties = new Dictionary<string, object> { ["total"] = 99 };

                return new WorkflowExecutionContext(type, workflow, input, null, properties, null, null, [], new VariableTypes());
            });

        manager.Setup(item => item.RetryActivityAsync(It.IsAny<Workflow>(), It.IsAny<string>()))
            .ReturnsAsync((Workflow workflow, string activityId) =>
            {
                Retried.Add((workflow.Id, activityId));
                workflow.Status = WorkflowStatus.Finished;

                return new WorkflowExecutionContext(Types[workflow.WorkflowTypeId], workflow, null, null, null, null, null, [], null);
            });

        manager.Setup(item => item.RestartWorkflowAsync(It.IsAny<WorkflowType>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>()))
            .ReturnsAsync((WorkflowType type, IDictionary<string, object> input, string correlationId) =>
            {
                Restarted.Add(type.Id);

                return new WorkflowExecutionContext(type, new Workflow { Id = 5001, WorkflowId = "wf-restarted", Status = WorkflowStatus.Finished }, input, null, null, null, null, [], null);
            });

        manager.Setup(item => item.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync((string name, IDictionary<string, object> input, string correlationId, bool isExclusive, bool isAlwaysCorrelated) =>
            {
                TriggeredEvents.Add((name, input, correlationId));

                var waiting = Instances.Single(workflow => workflow.WorkflowId == "wf-waiting");

                return new[] { new WorkflowExecutionContext(Types[waiting.WorkflowTypeId], waiting, input, null, null, null, null, [], null) };
            });

        manager.Setup(item => item.ResumeWorkflowAsync(It.IsAny<Workflow>(), It.IsAny<BlockingActivity>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync((Workflow workflow, BlockingActivity activity, IDictionary<string, object> input) =>
            {
                Resumed.Add((workflow.Id, activity.ActivityId, input));

                return new WorkflowExecutionContext(Types[workflow.WorkflowTypeId], workflow, input, null, null, null, null, [], null);
            });

        manager.Setup(item => item.ResumeParentOfStoppedChildAsync(It.IsAny<Workflow>(), It.IsAny<string>()))
            .Callback((Workflow child, string reason) => ResumedParents.Add(child.Id))
            .Returns(Task.CompletedTask);

        return manager.Object;
    }

    private static IActivityLibrary CreateActivityLibrary()
    {
        var activities = new List<IActivity>
        {
            CreateEvent("ContentPublishedEvent"),
            CreateEvent("StartedByWorkflowEvent"),
            CreateEvent("HttpRequestEvent"),
            CreateEvent("SignalEvent"),
            CreateEvent("UserTaskEvent"),
        };

        var notify = new Mock<ITask>();
        notify.Setup(activity => activity.Name).Returns("NotifyTask");
        notify.Setup(activity => activity.DisplayText).Returns(new LocalizedString("NotifyTask", "Notify"));
        notify.Setup(activity => activity.Category).Returns(new LocalizedString("UI", "UI"));
        notify.As<IActivityOutputs>().Setup(activity => activity.GetOutputs()).Returns(new[]
        {
            new ActivityOutputDescriptor { Name = "Sent", TypeName = "boolean", DisplayName = LocalizationSource.Create("Sent") },
        });
        activities.Add(notify.Object);

        var library = new Mock<IActivityLibrary>();
        library.Setup(item => item.ListActivities()).Returns(activities);
        library.Setup(item => item.GetActivityByName(It.IsAny<string>())).Returns((string name) => activities.FirstOrDefault(activity => activity.Name == name));

        return library.Object;
    }

    private static IActivity CreateEvent(string name)
    {
        var activity = new Mock<IEvent>();
        activity.Setup(item => item.Name).Returns(name);
        activity.Setup(item => item.DisplayText).Returns(new LocalizedString(name, name));
        activity.Setup(item => item.Category).Returns(new LocalizedString("Events", "Events"));

        return activity.Object;
    }

    private static IWorkflowActivitySchemaService CreateSchemaService()
    {
        IReadOnlyList<WorkflowActivityDescriptor> descriptors =
        [
            new WorkflowActivityDescriptor
            {
                Name = "ContentPublishedEvent",
                IsEvent = true,
                IsTask = false,
                DisplayText = "Content Published",
                Outcomes = ["Done"],
                HasSchemaDefinition = true,
                Properties = new JsonSchemaBuilder().Type(SchemaValueType.Object),
            },
            new WorkflowActivityDescriptor
            {
                Name = "NotifyTask",
                IsEvent = false,
                IsTask = true,
                DisplayText = "Notify",
                Description = "Shows a notification.",
                Outcomes = ["Done"],
                HasSchemaDefinition = true,
                Properties = new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(("Message", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .Required("Message")
                    .AdditionalProperties(false),
            },
        ];

        var service = new Mock<IWorkflowActivitySchemaService>();
        service.Setup(item => item.GetActivityDescriptorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(descriptors);

        return service.Object;
    }

    private IContentManager CreateContentManager()
    {
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(ContentItem.ContentItemId, It.IsAny<VersionOptions>())).ReturnsAsync(ContentItem);

        return contentManager.Object;
    }

    private sealed class TypeStore : IWorkflowTypeStore
    {
        private readonly WorkflowToolHarness _harness;

        public TypeStore(WorkflowToolHarness harness)
        {
            _harness = harness;
        }

        public Task<WorkflowType> GetAsync(long id)
            => Task.FromResult(_harness.Types.Values.FirstOrDefault(type => type.Id == id));

        public Task<WorkflowType> GetAsync(string uid)
            => Task.FromResult(uid is not null && _harness.Types.TryGetValue(uid, out var type) ? type : null);

        public Task<IEnumerable<WorkflowType>> GetAsync(IEnumerable<long> ids)
            => Task.FromResult(_harness.Types.Values.Where(type => ids.Contains(type.Id)));

        public Task<IEnumerable<WorkflowType>> ListAsync()
            => Task.FromResult<IEnumerable<WorkflowType>>(_harness.Types.Values.ToList());

        public Task<IEnumerable<WorkflowType>> GetByStartActivityAsync(string activityName)
            => Task.FromResult(_harness.Types.Values.Where(type => type.IsEnabled && type.Activities.Any(activity => activity.IsStart && activity.Name == activityName)));

        public Task SaveAsync(WorkflowType workflowType)
        {
            if (workflowType.Id == 0)
            {
                workflowType.Id = _harness._nextId++;
            }

            _harness.Types[workflowType.WorkflowTypeId] = workflowType;
            _harness.CreateVersion(workflowType);

            return Task.CompletedTask;
        }

        public Task DeleteAsync(WorkflowType workflowType)
        {
            _harness.Types.Remove(workflowType.WorkflowTypeId);
            _harness.Instances.RemoveAll(workflow => workflow.WorkflowTypeId == workflowType.WorkflowTypeId);
            _harness.Versions.RemoveAll(version => version.WorkflowTypeId == workflowType.WorkflowTypeId);
            _harness.DeletedTypes.Add(workflowType.WorkflowTypeId);

            return Task.CompletedTask;
        }
    }

    private sealed class VersionStore : IWorkflowTypeVersionStore
    {
        private readonly WorkflowToolHarness _harness;

        public VersionStore(WorkflowToolHarness harness)
        {
            _harness = harness;
        }

        public Task<WorkflowTypeVersion> GetAsync(string versionId)
            => Task.FromResult(_harness.Versions.FirstOrDefault(version => version.VersionId == versionId));

        public Task<IEnumerable<WorkflowTypeVersion>> ListAsync(string workflowTypeId)
            => Task.FromResult<IEnumerable<WorkflowTypeVersion>>(_harness.Versions.Where(version => version.WorkflowTypeId == workflowTypeId).OrderByDescending(version => version.Version).ToList());

        public Task<WorkflowTypeVersion> GetLatestAsync(string workflowTypeId)
            => Task.FromResult(_harness.Versions.Where(version => version.WorkflowTypeId == workflowTypeId).MaxBy(version => version.Version));

        public Task<WorkflowTypeVersion> CreateIfChangedAsync(WorkflowType workflowType)
        {
            _harness.CreateVersion(workflowType);

            return GetAsync(workflowType.VersionId);
        }

        public Task<WorkflowType> GetWorkflowTypeAsync(WorkflowType workflowType, string versionId)
            => Task.FromResult(workflowType);

        public Task DeleteAsync(string workflowTypeId)
        {
            _harness.Versions.RemoveAll(version => version.WorkflowTypeId == workflowTypeId);

            return Task.CompletedTask;
        }
    }

    private sealed class InstanceStore : IWorkflowStore
    {
        private readonly WorkflowToolHarness _harness;

        public InstanceStore(WorkflowToolHarness harness)
        {
            _harness = harness;
        }

        public Task<int> CountAsync(string workflowTypeId = null)
            => Task.FromResult(Filter(workflowTypeId).Count());

        public Task<bool> HasHaltedInstanceAsync(string workflowTypeId)
            => Task.FromResult(_harness.Instances.Any(workflow => workflow.WorkflowTypeId == workflowTypeId && workflow.BlockingActivities.Count > 0));

        public Task<bool> HasHaltedInstanceAsync(string workflowTypeId, string correlationId)
            => Task.FromResult(_harness.Instances.Any(workflow => workflow.WorkflowTypeId == workflowTypeId && workflow.CorrelationId == correlationId && workflow.BlockingActivities.Count > 0));

        public Task<IEnumerable<Workflow>> ListAsync(string workflowTypeId = null, int? skip = null, int? take = null)
            => Task.FromResult<IEnumerable<Workflow>>(Filter(workflowTypeId).OrderByDescending(workflow => workflow.CreatedUtc).Skip(skip ?? 0).Take(take ?? int.MaxValue).ToList());

        public Task<IEnumerable<Workflow>> ListAsync(IEnumerable<string> workflowTypeIds)
            => Task.FromResult(_harness.Instances.Where(workflow => workflowTypeIds.Contains(workflow.WorkflowTypeId)));

        public Task<IEnumerable<Workflow>> ListAsync(string workflowTypeId, IEnumerable<string> blockingActivityIds)
            => throw new NotSupportedException();

        public Task<IEnumerable<Workflow>> ListByActivityNameAsync(string activityName, string correlationId = null, bool isAlwaysCorrelated = false)
            => Task.FromResult<IEnumerable<Workflow>>(_harness.Instances
                .Where(workflow => workflow.CorrelationId == correlationId && workflow.BlockingActivities.Any(activity => activity.Name == activityName))
                .ToList());

        public Task<IEnumerable<Workflow>> ListAsync(string workflowTypeId, string activityName, string correlationId = null, bool isAlwaysCorrelated = false)
            => throw new NotSupportedException();

        public Task<IEnumerable<Workflow>> ListDueRetriesAsync(DateTime utcNow, int take)
            => throw new NotSupportedException();

        public Task<Workflow> GetAsync(long id)
            => Task.FromResult(_harness.Instances.FirstOrDefault(workflow => workflow.Id == id));

        public Task<Workflow> GetAsync(string uid)
            => Task.FromResult(_harness.Instances.FirstOrDefault(workflow => workflow.WorkflowId == uid));

        public Task<IEnumerable<Workflow>> GetAsync(IEnumerable<long> ids)
            => Task.FromResult(_harness.Instances.Where(workflow => ids.Contains(workflow.Id)));

        public Task<IEnumerable<Workflow>> GetAsync(IEnumerable<string> uids)
            => Task.FromResult(_harness.Instances.Where(workflow => uids.Contains(workflow.WorkflowId)));

        public Task SaveAsync(Workflow workflow)
        {
            _harness.SavedInstances.Add(workflow.Id);

            return Task.CompletedTask;
        }

        public Task DeleteAsync(Workflow workflow)
        {
            _harness.Instances.Remove(workflow);
            _harness.DeletedInstances.Add(workflow.Id);

            return Task.CompletedTask;
        }

        private IEnumerable<Workflow> Filter(string workflowTypeId)
            => _harness.Instances.Where(workflow => workflowTypeId is null || workflow.WorkflowTypeId == workflowTypeId);
    }

    private sealed class VariableTypes : IWorkflowVariableTypeProvider
    {
        private static readonly WorkflowVariableTypeDescriptor[] _descriptors =
        [
            WorkflowVariableTypeDescriptor.Create<NumberType>(),
            WorkflowVariableTypeDescriptor.Create<TextType>(),
        ];

        public IReadOnlyList<WorkflowVariableTypeDescriptor> List()
            => _descriptors;

        public WorkflowVariableTypeDescriptor GetDescriptor(string name)
            => _descriptors.FirstOrDefault(descriptor => string.Equals(descriptor.Name, name, StringComparison.OrdinalIgnoreCase));

        public IWorkflowVariableType Get(string name)
            => GetDescriptor(name)?.Instance;
    }

    private sealed class NumberType : IWorkflowVariableType
    {
        public static string Name => "number";

        public static LocalizationSource DisplayName => LocalizationSource.Create("Number");

        public static string Editor => "number";

        public bool TryCoerce(object value, out object result)
        {
            result = null;

            switch (value)
            {
                case JsonValue node when node.TryGetValue<double>(out var number):
                    result = number;
                    return true;
                case int or long or double:
                    result = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                default:
                    return false;
            }
        }
    }

    private sealed class TextType : IWorkflowVariableType
    {
        public static string Name => "string";

        public static LocalizationSource DisplayName => LocalizationSource.Create("Text");

        public static string Editor => "string";

        public bool TryCoerce(object value, out object result)
        {
            result = value?.ToString();

            return true;
        }
    }
}
