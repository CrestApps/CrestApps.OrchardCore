namespace CrestApps.OrchardCore.Tickets;

/// <summary>
/// Names shared by the Tickets module and the modules that build on it.
/// </summary>
public static class TicketsConstants
{
    /// <summary>
    /// The record kind of a ticket in a <c>RecordRef</c>: its timeline, files, service-level timers and links.
    /// </summary>
    public const string RecordKind = "Ticket";

    /// <summary>
    /// The scope of ticket service-level policies.
    /// </summary>
    public const string ServiceLevelScope = "Tickets";

    /// <summary>
    /// The YesSql collection that holds the ticket settings records.
    /// </summary>
    public const string CollectionName = "Tickets";

    /// <summary>
    /// The stereotype of a ticket content type.
    /// </summary>
    public const string Stereotype = "Ticket";

    /// <summary>
    /// The content part every ticket type carries.
    /// </summary>
    public const string PartName = "TicketPart";

    /// <summary>
    /// The default ticket content type.
    /// </summary>
    public const string DefaultContentType = "Ticket";

    /// <summary>
    /// The group of the tickets' kinds of e-mail and their templates.
    /// </summary>
    public const string EmailGroup = "Tickets";

    /// <summary>
    /// The scope of reply templates offered on tickets.
    /// </summary>
    public const string ReplyTemplateScope = "Tickets";

    /// <summary>
    /// The timeline entry kinds of a ticket.
    /// </summary>
    public static class EntryKinds
    {
        /// <summary>
        /// A public reply, from an agent or the requester.
        /// </summary>
        public const string Reply = "Reply";

        /// <summary>
        /// An internal note.
        /// </summary>
        public const string Note = "Note";

        /// <summary>
        /// Something that happened, such as a change of status.
        /// </summary>
        public const string Event = "Event";
    }

    /// <summary>
    /// The recipe and deployment step names of the ticket settings.
    /// </summary>
    public static class RecipeSteps
    {
        /// <summary>
        /// The ticket statuses.
        /// </summary>
        public const string Statuses = "TicketStatuses";

        /// <summary>
        /// The ticket priorities.
        /// </summary>
        public const string Priorities = "TicketPriorities";

        /// <summary>
        /// The desks.
        /// </summary>
        public const string Desks = "TicketDesks";

        /// <summary>
        /// The saved views.
        /// </summary>
        public const string Views = "TicketViews";
    }

    /// <summary>
    /// The channels a ticket or a reply comes from. The value is an open string; channel modules add their own.
    /// </summary>
    public static class Channels
    {
        /// <summary>
        /// Written by an agent on the ticket pages.
        /// </summary>
        public const string Agent = "Agent";

        /// <summary>
        /// Written by the requester on the portal.
        /// </summary>
        public const string Portal = "Portal";

        /// <summary>
        /// Sent through the API.
        /// </summary>
        public const string Api = "Api";
    }

    /// <summary>
    /// The display types of a ticket.
    /// </summary>
    public static class DisplayTypes
    {
        /// <summary>
        /// The ticket page of the admin: the drivers of <see cref="Models.TicketPart"/> and other parts add panels.
        /// </summary>
        public const string DetailAdmin = "DetailAdmin";
    }

    /// <summary>
    /// The feature identifiers of the Tickets module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Tickets feature.
        /// </summary>
        public const string Tickets = "CrestApps.OrchardCore.Tickets";
    }
}
