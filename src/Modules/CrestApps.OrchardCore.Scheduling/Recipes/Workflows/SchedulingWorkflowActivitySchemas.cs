using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Scheduling.Recipes.Workflows;

/// <summary>
/// Describes the filter every appointment workflow event shares.
/// </summary>
internal abstract class AppointmentEventActivitySchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Scheduling";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("AppointmentTypeIds", WorkflowActivitySchemaBuilders.StringArray("The identifiers of the appointment types whose appointments start the workflow. Empty for every type."));
    }
}

/// <summary>
/// Describes the properties every appointment workflow task shares.
/// </summary>
internal abstract class AppointmentTaskActivitySchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Scheduling";

    protected virtual string DefaultOutputName => "Appointment";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        foreach (var property in GetTaskProperties())
        {
            yield return property;
        }

        yield return ("OutputName", WorkflowActivitySchemaBuilders.String(DefaultOutputName is null
            ? "Not used by this task, which writes no output."
            : $"The name of the workflow output that receives the result. Defaults to '{DefaultOutputName}'. The result is also the workflow's last result."));
    }

    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties();

    protected static JsonSchemaBuilder Text(string description)
        => WorkflowActivitySchemaBuilders.String(description + " A plain string that supports Liquid.");

    protected static JsonSchemaBuilder Time(string description)
        => Text(description + " An ISO 8601 time, read as UTC when it has no offset.");
}

internal sealed class AppointmentRequestedEventSchema : AppointmentEventActivitySchemaBase
{
    public override string Name => "AppointmentRequestedEvent";

    protected override string DisplayText => "Appointment Requested";

    protected override string Description => "Starts when an appointment is booked and waits for someone to confirm it";
}

internal sealed class AppointmentConfirmedEventSchema : AppointmentEventActivitySchemaBase
{
    public override string Name => "AppointmentConfirmedEvent";

    protected override string DisplayText => "Appointment Confirmed";

    protected override string Description => "Starts when an appointment is confirmed";
}

internal sealed class AppointmentRescheduledEventSchema : AppointmentEventActivitySchemaBase
{
    public override string Name => "AppointmentRescheduledEvent";

    protected override string DisplayText => "Appointment Rescheduled";

    protected override string Description => "Starts when an appointment moves to another time";
}

internal sealed class AppointmentCancelledEventSchema : AppointmentEventActivitySchemaBase
{
    public override string Name => "AppointmentCancelledEvent";

    protected override string DisplayText => "Appointment Cancelled";

    protected override string Description => "Starts when an appointment is cancelled";
}

internal sealed class AppointmentCompletedEventSchema : AppointmentEventActivitySchemaBase
{
    public override string Name => "AppointmentCompletedEvent";

    protected override string DisplayText => "Appointment Completed";

    protected override string Description => "Starts when an appointment is marked completed";
}

internal sealed class AppointmentNoShowEventSchema : AppointmentEventActivitySchemaBase
{
    public override string Name => "AppointmentNoShowEvent";

    protected override string DisplayText => "Appointment Marked No-Show";

    protected override string Description => "Starts when the customer of an appointment is marked as not having come";
}

internal sealed class AppointmentHoldExpiredEventSchema : AppointmentEventActivitySchemaBase
{
    public override string Name => "AppointmentHoldExpiredEvent";

    protected override string DisplayText => "Reservation Hold Expired";

    protected override string Description => "Starts when a held time is let go because the booking wasn't finished in time";
}

internal sealed class WaitlistSlotAvailableEventSchema : AppointmentEventActivitySchemaBase
{
    public override string Name => "WaitlistSlotAvailableEvent";

    protected override string DisplayText => "Waitlist Slot Available";

    protected override string Description => "Starts when a time opens that a waitlist entry waits for";
}

internal sealed class AppointmentStartingSoonEventSchema : AppointmentEventActivitySchemaBase
{
    public override string Name => "AppointmentStartingSoonEvent";

    protected override string DisplayText => "Appointment Starting Soon";

    protected override string Description => "Starts a set number of minutes before an appointment. Needs the Calendar Reminders feature";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        foreach (var property in base.GetPropertyDefinitions(context))
        {
            yield return property;
        }

        yield return ("MinutesBefore", WorkflowActivitySchemaBuilders.Integer("How many minutes before the start of the appointment the workflow starts, from 0 to 43200. Defaults to 60.").Minimum(0).Maximum(43200));
    }
}

internal sealed class FindAppointmentSlotsTaskSchema : AppointmentTaskActivitySchemaBase
{
    public override string Name => "FindAppointmentSlotsTask";

    protected override string DisplayText => "Find Appointment Slots";

    protected override string Description => "Lists up to 100 free times of an appointment type between two times";

    protected override IEnumerable<string> Outcomes => ["Found", "None"];

    protected override IEnumerable<string> RequiredProperties => ["AppointmentTypeId", "From", "To"];

    protected override string DefaultOutputName => "AppointmentSlots";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("AppointmentTypeId", Text("The identifier of the appointment type."));
        yield return ("From", Time("The start of the range."));
        yield return ("To", Time("The end of the range."));
    }
}

internal sealed class BookAppointmentTaskSchema : AppointmentTaskActivitySchemaBase
{
    public override string Name => "BookAppointmentTask";

    protected override string DisplayText => "Book Appointment";

    protected override string Description => "Books an appointment of a type at a time. Taken when the time is no longer free";

    protected override IEnumerable<string> Outcomes => ["Done", "Taken", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["AppointmentTypeId", "Start"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("AppointmentTypeId", Text("The identifier of the appointment type."));
        yield return ("Start", Time("When the appointment starts."));
        yield return ("CustomerName", Text("The customer's name."));
        yield return ("CustomerEmail", Text("The customer's email."));
        yield return ("CustomerPhone", Text("The customer's phone."));
        yield return ("Notes", Text("Notes kept with the appointment."));
    }
}

internal sealed class ConfirmAppointmentTaskSchema : AppointmentTaskActivitySchemaBase
{
    public override string Name => "ConfirmAppointmentTask";

    protected override string DisplayText => "Confirm Appointment";

    protected override string Description => "Confirms a requested appointment";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["AppointmentId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("AppointmentId", Text("The identifier of the appointment."));
    }
}

internal sealed class RescheduleAppointmentTaskSchema : AppointmentTaskActivitySchemaBase
{
    public override string Name => "RescheduleAppointmentTask";

    protected override string DisplayText => "Reschedule Appointment";

    protected override string Description => "Moves an appointment to another time. Taken when the time is no longer free";

    protected override IEnumerable<string> Outcomes => ["Done", "Taken", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["AppointmentId", "Start"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("AppointmentId", Text("The identifier of the appointment."));
        yield return ("Start", Time("The new start of the appointment."));
    }
}

internal sealed class CancelAppointmentTaskSchema : AppointmentTaskActivitySchemaBase
{
    public override string Name => "CancelAppointmentTask";

    protected override string DisplayText => "Cancel Appointment";

    protected override string Description => "Cancels an appointment";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["AppointmentId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("AppointmentId", Text("The identifier of the appointment."));
        yield return ("Reason", Text("Why the appointment is cancelled."));
    }
}

internal sealed class GetAppointmentTaskSchema : AppointmentTaskActivitySchemaBase
{
    public override string Name => "GetAppointmentTask";

    protected override string DisplayText => "Get Appointment";

    protected override string Description => "Reads an appointment into the workflow output";

    protected override IEnumerable<string> Outcomes => ["Found", "NotFound"];

    protected override IEnumerable<string> RequiredProperties => ["AppointmentId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("AppointmentId", Text("The identifier of the appointment."));
    }
}

internal sealed class AddToWaitlistTaskSchema : AppointmentTaskActivitySchemaBase
{
    public override string Name => "AddToWaitlistTask";

    protected override string DisplayText => "Add to Waitlist";

    protected override string Description => "Adds a customer to the waitlist of an appointment type for a range of times";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["AppointmentTypeId", "From", "To", "CustomerName"];

    protected override string DefaultOutputName => null;

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("AppointmentTypeId", Text("The identifier of the appointment type."));
        yield return ("From", Time("The start of the times the customer wants."));
        yield return ("To", Time("The end of the times the customer wants; after From."));
        yield return ("CustomerName", Text("The customer's name."));
        yield return ("CustomerEmail", Text("The customer's email."));
        yield return ("CustomerPhone", Text("The customer's phone."));
    }
}
