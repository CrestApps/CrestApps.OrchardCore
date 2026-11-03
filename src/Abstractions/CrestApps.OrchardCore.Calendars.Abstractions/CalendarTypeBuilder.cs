using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Configures a <see cref="CalendarTypeDescriptor"/> fluently.
/// </summary>
public sealed class CalendarTypeBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarTypeBuilder"/> class.
    /// </summary>
    /// <param name="descriptor">The descriptor to configure.</param>
    public CalendarTypeBuilder(CalendarTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        Descriptor = descriptor;
    }

    /// <summary>
    /// Gets the descriptor being configured.
    /// </summary>
    public CalendarTypeDescriptor Descriptor { get; }

    /// <summary>
    /// Sets the name shown for the type.
    /// </summary>
    /// <param name="displayName">The name.</param>
    /// <returns>The builder.</returns>
    public CalendarTypeBuilder WithDisplayName(LocalizationSource displayName)
    {
        Descriptor.DisplayName = displayName;

        return this;
    }

    /// <summary>
    /// Sets what calendars of the type are for.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <returns>The builder.</returns>
    public CalendarTypeBuilder WithDescription(LocalizationSource description)
    {
        Descriptor.Description = description;

        return this;
    }

    /// <summary>
    /// Sets the Font Awesome classes of the type's icon.
    /// </summary>
    /// <param name="icon">The icon classes.</param>
    /// <returns>The builder.</returns>
    public CalendarTypeBuilder WithIcon(string icon)
    {
        Descriptor.Icon = icon;

        return this;
    }

    /// <summary>
    /// Sets the color new calendars of the type get.
    /// </summary>
    /// <param name="color">The CSS color.</param>
    /// <returns>The builder.</returns>
    public CalendarTypeBuilder WithColor(string color)
    {
        Descriptor.DefaultColor = color;

        return this;
    }

    /// <summary>
    /// Sets who calendars of the type belong to.
    /// </summary>
    /// <param name="ownerKind">The owner kind.</param>
    /// <param name="onePerOwner">Whether an owner may have only one calendar of the type.</param>
    /// <returns>The builder.</returns>
    public CalendarTypeBuilder WithOwner(CalendarOwnerKind ownerKind, bool onePerOwner = false)
    {
        Descriptor.OwnerKind = ownerKind;
        Descriptor.OnePerOwner = onePerOwner;

        return this;
    }

    /// <summary>
    /// Sets what calendars of the type support.
    /// </summary>
    /// <param name="capabilities">The capabilities.</param>
    /// <returns>The builder.</returns>
    public CalendarTypeBuilder WithCapabilities(CalendarCapabilities capabilities)
    {
        Descriptor.Capabilities = capabilities;

        return this;
    }

    /// <summary>
    /// Sets the visibility new calendars of the type get.
    /// </summary>
    /// <param name="visibility">The visibility.</param>
    /// <returns>The builder.</returns>
    public CalendarTypeBuilder WithVisibility(CalendarVisibility visibility)
    {
        Descriptor.DefaultVisibility = visibility;

        return this;
    }

    /// <summary>
    /// Limits how many busy events may overlap on new calendars of the type.
    /// </summary>
    /// <param name="capacity">How many busy events may overlap; 1 for a room.</param>
    /// <returns>The builder.</returns>
    public CalendarTypeBuilder LimitConcurrent(int capacity = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        Descriptor.DefaultConflictPolicy = CalendarConflictPolicy.LimitConcurrent;
        Descriptor.DefaultCapacity = capacity;

        return this;
    }

    /// <summary>
    /// Keeps people from creating calendars of the type in the admin, for a type a module creates calendars of itself.
    /// </summary>
    /// <returns>The builder.</returns>
    public CalendarTypeBuilder ManagedByCode()
    {
        Descriptor.Creatable = false;

        return this;
    }

    /// <summary>
    /// Sets the order the type is listed in.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <returns>The builder.</returns>
    public CalendarTypeBuilder WithPosition(int position)
    {
        Descriptor.Position = position;

        return this;
    }

    /// <summary>
    /// Adds an event type calendars of the type hold.
    /// </summary>
    /// <param name="name">The technical name of the event type.</param>
    /// <param name="displayName">The name shown for the event type.</param>
    /// <param name="configure">Configures the event type.</param>
    /// <returns>The builder.</returns>
    public CalendarTypeBuilder AddEventType(string name, LocalizationSource displayName, Action<CalendarEventTypeBuilder> configure = null)
    {
        var eventType = new CalendarEventTypeDescriptor(name, displayName);

        configure?.Invoke(new CalendarEventTypeBuilder(eventType));
        Descriptor.AddEventType(eventType);

        return this;
    }
}
