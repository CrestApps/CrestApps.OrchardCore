# Orchard Core: workflows and data pipelines

Part of the [AI tool coverage plan](README.md). Nothing in this file is built yet. The design rules, the permission and approval model and the test plan that apply to every tool below are in [01-foundation.md](01-foundation.md) and [03-testing.md](03-testing.md); the final agent roster is in [02-agents.md](02-agents.md) and wins over the agent notes in this file.

Scope: `OrchardCore.Workflows` (features `OrchardCore.Workflows`, `OrchardCore.Workflows.Http`, `OrchardCore.Workflows.Timers`, `OrchardCore.Workflows.Session`), `OrchardCore.DataPipelines` (features `OrchardCore.DataPipelines.Core`, `OrchardCore.DataPipelines`), `OrchardCore.DataPipelines.Ftp`, `OrchardCore.DataPipelines.Sftp`. Everything below was read from the code in `OrchardCore/src/OrchardCore.Modules/*`, `OrchardCore/src/OrchardCore/OrchardCore.Workflows.Abstractions`, `OrchardCore.DataPipelines.Abstractions`, `OrchardCore.DataPipelines.Core`, `OrchardCore.DataSources.Abstractions`, and from `src/Modules/CrestApps.OrchardCore.AI.Agent` and `src/Core/CrestApps.OrchardCore.Recipes.Core`.

Totals: 39 new tool names (18 for workflows, one of which replaces `createOrUpdateWorkflow`; 21 for data pipelines, data sets and shared links), 3 existing workflow tools to improve, 1 settings group for the generic settings trio, 2 agents (`WorkflowAgent` expanded to 21 tools, new `DataPipelineAgent` with 21 tools).

## Workflows: workflow types and the designer — `OrchardCore.Workflows`

**What an admin can do in the UI:**

- Design → Workflows (`WorkflowTypeController.Index`, route `Admin/Workflows/Types`): list the workflow types, search by name, page, bulk **Delete** (the controller's `BulkEdit` also handles `Export`, but the list only offers Delete), open **Instances** of every workflow, **Create Workflow**.
- Create Workflow (`WorkflowTypeController.Create`): name plus "More settings" from `WorkflowTypePropertiesViewModel`: `IsEnabled` (default true), instances at a time (`InstanceLimit`: Any / One = `IsSingleton` / OnePerCorrelation = `IsSingletonPerCorrelation`), `LockTimeout`, `LockExpiration`, `DeleteFinishedWorkflows`, `IsActivity` (usable as an activity), `BranchingMode` (`FirstOnly` / `All`), `FaultOnScriptErrors`, `RecordActivityData` (default true). It saves an empty, published workflow type and opens the designer.
- Row actions: **Edit** (designer), **Delete**; Actions menu: **Clone** (`WorkflowTypeController.Clone`: new name, copies activities, transitions, variables and execution settings, and copies the source's enabled state) and **Export** (`WorkflowTypeController.Export`: a zip holding a `WorkflowType` recipe).
- Designer (`WorkflowTypeController.Edit` page + JSON endpoints of `WorkflowDesignerController`, route `Admin/Workflows/Types/{workflowTypeId}/Designer/{action}`): `Definition` (draft or live definition, validation issues, running instance count, how the workflow can be run), `Library` (activities by category plus presets), `AddActivity` (by activity name or preset id), `Save` (positions, start flags, transitions, removed and restored activities), `Editor` GET/POST (an activity's settings through its display driver), `OutputBindings` (store an activity output in a variable), `Variables` (replace the declared variables, validated by `WorkflowVariableValidator`), `Settings` GET/POST (name, enabled, execution settings; enabling or disabling applies to the live type at once), `Publish`, `Discard`, `Versions`, `Version`, `Compare`, `Restore`.
- All changes go to a `WorkflowTypeDraft` with optimistic concurrency (each call sends the revision it last saw; a mismatch returns 409). Only `Publish` changes the live `WorkflowType`, which creates the next `WorkflowTypeVersion`; a draft with `Error` issues cannot be published.
- Read-only pages: a version (`WorkflowTypeController.Version`) and a side-by-side comparison (`WorkflowTypeController.CompareVersions`, diff from `WorkflowTypeDiff.Compare`).
- There is no import page in this module: importing is the `WorkflowType` recipe step (`Recipes/WorkflowTypeStep.cs`) run from the recipe/deployment import UI.
- Activities come from every enabled module: 71 concrete activities are registered by Orchard Core modules (Workflows 26, Contents 13, Users 11, Forms 6, Tenants 4, Roles 2, Notifications 2, DataPipelines 2, Email, Sms, ReCaptcha, Twitter, Facebook 1 each) and about 150 more by some 20 CrestApps modules (AI, Bookings, Calendars, ContactCenter, Dispatch, Inventory, Omnichannel, Orders, Reports, Scheduling, Shipping, Skills, Staff, Stripe, Subscriptions, Wizard, WorkOrders, Workforce...).

**Already covered:** existing tools `listWorkflowTypes`, `getWorkflowType`, `listWorkflowActivities`, `createOrUpdateWorkflow`; recipe step `WorkflowType` (usable through `importOrchardCoreRecipe`; creates a type or saves the import as the next published version of an existing one and discards its draft); deployment step `AllWorkflowType` (exports every workflow type). CrestApps.OrchardCore.AI also has "Workflow tool instances" (`Workflows/Tools/WorkflowToolFunction.cs`): a per-workflow AI tool that starts a workflow whose start activity is `AIToolCalledEvent`; that is a way to expose one workflow as a tool, not a management tool.

**What the existing tools and `Templates/Profiles/workflow-agent.md` miss compared with the UI:**

- Instances: nothing. No list, status counts, filters, details, journal, variable values or state, and no restart, retry, cancel or delete.
- Enable / disable: no tool. The only way is to re-import the whole definition with `IsEnabled` flipped through `createOrUpdateWorkflow`, which also discards any draft and publishes.
- Delete, clone, export: no tool for a workflow type.
- Run: no tool. The agent prompt says so itself: "Do not claim a workflow ran or worked. You can only build and describe workflows, not test them."
- Per-activity schemas: `listWorkflowActivities` returns only `Name`, `DisplayText`, `Category`, `IsEvent`, `IsTask` for every activity, with no filter. It returns no property schema, outcomes, outputs or provided values. The schemas exist: `IWorkflowActivitySchemaService` (`CrestApps.OrchardCore.Recipes.Core/Services/WorkflowActivitySchemaService.cs`) merges the activity library with `IWorkflowActivitySchemaDefinition` contributions (every activity registered with `AddActivity<>` in both repositories has a matching schema definition class; there are 223 `AddWorkflowActivitySchema<>` registrations), but they are only reachable through `getOrchardCoreRecipeJsonSchema(step: "WorkflowType")` or `listOrchardCoreRecipeStepsAndSchemas`, and neither tool is in the agent's `ToolNames` (`listWorkflowTypes, getWorkflowType, listWorkflowActivities, createOrUpdateWorkflow`). The base tool's argument description tells the model to call `getOrchardCoreRecipeJsonSchema` "if available"; for this agent it is not.
- Validation: the only check is JSON-schema validation of the recipe (`rootSchema.Evaluate` in `ImportRecipeBaseTool.ProcessRecipeAsync`), and activity `Properties` accept additional properties. The designer validation is never run: `IWorkflowTypeDraftManager.ValidateAsync` (missing start activity, unknown activity type, transition to a missing activity, duplicate outcome transition, unreachable activity, undeclared variable, output type mismatch, missing or recursive Execute Workflow, and invalid activity settings checked by replaying each activity's editor in `WorkflowActivityEditorValidator`) and `WorkflowVariableValidator`. The recipe step saves whatever it gets, for example a transition whose `SourceOutcomeName` the activity never produces.
- Drafts and versions: the tools read and write only the published definition. A draft a person is editing is invisible to `getWorkflowType`, and `createOrUpdateWorkflow` on an existing type deletes that draft (`_workflowTypeDraftManager.DiscardAsync` in `WorkflowTypeStep`) and goes live as the next version. No draft stage, no publish step, no version list, compare or restore.
- Result reporting: inside a request `ProcessRecipeAsync` always defers the import (`DeferredWork.Add("ai_recipe_import", ...)`) and returns "Recipe was successfully imported" before the recipe ran. A failed import is only logged, so the model reports a success it cannot verify, and it never gets back generated values such as the URL of an HTTP Request event.
- Identifiers: the recipe step never generates `ActivityId`s (the schema says "must be supplied") and needs a `WorkflowTypeId` chosen by the model.
- Permissions and approval: none of the four tools checks `ManageWorkflows`. `OrchardCorePermissions.ManageWorkflows` is declared in `CrestApps.OrchardCore.AI.Agent/OrchardCorePermissions.cs` and never used; the only gate is the per-tool `AccessAITool` permission (`OrchardCoreAIToolAccessEvaluator`). `createOrUpdateWorkflow` is registered without `.RequireApproval()` and hands the model's `recipe` string to the same `ProcessRecipeAsync` as `importOrchardCoreRecipe` (which does require approval), with no check that the recipe holds only `WorkflowType` steps. It is therefore an unapproved way to run any recipe step.
- Registration: `createOrUpdateWorkflow` and `listWorkflowActivities` are registered in `WorkflowsRecipesStartup`, which requires `OrchardCore.Recipes.Core`, while the agent template only requires `OrchardCore.Workflows`. Without Recipes.Core the agent is left with two read tools. `listWorkflowActivities` does not need recipes at all.
- `listWorkflowTypes` details: `term` is `required` in the JSON schema although the description says "List all workflow types" and the code treats it as optional; `workflowsCount` and `totalPages` are computed before the term filter is applied; every row is the full `WorkflowType` document (all activities and their properties); there is no summary of enabled state, draft, version or instances.
- `getWorkflowType` details: looks up only by `WorkflowTypeId` (not by name or document id) and returns the raw published document, without issues, draft state, version or run mode.
- Not covered at all: variables and output bindings as first-class edits, HTTP Request URL generation, signals, user task actions, the trimming settings.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listWorkflowTypes` (existing, improve) | Read | no | `ManageWorkflows` | Lists the site's workflows with id, name, enabled state, published version number, whether a draft with unpublished changes exists, start events and number of instances; optional name filter and paging. Use it to find a workflow's id before reading or changing it. | WorkflowAgent |
| `getWorkflowType` (existing, improve) | Read | no | `ManageWorkflows` | Returns one workflow's definition (settings, variables, activities with their properties, transitions) from its draft, its published version or a given version, with the designer's validation issues, its revision and how it can be run. Read it before changing a workflow; pass `asRecipe` to get the exportable `WorkflowType` recipe. | WorkflowAgent |
| `listWorkflowActivities` (existing, improve) | Read | no | `ManageWorkflows` | Lists the events and tasks available on this site (name, title, category, event or task, known outcomes), filtered by kind, category or text, plus the workflows usable as activities, the variable types and the expression syntaxes. Use only activity names it returns. | WorkflowAgent |
| `getWorkflowActivity` | Read | no | `ManageWorkflows` | Returns, for up to 10 activity names, the JSON schema of the activity's `Properties`, its outcomes (and whether they depend on its settings), its outputs and the values it provides to later activities. Call it for every activity type before adding or editing one. | WorkflowAgent |
| `saveWorkflowType` (replaces `createOrUpdateWorkflow`) | Write | no | `ManageWorkflows` | Creates a workflow (disabled unless told otherwise) or replaces the draft of an existing one with a full definition: settings, variables, activities and transitions. Nothing runs until `publishWorkflowType`; the result lists the validation issues to fix, and `validateOnly` checks a definition without saving it. | WorkflowAgent |
| `updateWorkflowDraft` | Write | no | `ManageWorkflows` | Applies small changes to a workflow's draft without resending it: add, update or remove an activity, connect or disconnect outcomes, set the start activity, bind an output to a variable, change variables or settings, or restore a version into the draft. Needs the revision from the last read and returns the new revision and issues. | WorkflowAgent |
| `publishWorkflowType` | Security | yes | `ManageWorkflows` | Publishes a workflow's draft as its next version, which new instances run from then on; refused while the draft has errors. Use it only after the user confirmed the change. | WorkflowAgent |
| `discardWorkflowDraft` | Destructive | yes | `ManageWorkflows` | Deletes a workflow's unpublished draft so it shows its published definition again. Cannot be undone. | WorkflowAgent |
| `listWorkflowTypeVersions` | Read | no | `ManageWorkflows` | Lists a workflow's versions (number, who published it, when, instances running on it) and its draft; with `from` and `to` (version ids or `draft`) it also returns what changed between them. | WorkflowAgent |
| `cloneWorkflowType` | Write | no | `ManageWorkflows` | Creates a copy of a workflow under a new name, with its activities, transitions, variables and settings. The copy is created disabled so it does not react to the same events as the original. | WorkflowAgent |
| `enableWorkflowType` | Write | no | `ManageWorkflows` | Enables a workflow so its start events start instances again. Takes effect at once, without publishing. | WorkflowAgent |
| `disableWorkflowType` | Write | no | `ManageWorkflows` | Disables a workflow: its events no longer start instances and its HTTP URL answers not found. Instances that already wait are kept, not cancelled. | WorkflowAgent |
| `deleteWorkflowType` | Destructive | yes | `ManageWorkflows` | Permanently deletes one or more workflows with their versions, draft, instances and journals. Prefer `disableWorkflowType` when the user only wants a workflow to stop. | WorkflowAgent |

**Implementation notes:**

- Services. Reads: `IWorkflowTypeStore` (`GetAsync(long)`, `GetAsync(string)`, `ListAsync`), `IWorkflowTypeVersionStore` (`ListAsync`, `GetAsync`, `GetWorkflowTypeAsync`), `IActivityLibrary`, `IWorkflowVariableTypeProvider`, all in `OrchardCore.Workflows.Abstractions` (already referenced by AI.Agent). Per-activity schema: `IWorkflowActivitySchemaService.GetActivityDescriptorsAsync` (properties schema, outcomes, `HasDynamicOutcomes`), plus `IActivityOutputs.GetOutputs()` and `IActivityProvidedValues.GetProvidedValues()` read from the activity instance. When a descriptor has `HasSchemaDefinition == false`, fall back to reflecting the activity's public read/write properties (each maps to a `Properties[name]` key through `Activity.GetProperty/SetProperty`).
- Drafts. Every write tool must go through `IWorkflowTypeDraftManager` (`SaveGraphAsync`, `AddActivityAsync`, `UpdateActivityAsync`, `UpdateSettingsAsync`, `UpdateVariablesAsync`, `UpdateOutputBindingsAsync`, `RestoreAsync`, `PublishAsync`, `DiscardAsync`, `ValidateAsync`) so the result carries `WorkflowTypeDraftResult.Issues`, revisions are honoured, and people with the designer open are notified (`IWorkflowDesignerNotifier`, SignalR when `OrchardCore.SignalR` is on). Gap: `IWorkflowTypeDraftManager`, `WorkflowTypeDraft`, `WorkflowDesignIssue`, `WorkflowVariableValidator`, `WorkflowTypeDiff` and `WorkflowDesignerModelBuilder` are public but live in the `OrchardCore.Workflows` module assembly, and no CrestApps project references that package today (only `OrchardCore.Workflows.Abstractions`). Either AI.Agent takes a reference to the module package, or these types move to the abstractions project in the Orchard Core fork.
- `saveWorkflowType` for a new workflow does what `WorkflowTypeController.Create` does (new `WorkflowType`, `IWorkflowTypeIdGenerator.GenerateUniqueId`, `IWorkflowTypeStore.SaveAsync`), then writes the definition into the draft. It should generate `ActivityId`s (`IActivityIdGenerator`) and let the model use its own aliases in the transitions of the same call. Default `IsEnabled` to false (the UI defaults to true; the current agent prompt asks for false).
- Enable / disable. `WorkflowTypeDraftManager.UpdateSettingsAsync` applies `IsEnabled` to the live type at once, and `WorkflowTypeDraftExtensions.ApplyTo` copies the draft's `Name` and `IsEnabled` onto the live type at publish. So `enableWorkflowType` / `disableWorkflowType` must change the live type and, when a draft exists, the draft too (or route through `UpdateSettingsAsync`), otherwise the next publish reverts the flag. For the same reason `saveWorkflowType` and `updateWorkflowDraft` should not accept `IsEnabled` for an existing workflow.
- Clone logic lives in `WorkflowTypeController.Clone` (POST) and must be reproduced; the UI keeps the source's enabled state, the tool should not.
- Export logic is `AllWorkflowTypeDeploymentSource.ProcessWorkflowType` (static, module assembly): serialize with `DocumentJsonSerializerOptions`, drop `Id` and `VersionId`.
- HTTP Request events: the recipe step regenerates the signed URL (`ISecurityTokenService.CreateToken(new WorkflowPayload(...))`) when the activity has a `TokenLifeSpan` property; the designer does it with `HttpWorkflowController.GenerateUrl` (`ManageWorkflows`). The draft manager does neither, so `saveWorkflowType` / `updateWorkflowDraft` must generate the `Url` property for an HTTP Request event that has none. `WorkflowPayload` is in the module assembly.
- Risk. `ManageWorkflows` is flagged `isSecurityCritical` in `WorkflowsPermissions`: a published workflow runs server-side JavaScript and Liquid (`ScriptTask`, expressions), calls URLs (`HttpRequestTask`), sends email and SMS, creates tenants and exposes anonymous token URLs (`HttpRequestEvent`). That is why publish is classed `Security` with approval even though saving a draft is a plain write.
- Retire `createOrUpdateWorkflow`, or keep the name as an alias of `saveWorkflowType`. In either case stop passing an arbitrary recipe through without approval.
- Rewrite `workflow-agent.md`: list activities → get the schema of each activity used → save the draft → fix the issues → ask before publishing → enable → test with `runWorkflowType` when the workflow can be run by hand → read the instance to confirm.

**Test prompts:**

- "Which workflows do we have, which ones are turned off, and which have unpublished changes?"
- "Create a workflow that emails the site owner whenever a Blog Post is published. Don't turn it on yet."
- "In the 'Order follow-up' workflow, add a notification to the Administrator role after the email step, then show me what changed compared with the published version."
- "Turn off the 'Welcome email' workflow and make a copy of it called 'Welcome email v2'."

## Workflows: instances, journal and runs — `OrchardCore.Workflows`

**What an admin can do in the UI:**

- Instances of one workflow (`WorkflowController.Index`, route `Admin/Workflows/Types/{workflowTypeId}/Instances/Index`) or of every workflow (`WorkflowController.All`, route `Admin/Workflows/Instances`): count per status (All, Halted, Running = Idle/Starting/Resuming/Executing, Faulted, Finished, Aborted), filters (status, created: any time / last 24 hours / 7 days / 30 days, workflow on the all-instances page, order by creation date), paging.
- Bulk actions on checked rows (`ApplyBulkActionAsync`): **Retry** (faulted instances, from the faulted activity found by `GetFaultedActivityIdAsync`, an internal extension in the module's `Helpers/WorkflowFaultExtensions.cs`; needs `ExecuteWorkflows` on top of `ManageWorkflows`), **Cancel** (sets `Aborted`, clears blocking activities and the pending retry, resumes the parent of a child that had not ended), **Delete** (deletes the instance and its journal, resumes the parent).
- Row actions: **Restart** (`WorkflowController.Restart`, only for instances that are not Executing, Halted, Resuming or Starting; honours singleton locks; starts a new instance with the original input on the current definition), **Delete** (`WorkflowController.Delete`).
- Instance page (`WorkflowController.Details` + `WorkflowDesignerController.Instance` and `JournalData`): the version the instance runs on in a read-only designer, blocking activities, variable values, fault message and faulted activity, pending retry, parent and child instances, the journal (up to 500 records: activity, status, outcomes, duration, error), the recorded data of one activity run (when `RecordActivityData` is on), the state as JSON, and **Retry from here** (`WorkflowDesignerController.Retry`, `ExecuteWorkflows`).
- **Run…** in the designer (`WorkflowDesignerController.Run`, `ExecuteWorkflows`): runs the published version of an enabled workflow that starts with `StartedByWorkflowEvent`, with values for its input variables, and shows status, fault and output variables. For a workflow that starts with `HttpRequestEvent` the browser sends the request to the event's URL itself; `LatestInstance` then finds the instance. Workflows starting with any other event cannot be run by hand.
- Background tasks (managed by the Background Tasks module, not here): "Workflow Retries" (every minute, `WorkflowRetryBackgroundTask`).

**Already covered:** nothing.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listWorkflowInstances` | Read | no | `ManageWorkflows` | Lists the instances of one workflow or of all workflows, with the count per status; filter by status (Halted, Running, Faulted, Finished, Aborted), creation window and order, with paging. Each row has the instance id, workflow, version, status, creation date, fault message, what it waits on and when a retry is due. | WorkflowAgent |
| `getWorkflowInstance` | Read | no | `ManageWorkflows` | Returns one instance: status, the version it runs on, the activities it waits on, its variable values, its fault and faulted activity, pending retry, parent and child instances, and its journal (activities run, outcomes, durations, errors). Use it to explain what a run did or why it failed; pass `sequence` to get the recorded data of one activity run. | WorkflowAgent |
| `runWorkflowType` | External | yes | `ExecuteWorkflows` | Starts a new instance of an enabled workflow's published version that starts with "Started by Workflow", with values for its input variables, and returns the instance id, status, fault and output variables. Workflows that start with another event cannot be started this way; for an HTTP Request start it returns the URL, method and expected parameters instead. | WorkflowAgent |
| `rerunWorkflowInstances` | External | yes | `ExecuteWorkflows` for `retry`; `ManageWorkflows` for `restart` | Runs instances again: `retry` continues faulted instances from the activity that faulted (or a given activity) with their state; `restart` starts a new instance on the published version with the original input. Activities run again, so emails, HTTP calls and content changes can repeat. | WorkflowAgent |
| `cancelWorkflowInstances` | Destructive | yes | `ManageWorkflows` | Cancels instances that have not ended: they become Aborted, stop waiting for their events and drop a pending retry. A cancelled instance cannot be resumed. | WorkflowAgent |
| `deleteWorkflowInstances` | Destructive | yes | `ManageWorkflows` | Permanently deletes instances and their journals. Use `cancelWorkflowInstances` when the history should be kept. | WorkflowAgent |

**Implementation notes:**

- Listing and counting use `WorkflowIndex` (`OrchardCore.Workflows/Indexes/WorkflowIndexProvider.cs`, module assembly) directly in the controller (`BuildListAsync`). `IWorkflowStore` only offers `ListAsync(workflowTypeId, skip, take)` and `CountAsync(workflowTypeId)` without a status or date filter. The filter logic must be extracted into a service, or the tool needs the module reference and `ISession`.
- Details: `IWorkflowStore.GetAsync(long)`, `IWorkflowTypeVersionStore.GetWorkflowTypeAsync(type, workflow.WorkflowTypeVersionId)`, `IWorkflowExecutionJournal.ListAsync(workflowId, count)` and `GetAsync(workflowId, sequence)`; the variable values are read out of `Workflow.State` as `WorkflowDesignerController.VariableValuesOf` does (private, to reproduce).
- Privacy: `Workflow.State` and journal `Data` can hold personal data and keys (the workflow setting says so). Return the state only on request and consider truncating values; everything returned goes to the model provider.
- Run: the logic is inside `WorkflowDesignerController.Run` (`RunOf`, type lock via `IDistributedLock.TryAcquireWorkflowTypeLockAsync`, singleton check with `IWorkflowStore.HasHaltedInstanceAsync`, input coercion with `IWorkflowVariableType.TryCoerce`, then `IWorkflowManager.StartWorkflowAsync(workflowType, start, input)`). It must be reproduced or extracted. The HTTP mode cannot be reproduced faithfully in-process, because the workflow's activities read the current `HttpContext.Request`, which would be the chat request.
- Retry: `IWorkflowManager.RetryActivityAsync(workflow, activityId)` (returns null when the instance is running; throws `ArgumentException` when the activity is gone). Finding the faulted activity (`GetFaultedActivityIdAsync`) is an internal helper of the module and has to be reproduced from the journal records. Restart: `IWorkflowManager.RestartWorkflowAsync(workflow, workflowType)` after the same lock and singleton checks as `WorkflowController.Restart`.
- Cancel and delete: reproduce `ApplyBulkActionAsync` including `TryAcquireWorkflowExclusiveLockAsync`, re-reading the instance under the lock, and `IWorkflowManager.ResumeParentOfStoppedChildAsync`. The single-row `Delete` action in the controller does not resume the parent; the tool should follow the bulk path.
- Take explicit instance ids (cap the number per call, for example 50) rather than a filter, as the UI only acts on the checked rows of a page.

**Test prompts:**

- "Are any workflows failing? Show me the faulted instances from the last 7 days."
- "Why did instance 4812 of the 'Order follow-up' workflow fail, and which step was it on?"
- "Retry the three faulted 'Send invoice' instances from today."
- "Run the 'Recalculate totals' workflow with orderId 1042 and tell me what it returned."

## Workflows: HTTP, signal, user task and timer triggers — `OrchardCore.Workflows.Http`, `OrchardCore.Workflows.Timers`, `OrchardCore.Workflows` (user tasks)

**What an admin can do in the UI:**

- User tasks (`UserTasks/Startup.cs`, active when Workflows, Contents and Roles are enabled): when an instance waits on a `UserTaskEvent` correlated to a content item, the content item's editor shows one button per action of that event ("{Action} and Save", "{Action} and Publish", from `UserTaskEventContentDriver` and `Views/Content.UserTaskButton.cshtml`), only to users who have one of the roles set on the activity (any user when no role is set). Clicking it saves or publishes the item and triggers `UserTaskEvent` with the chosen action, the content item and its id as correlation id, which resumes the instance on the outcome of that name.
- HTTP feature activities: `HttpRequestEvent`, `HttpRequestFilterEvent`, `SignalEvent`, `HttpRedirectTask`, `HttpRequestTask`, `HttpResponseTask`.
- In the editor of an HTTP Request event: **generate the URL** (`HttpWorkflowController.GenerateUrl`, POST, `ManageWorkflows`): a signed token for the workflow type id and activity id, valid `tokenLifeSpan` days (0 = 36500 days).
- Public endpoints, no admin page: `workflows/Invoke?token=` / `workflows/invoke/{token}` (starts the workflow when the activity is its start activity, otherwise resumes the instances halted on it; returns not found when the workflow is disabled) and `workflows/Trigger?token=` (resumes instances waiting on a `SignalEvent` by instance id, or triggers the signal for a correlation id). Signal URLs are produced inside workflows with the `signal_url` Liquid filter or the `signalUrl()` script function, valid 7 days.
- Timers: `TimerEvent` (`CronExpression`, default `*/5 * * * *`, and `UseLocalTime`) started and resumed every minute by the "Timed Workflow Starter" background task (`TimerBackgroundTask`). There is nothing to manage beyond the activity's own settings.

**Already covered:** the activities themselves are edited with the workflow type tools above; the timer is the way to schedule anything, including data pipeline runs. Nothing covers sending a signal or choosing a user task action. The content tools (`createOrUpdateContentItem`, `publishContentItem`) do not go through the content editor driver, so they never trigger a user task.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `triggerWorkflowSignal` | External | yes | `ExecuteWorkflows` (unverified: the UI has no such action; the public endpoint is protected only by its signed token) | Sends a named signal: resumes the instances waiting on a Signal event with that name for a given instance id or correlation id, and starts enabled workflows whose start activity is that signal. Use it to move a waiting workflow forward, for example an approval step. | WorkflowAgent |
| `completeWorkflowUserTask` | External | yes | No permission constant in code: the calling user must hold one of the roles set on the waiting `UserTaskEvent` (the check `UserTaskEventContentDriver` makes) and be allowed to edit the content item (the buttons only exist in its editor) | Chooses a user task action, such as Approve or Reject, for a content item that a workflow is waiting on, which resumes that workflow on the matching outcome. Called without an action, or with an unknown one, it returns the actions the current user may choose for that item. | WorkflowAgent |

**Implementation notes:**

- User task: reproduce `UserTaskEventContentDriver`: list the instances halted on `UserTaskEvent` for the content item (`IWorkflowStore.ListByActivityNameAsync("UserTaskEvent", contentItemId)`), read each blocking activity's `Actions` and `Roles` from the instance state, check the caller's role claims, then `IWorkflowManager.TriggerEventAsync("UserTaskEvent", { UserAction, ContentItem, ContentEvent }, correlationId: contentItemId)`. The input keys are `ContentEventConstants.UserActionInputKey`, `ContentItemInputKey` and `ContentEventInputKey`. The tool does not save or publish the item (the UI button saves or publishes it in the same submit); pair it with the content tools when that is wanted.
- Signal: same logic as `HttpWorkflowController.Trigger`: with an instance id, load the instance, take `TryAcquireWorkflowLockAsync`, and `IWorkflowManager.ResumeWorkflowAsync(workflow, signalActivity, { "Signal": name })` for each blocking `SignalEvent`; otherwise `IWorkflowManager.TriggerEventAsync("SignalEvent", { "Signal": name }, correlationId)`.
- `SignalEvent.EventName` and `SignalPayload` are in the module assembly; the tool only needs the activity name string `SignalEvent`.
- Tokens are self-contained (`SecurityTokenService` is a time-limited data protector; nothing is stored), so a generated HTTP URL cannot be revoked before it expires other than by disabling or deleting the workflow, or by removing that start activity from the published definition (`HttpWorkflowController.Invoke` only starts an instance for an activity of the current definition); generating a new one does not invalidate the old one. That is why URL generation is folded into the draft tools and gated by the publish approval, not offered as a separate "regenerate" tool.

**Test prompts:**

- "The expense approval for request 77 is stuck waiting. Send it the 'Approved' signal."
- "The article 'Summer sale' is waiting for review. Approve it."
- "Make a workflow that runs every weekday at 7am site time and logs 'Morning run'."
- "What URL do I call to start the 'Inbound lead' workflow, and with which HTTP method?"

## Workflow Trimming — `OrchardCore.Workflows` (settings page)

**What an admin can do in the UI:** Settings → Workflow Trimming (`Trimming/AdminMenu.cs`): retention days, disable trimming, the statuses to trim; the page also shows the last run time (read-only, from the `WorkflowTrimmingState` document). The "Workflow Trimming Background Task" runs daily (`0 0 * * *`) and deletes up to `Workflows:Trimming:BatchSize` (5000, configuration only) instances older than the retention.

**Already covered:** `applySiteSettings` / the `settings` recipe step with `WorkflowTrimmingSettings` (schema in `Recipes.Core/Schemas/SiteSettings/WorkflowTrimmingSettingsSchema.cs`); the generic settings trio will cover it.

**Settings groups:** `WorkflowTrimmingSettings` / group `WorkflowTrimmingSettings` / permission `ManageWorkflowSettings` (implied by `ManageWorkflows`) / secrets: none. Fields: `RetentionDays` (default 90), `Disabled`, `Statuses` (`WorkflowStatus[]`: Idle, Starting, Resuming, Executing, Halted, Finished, Faulted, Aborted; null means every status in `WorkflowTrimmingService`, an empty array trims nothing).

No dedicated tool: there is no "trim now" button in the UI; running the background task now belongs to the Background Tasks group.

**Implementation notes:** `Workflows:Versions:MaxCount`, `Workflows:Journal:Enabled` / `MaxRecordsPerInstance` and `Workflows:Trimming:BatchSize` are appsettings only (`ConfigurationSchema.json`); not reachable by a tool.

**Test prompts:**

- "How long do we keep finished workflow instances? Change it to 30 days and only trim finished and aborted ones."

## Data Pipelines: pipelines and the designer — `OrchardCore.DataPipelines`

**What an admin can do in the UI:**

- Tools → Data Pipelines → Pipelines (`AdminController.Index`, route `Admin/DataPipelines`, `ViewDataPipelines`): list ordered by name, search (`q`), paging; each row shows Draft / Version N / Unpublished changes / Disabled and the status of the last run. **Add Pipeline** (`Create`: name, description; `ManageDataPipelines`) opens the designer.
- Row: **Edit** (or **View** without `ManageDataPipelines`); Actions: **Run now** (`RunNow`, `RunDataPipelines`), **Run history**, **Start over** (`StartOver`, `ManageDataPipelines`: clears the values kept between runs so the next run reads everything again; refused while a run is queued or running), **Delete** (`Delete`, `ManageDataPipelines`: deletes the pipeline, its versions, runs and state).
- Designer (`AdminController.Edit` + `DataPipelineDesignerController`, route `Admin/DataPipelines/{pipelineId}/Designer/{action}`): view endpoints `Definition`, `Library`, `Fields`, `Versions`, `Runs` (`ViewDataPipelines`); edit endpoints `Save` (positions, connections, removed and restored steps), `AddStep`, `DataSets`, `AddDataSource`, `Editor` GET/POST (a step's settings through its display driver, including an optional title), `Settings` GET/POST (name, description, enabled; not versioned, applied at once), `Preview`, `Publish`, `Discard`, `Restore` (`ManageDataPipelines`); `Run`, `CancelRun` (`RunDataPipelines`).
- Draft and versions: every edit goes to `DataPipeline.Draft` with a revision check (`DataPipelineConflictException`); **Publish** is refused while the analysis has errors and creates a `DataPipelineVersion`; runs only execute the published version, with the access of the user who published it (`PublishedByUserId`).
- Preview (the Data tab): runs the draft up to one step with the previewing user's access, at most `PreviewRowLimit` rows per source, never executes destinations, stops after two minutes.
- Step types (21): sources `DataSource`, `ReadMediaFile`; transforms `Filter`, `CalculatedFields`, `ConvertTimeZone`, `SelectFields`, `Sort`, `Aggregate`, `Join`, `Union`, `Distinct`, `Limit`; files `CreateFile`, `ZipFiles`; destinations `SaveToMedia`, `ShareDownloadLink`, `SendToWebApi`, `SendByEmail`, `SaveContentItems`, `UploadToFtp`, `UploadToSftp`. Media, Email, Contents, FTP and SFTP steps appear only when those features are enabled.
- There is no schedule page: a pipeline runs on a schedule through a workflow (`TimerEvent` → `RunDataPipelineTask`, with "Skip when a run is already queued or running").
- There is no settings page: the limits (`StepMemoryLimitMegabytes`, `MaxConcurrentRuns`, `RunRetentionDays`, `PreviewRowLimit`) are `DataPipelineOptions`, bound to the `OrchardCore:DataPipelines` configuration section only.

**Already covered:** no AI tool. Recipe step `DataPipelines` (usable through `importOrchardCoreRecipe`; schema in `Recipes.Core/Schemas/Steps/DataPipelinesRecipeStep.cs`): creates a pipeline or updates the name, description, enabled flag and draft of an existing one. It never publishes, cannot run anything, cannot set secrets, and its step `Properties` schema is free-form. Deployment step `DataPipelines` (exports all or selected pipelines without secrets). The workflow activities `RunDataPipelineTask` and `DataPipelineRunCompletedEvent` are reachable through the workflow tools (both have activity schemas).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listDataPipelines` | Read | no | `ViewDataPipelines` | Lists the data pipelines with id, name, description, enabled state, published version number, whether they have unpublished changes and the status and time of their last run; optional name filter and paging. Use it to find a pipeline's id. | DataPipelineAgent |
| `getDataPipeline` | Read | no | `ViewDataPipelines` | Returns one pipeline: its steps with their settings (secrets shown only as set or not set), connections, issues, revision, versions and the workflows that run it, from the draft, the published version or a given version. Pass `fieldsOfStepId` to get the fields a step receives and produces, which formulas and mappings must use. | DataPipelineAgent |
| `listDataPipelineStepTypes` | Read | no | `ViewDataPipelines` | Lists the step types available on this site by category (source, transform, file, destination) with their input and output ports; for the names passed in `schemaFor` it also returns the settings schema, the settings key to use under `Properties` and which settings are secrets. Call it before adding or editing a step. | DataPipelineAgent |
| `saveDataPipeline` | Write | no | `ManageDataPipelines` | Creates a pipeline from a name and description, or changes those and replaces the draft of an existing pipeline with a full set of steps and connections. It never publishes or runs; the result lists the issues to fix. Secrets cannot be set here and existing ones are kept. | DataPipelineAgent |
| `updateDataPipelineDraft` | Write | no | `ManageDataPipelines` | Applies small changes to a pipeline's draft: add a step (optionally connected after another step's output), change a step's settings or title, remove a step, connect or disconnect ports, or restore a version into the draft. Needs the revision from the last read and returns the new revision, the issues and the changed step's output fields. | DataPipelineAgent |
| `previewDataPipelineStep` | Read | no | `ManageDataPipelines` | Runs the draft up to one step and returns the first rows that step produces (or the rows or files a destination would receive), reading data with the current user's access. It never delivers anything and stops after two minutes; use it to check a pipeline before publishing. | DataPipelineAgent |
| `publishDataPipeline` | Security | yes | `ManageDataPipelines` | Publishes a pipeline's draft as its next version. From then on every run reads and writes data with the access of the user who published, so use it only after the user confirmed; refused while the pipeline has errors. | DataPipelineAgent |
| `discardDataPipelineDraft` | Destructive | yes | `ManageDataPipelines` | Drops the unpublished changes of a pipeline so it shows its published version again. Cannot be undone. | DataPipelineAgent |
| `enableDataPipeline` | Write | no | `ManageDataPipelines` | Enables a pipeline so it can be run by hand and by workflows. Takes effect at once. | DataPipelineAgent |
| `disableDataPipeline` | Write | no | `ManageDataPipelines` | Disables a pipeline: Run now is refused and the Run Data Pipeline workflow task takes its Failed outcome. Its design and history are kept. | DataPipelineAgent |
| `resetDataPipelineState` | Destructive | yes | `ManageDataPipelines` | "Start over": forgets what the pipeline's runs kept, such as how far an incremental read went, so the next run reads every row again and may deliver data a second time. Refused while a run is queued or running. | DataPipelineAgent |
| `deleteDataPipeline` | Destructive | yes | `ManageDataPipelines` | Permanently deletes a pipeline with its versions, run history and kept state. Prefer `disableDataPipeline` when it should only stop running. | DataPipelineAgent |

**Implementation notes:**

- Services: `DataPipelineManager` (`ListAsync(search, skip, take)`, `GetAsync`, `CreateAsync`, `SaveDraftAsync(pipeline, expectedRevision, change, user)`, `UpdateSettingsAsync`, `PublishAsync(pipeline, user)`, `DiscardDraftAsync`, `ListVersionsAsync`, `GetVersionAsync`, `RestoreAsync`, `DeleteAsync`), `DataPipelineStateManager.ClearAsync`, `DataPipelineRunManager.HasActiveRunAsync`. Gap: these are concrete public classes in the `OrchardCore.DataPipelines` module assembly with no interface in `OrchardCore.DataPipelines.Abstractions`; no CrestApps project references that module package. Same decision as for workflows: reference the module package from AI.Agent or lift interfaces into the abstractions project.
- Validation: `DataPipelineAnalyzer.AnalyzeAsync(definition, user, services, cancellationToken)` (`OrchardCore.DataPipelines.Core`) returns the issues (`DataPipelineIssue`: severity, message, step id) and per-step input and output fields (`DataPipelineStepAnalysis`). Return the issues from every write, as the designer does. The step editors add their own model validation in the display drivers; a tool that writes settings with `step.Put(settings)` skips that, so rely on the analyzer and keep the schema strict.
- Step catalog: `IDataPipelineStepTypeManager.GetStepTypes()` gives name, display name, description and category; `IDataPipelineStepType.GetInputs/GetOutputs(step)` give the ports. Gap: nothing maps a step type to its settings class (`DataSourceStepSettings`, `CreateFileStepSettings`, `UploadToSftpStepSettings`...; each step calls `step.GetOrCreate<TSettings>()` itself), and the recipe schema leaves `Properties` free-form. A settings-schema registry per step type has to be added (same idea as `IWorkflowActivitySchemaDefinition`).
- Secrets: settings whose names start with `Protected` hold values encrypted by `DataPipelineSecrets` (data protection, purpose `OrchardCore.DataPipelines.Secrets`). Known ones: `UploadToFtpStepSettings.ProtectedPassword`; `UploadToSftpStepSettings.ProtectedPassword`, `ProtectedPrivateKey`, `ProtectedPassphrase`; `SendToWebApiStepSettings.ProtectedPassword`, `ProtectedToken`, `ProtectedApiKey`. Tools must never return them (use `DataPipelineSecrets.RemoveProtectedValues` and report "set / not set"), must carry the existing `Protected*` values over by `StepId` when a draft is replaced or a step is updated, and should not accept secret values (they would pass through the model provider). The admin enters secrets in the designer; the agent tells the user which steps still need one.
- Publish stores the caller as `PublishedByUserId`, and runs then act with that user's access. The tool must pass the real chat user (`ClaimsPrincipal`), never a system identity. `ManageDataPipelines` is `isSecurityCritical` for exactly this reason, hence `Security` + approval.
- Preview: the controller builds the `DataPipelineRunContext` itself (temp folder under `App_Data/Sites/{tenant}/DataPipelines/Previews/{id}`, `IsPreview`, `PreviewRowLimit`, two-minute timeout, child scope per step) and calls `DataPipelineExecutor.PreviewAsync`. This block has to be extracted to be reused.
- "The workflows that run it" in `getDataPipeline`: scan `IWorkflowTypeStore.ListAsync()` for activities named `RunDataPipelineTask` whose `PipelineId` property matches (only when Workflows is enabled).
- Scheduling is a workflow, not a pipeline property. The main assistant has to ask `WorkflowAgent` for it (delegation is one level deep), or see the open question about a convenience tool.

**Test prompts:**

- "Which data pipelines do we have and when did each last run?"
- "Build a pipeline that exports all published Blog Posts with title, author and publish date to an Excel file in the media folder 'exports'. Show me a preview before publishing."
- "In the 'Orders export' pipeline, only keep orders from the last 30 days and sort them by total, highest first."
- "The 'Customer sync' pipeline delivered nothing new. Make its next run send everything again."

## Data Pipelines: runs and run history — `OrchardCore.DataPipelines`

**What an admin can do in the UI:**

- **Run now** from the list (`AdminController.RunNow`) or **Run** in the designer (`DataPipelineDesignerController.Run`), both `RunDataPipelines`: queue a run of the published version of an enabled pipeline. Runs execute in the background, one per pipeline at a time, at most `MaxConcurrentRuns` per tenant.
- Tools → Data Pipelines → Run History (`AdminController.Runs`, route `Admin/DataPipelines/Runs/{pipelineId?}`, `ViewDataPipelines`): runs of every pipeline or of one, most recent first, search by pipeline name, filter by status (Queued, Running, Succeeded, Failed, Cancelled), paging.
- Run page (`AdminController.Run`): when it was queued, started and ended, what triggered it and who, version, error and failed step, each step with status, rows and files in and out and warnings, deliveries, and the log (last 200 entries).
- **Cancel** a queued or running run (`AdminController.CancelRun` / designer `CancelRun`, `RunDataPipelines`).
- Background task "Data pipeline runs" (every minute): starts missed queued runs, fails runs that stopped reporting for 10 minutes, deletes runs older than `RunRetentionDays`.

**Already covered:** nothing as a tool. A workflow can run a pipeline with `RunDataPipelineTask` and react to `DataPipelineRunCompletedEvent`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `runDataPipeline` | External | yes | `RunDataPipelines` | Queues a run of a pipeline's published version, optionally with run parameters, and returns the run id; the run executes in the background and can deliver files by FTP, SFTP, email or web API and create content. Refused when the pipeline is disabled or was never published; follow it with `getDataPipelineRun`. | DataPipelineAgent |
| `listDataPipelineRuns` | Read | no | `ViewDataPipelines` | Lists runs of one pipeline or of all, most recent first, filtered by status (Queued, Running, Succeeded, Failed, Cancelled) or pipeline name, with paging. Each row has the run id, pipeline, version, status, trigger, who started it, times and error. | DataPipelineAgent |
| `getDataPipelineRun` | Read | no | `ViewDataPipelines` | Returns one run: status, times, trigger and parameters, error and failed step, each step's status with rows and files in and out and warnings, what was delivered, and the log. Use it to check that a run finished or to explain why it failed. | DataPipelineAgent |
| `cancelDataPipelineRun` | Write | no | `RunDataPipelines` | Cancels a run that has not ended: a queued run is cancelled at once and a running run stops at its next batch. Data already delivered stays delivered. | DataPipelineAgent |

**Implementation notes:**

- `DataPipelineRunManager.QueueAsync(pipeline, new DataPipelineRunRequest { Trigger = "Manual", TriggeredBy = user, Parameters = ... })`, `ListAsync(DataPipelineRunFilter, skip, take)`, `GetAsync(runId)`, `CancelAsync(run, user)` (module assembly, same gap as above). `QueueAsync` throws `InvalidOperationException` for an unpublished or disabled pipeline; check first and answer with a clear message, as both controllers do.
- Run parameters: the UI's Run now passes none; only `RunDataPipelineTask` passes them, but `DataPipelineRunRequest.Parameters` accepts them. Offering them in the tool goes slightly beyond the UI and lets the model test pipelines that use `{Parameter:name}`.
- The run is asynchronous (a deferred task dispatches it after the request commits), so the tool cannot wait for the result in the same call; the agent must poll `getDataPipelineRun`.
- Resolve step names for the result as `AdminController.Run` does (step title, else the localized step type name).
- The SFTP step writes the host key fingerprint the server presented into the run log when none is configured; that is how the agent can find it (see the FTP and SFTP section).

**Test prompts:**

- "Run the 'Nightly orders export' pipeline now and tell me when it's done and what it delivered."
- "Which pipeline runs failed this week, and why?"
- "Cancel the 'Full catalog export' run that is still going."

## Data Pipelines Core: data sets and shared links — `OrchardCore.DataPipelines.Core`

**What an admin can do in the UI:**

- Data sets: the designer's **Add a data source** dialog (`DataPipelineDesignerController.DataSets`, `ManageDataPipelines`) lists every data set the user may read from every registered data source (`IDataSourceManager`: Contents, Users, Queries in Orchard Core, plus the data sources CrestApps modules register), with name, description, data source and group; the step's Fields tab then shows the fields with their types.
- Tools → Shared Links (`SharedLinksAdminController`, route `Admin/SharedLinks`, `ManageSharedLinks`): list the links of every module (pipeline files from the **Share a download link** step, report shares...), search by title or by what shared them, with kind, recipients, expiry, uses and state; **Revoke** a link (it stops working, the resource stays until deletion); **Delete** a link and its resource.
- Recipients open a link at `SharedLinks/{linkId}?token=` (`SharedLinkController.Open`, signed-in recipients only). The token is only ever emailed; the admin cannot see it.
- Background task "Shared links cleanup" (every 15 minutes) deletes links a day after they stopped working.

**Already covered:** nothing.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listDataSets` | Read | no | `ManageDataPipelines` | Lists the data sets the current user may read (content types, users, saved queries and module data), with their `Source::DataSet` value, name, description, data source and group; optional text and data source filter. Use it to pick what a "Read a data source" step reads. | DataPipelineAgent |
| `getDataSetSchema` | Read | no | `ManageDataPipelines` | Returns the fields of one data set with their names, labels and types, and the parameters it takes. Use it before writing the fields, filters and formulas of a pipeline that reads it. | DataPipelineAgent |
| `listSharedLinks` | Read | no | `ManageSharedLinks` | Lists the shared links of every module with their title, kind, what shared them, recipients, expiry, uses, and whether they still work; optional text filter and paging. It never returns the link's secret URL. | DataPipelineAgent |
| `revokeSharedLink` | Security | yes | `ManageSharedLinks` | Stops a shared link from working for all its recipients. The shared file or resource is kept until the link is deleted. | DataPipelineAgent |
| `deleteSharedLink` | Destructive | yes | `ManageSharedLinks` | Deletes a shared link together with the resource it shared, such as the stored copy of an exported file. Cannot be undone. | DataPipelineAgent |

**Implementation notes:**

- Data sets: `IDataSourceManager.GetDataSources()` + `IDataSource.GetDataSetsAsync(new DataSourceContext { User = user })` (the same loop as `DataSetOption.ListAsync`, which lives in the module's view models) and `IDataSource.GetSchemaAsync(dataSet, context)`. These interfaces are in `OrchardCore.DataSources.Abstractions`, which CrestApps already references (Reports, DataConnectors), so no module reference is needed here.
- The UI lists data sets only to users with `ManageDataPipelines`; the data sources themselves filter by the user's own read access.
- Shared links: `ISharedLinkManager` (`OrchardCore.DataPipelines.Abstractions`): `ListAsync(search, skip, take)`, `GetAsync(linkId)`, `RevokeAsync(link)`, `DeleteAsync(link)`. Never expose `SharedLink.TokenHash`. Compute "still works" with `SharedLink.IsActive(utcNow)`.
- Creating a link is not an admin action in this UI (links are created by the Share a download link step and by other modules through `ISharedLinkManager.CreateAsync`), so no create tool is proposed here.

**Test prompts:**

- "What data can I build a pipeline or report from? Do we have anything about orders?"
- "Which fields does the Blog Post data set have?"
- "Who still has a working download link to last month's export? Revoke the one for the finance file."

## Data Pipelines FTP and SFTP — `OrchardCore.DataPipelines.Ftp`, `OrchardCore.DataPipelines.Sftp`

**What an admin can do in the UI:** each module only registers one destination step type with its editor (`Startup.cs`: `AddDataPipelineStep<UploadToFtpStep, ...>` / `AddDataPipelineStep<UploadToSftpStep, ...>`). No menu entry, controller, permission, settings page, recipe step or background task.

- `UploadToFtp` settings (`UploadToFtpStepSettings`): `Host`, `Port` (21), `Encryption` (ExplicitTls default, implicit TLS, none), `ValidateCertificate` (true), `Username`, `RemoteFolder`, `Overwrite` (true), `TimeoutSeconds` (30); secret: `ProtectedPassword`.
- `UploadToSftp` settings (`UploadToSftpStepSettings`): `Host`, `Port` (22), `Username`, `HostKeyFingerprint`, `RemoteFolder`, `Overwrite` (true), `TimeoutSeconds` (30); secrets: `ProtectedPassword`, `ProtectedPrivateKey`, `ProtectedPassphrase`.
- SFTP host keys: there is no host key store or page. `HostKeyFingerprint` is a plain step setting (SHA-256, `SHA256:` prefix optional). When it is empty the step adds a warning issue and, during a run, logs "The host key of the SFTP server '{host}' isn't verified. To verify it, set its fingerprint: SHA256:{presented}". When it is set and the server presents another key, the connection is refused.

**Already covered:** the step types are listed by `listDataPipelineStepTypes` and edited with `saveDataPipeline` / `updateDataPipelineDraft` proposed above. No tool of their own.

**Implementation notes:**

- Host key flow for the agent: read the presented fingerprint from the run log with `getDataPipelineRun`, show it to the user, and set `HostKeyFingerprint` with `updateDataPipelineDraft` only after the user confirmed it against the server operator's value. Trusting the first key seen is a security decision; it must not be automatic.
- Passwords, private keys and passphrases are not set by tools (see the secrets note above). A pipeline imported or built by the agent with an FTP, SFTP or web API step stays unpublishable or failing until an admin enters the secret in the designer.
- The FTP step warns when the connection is not encrypted; surface that issue to the user rather than switching encryption off silently.

**Test prompts:**

- "Add an SFTP upload to the 'Orders export' pipeline: host sftp.example.com, user exports, folder /incoming. Tell me what I still have to fill in myself."
- "The last run of 'Orders export' warned about an unverified host key. What fingerprint did the server present?"

## No tools needed

| Module | Reason | Settings group (if any) |
| --- | --- | --- |
| `OrchardCore.Workflows.Timers` | Only adds the `TimerEvent` activity and the "Timed Workflow Starter" background task; the timer is configured as an activity through the workflow type tools. | none |
| `OrchardCore.Workflows.Session` | Only adds the `CommitTransactionTask` activity. | none |
| `OrchardCore.Workflows` trimming | A settings page and a daily background task; covered by the generic settings trio. Batch size is appsettings only. | `WorkflowTrimmingSettings` (`ManageWorkflowSettings`, no secrets) |
| `OrchardCore.Workflows` real-time designer (`RealTime/RealTimeStartup.cs`, needs `OrchardCore.SignalR`) | Live designer updates for people; draft changes made by tools are broadcast automatically when they go through `IWorkflowTypeDraftManager`. | none |
| `OrchardCore.Workflows` configuration (`Workflows:Versions`, `Workflows:Journal`, `Workflows:Trimming`) | appsettings only. | none |
| `OrchardCore.DataPipelines.Ftp` | One step type, edited with the pipeline draft tools; its password is a secret tools do not set. | none |
| `OrchardCore.DataPipelines.Sftp` | One step type, edited with the pipeline draft tools; its password, private key and passphrase are secrets tools do not set; the host key fingerprint is a plain step setting. | none |
| `OrchardCore.DataPipelines` configuration (`OrchardCore:DataPipelines`: `StepMemoryLimitMegabytes`, `MaxConcurrentRuns`, `RunRetentionDays`, `PreviewRowLimit`) | appsettings only; there is no settings page. | none |
| Background tasks of this group ("Workflow Retries", "Timed Workflow Starter", "Workflow Trimming Background Task", "Data pipeline runs", "Shared links cleanup") | Managed (run now, schedule, enable) by the Background Tasks module, another group. | none |

## Proposed agents for this group

| Agent | Purpose (one sentence) | Tools | Could merge with |
| --- | --- | --- | --- |
| WorkflowAgent (existing, expanded and re-prompted) | Reads, explains, builds, validates, publishes, enables, runs and troubleshoots the site's workflows and their instances. | 21: `listWorkflowTypes`, `getWorkflowType`, `listWorkflowActivities`, `getWorkflowActivity`, `saveWorkflowType`, `updateWorkflowDraft`, `publishWorkflowType`, `discardWorkflowDraft`, `listWorkflowTypeVersions`, `cloneWorkflowType`, `enableWorkflowType`, `disableWorkflowType`, `deleteWorkflowType`, `listWorkflowInstances`, `getWorkflowInstance`, `runWorkflowType`, `rerunWorkflowInstances`, `cancelWorkflowInstances`, `deleteWorkflowInstances`, `triggerWorkflowSignal`, `completeWorkflowUserTask` | Should stay on its own: it is at 21 tools and needs long instructions. `completeWorkflowUserTask` could instead sit with ContentAgent, since it acts on a content item. A background-tasks area (run now, schedules) is the closest neighbour if that group stays small. |
| DataPipelineAgent (new) | Designs, previews, publishes, runs and monitors data pipelines, finds the data sets they can read, and manages shared download links. | 21: `listDataPipelines`, `getDataPipeline`, `listDataPipelineStepTypes`, `saveDataPipeline`, `updateDataPipelineDraft`, `previewDataPipelineStep`, `publishDataPipeline`, `discardDataPipelineDraft`, `enableDataPipeline`, `disableDataPipeline`, `resetDataPipelineState`, `deleteDataPipeline`, `runDataPipeline`, `listDataPipelineRuns`, `getDataPipelineRun`, `cancelDataPipelineRun`, `listDataSets`, `getDataSetSchema`, `listSharedLinks`, `revokeSharedLink`, `deleteSharedLink` | The CrestApps Reports / Report Builder area and the generic data-analyst agent share `listDataSets` and `getDataSetSchema` (same `IDataSourceManager`) and the shared links (reports create them too); those five tools could move to a common "DataAgent" or be ticked on both agents. Import/export and deployment are a neighbour for moving pipelines between sites. |

Settings: no agent; `WorkflowTrimmingSettings` goes through the generic `getSiteSettings` / `updateSiteSettings`.

## Open questions

- Module-assembly dependency. The draft, validation and run services needed for most of these tools are public but live in module assemblies (`OrchardCore.Workflows`: `IWorkflowTypeDraftManager`, `WorkflowIndex`, `WorkflowVariableValidator`, `WorkflowTypeDiff`, `WorkflowPayload`; `OrchardCore.DataPipelines`: `DataPipelineManager`, `DataPipelineRunManager`, `DataPipelineStateManager`). CrestApps references only the `.Abstractions` packages today and `OrchardCorePermissions.cs` states the intent to avoid module references. Decide: reference the module packages from AI.Agent, lift interfaces into the abstractions projects in the Orchard Core fork, or host these tools in Orchard Core itself.
- Controller-only logic to extract first: workflow instance filtering and counts (`WorkflowController.BuildListAsync`), bulk retry/cancel/delete (`ApplyBulkActionAsync`), restart checks, the designer Run (`WorkflowDesignerController.Run` / `RunOf`), clone, instance variable values, pipeline preview setup (`DataPipelineDesignerController.Preview`), data set listing (`DataSetOption.ListAsync`).
- Go-live approvals. I classed `publishWorkflowType` and `publishDataPipeline` as `Security` (both permissions are `isSecurityCritical` in code: a workflow runs server-side scripts and outbound calls unattended; a pipeline runs with the publisher's access). `enableWorkflowType` and `enableDataPipeline` are plain writes under the brief's rule, yet enabling is also a go-live moment. Should enable require approval too?
- `createOrUpdateWorkflow` today accepts any recipe without approval and without a permission check. Remove it, or keep the name as an alias of `saveWorkflowType` restricted to workflow definitions?
- Secrets in chat. I propose that no tool accepts or returns pipeline step secrets. If the chat widget gets a secure input that bypasses the model, a `setDataPipelineStepSecret` tool (Security) becomes possible; without it, pipelines with FTP, SFTP or authenticated web API steps always need a manual step in the designer.
- Running HTTP-started workflows. `runWorkflowType` can only return the URL for a workflow that starts with an HTTP Request event, because the workflow reads the live request. Should the tool make a loopback HTTP call to the site (needs the public base URL and care with antiforgery settings), or is returning the URL enough?
- Scheduling a pipeline needs both agents (timer workflow + pipeline). Delegation is one level deep, so the main assistant must orchestrate. Would a convenience tool such as `scheduleDataPipeline(pipelineId, cron, useLocalTime, parameters)` that creates, publishes and enables a small workflow (`TimerEvent` → `RunDataPipelineTask` with `SkipWhenActive`) be worth one more tool? It would need `ManageWorkflows` and approval.
- Bulk scope. The UI acts on checked rows of one page. I propose explicit ids with a cap per call; "retry every faulted instance of workflow X" would then need paging by the agent. Is a filter-based bulk action wanted?
- Sensitive data in reads. `getWorkflowInstance` (state, journal data) and `previewDataPipelineStep` (rows) return site data to the model provider. Do we need a redaction or row/field cap policy shared across agents?
- `triggerWorkflowSignal` has no UI equivalent and no existing permission check (the public endpoint relies on its signed token). `ExecuteWorkflows` is my choice; confirm.
- Step settings schemas. Data pipeline step settings have no schema source (unlike workflow activities). Who owns the new registry: Orchard Core (next to each step type) or `CrestApps.OrchardCore.Recipes.Core` (next to `DataPipelinesRecipeStep`, which already hard-codes the 21 step type names)?
- Tool placement. The brief puts Orchard Core module tools in `CrestApps.OrchardCore.AI.Agent`. Should the 21 data pipeline tools go there too (gated by `[RequireFeatures("OrchardCore.DataPipelines")]`), with the data set and shared link tools gated by `OrchardCore.DataPipelines.Core` so the Reports area can use them without the Data Pipelines feature?
