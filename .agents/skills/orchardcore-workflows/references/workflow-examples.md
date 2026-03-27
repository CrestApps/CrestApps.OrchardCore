# Workflow Examples

## Example 1: Send Email on Content Published

A workflow that sends an email notification when a BlogPost is published.

### Workflow Structure

```
ContentPublishedEvent (BlogPost) → IfElseTask (check author) → SendEmailTask → NotifyTask
```

### Custom Activity Driver

```csharp
using OrchardCore.Workflows.Display;

public sealed class MyCustomTaskDisplayDriver : ActivityDisplayDriver<MyCustomTask>
{
    public override IDisplayResult Edit(MyCustomTask activity)
    {
        return Initialize<MyCustomTaskViewModel>("MyCustomTask_Edit", model =>
        {
            model.Message = activity.Message;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(MyCustomTask activity, IUpdateModel updater)
    {
        var model = new MyCustomTaskViewModel();
        await updater.TryUpdateModelAsync(model, Prefix);
        activity.Message = model.Message;
        return Edit(activity);
    }
}
```

## Example 2: Scheduled Cleanup Workflow

A workflow triggered by a timer that cleans up expired content.

### Timer Event Configuration

- **Name**: Content Cleanup
- **Trigger**: TimerEvent with cron `0 2 * * *` (daily at 2 AM)
- **Activities**:
  1. `TimerEvent` - Triggers daily.
  2. `ScriptTask` - Queries for expired content items.
  3. `ForLoopTask` - Iterates over expired items.
  4. `ContentDeleteTask` - Deletes each expired item.
  5. `LogTask` - Logs the cleanup summary.
