using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// What the automated (AI) conversation engine needs to know about one messaging channel: how its inbound messages are
/// announced, how its addresses are written, how a contact opts out of it, how quickly a reply should come, and how an
/// opening message is shaped. Everything else (the reply loop, the should-respond gate, the conclusion, the handoff, the
/// entry-point starter, the owed-reply recovery and the cadence follow-ups) is shared. Messages leave through the
/// channel's own <c>IMessagingChannel</c>.
/// </summary>
/// <remarks>
/// A channel is registered with <c>services.AddAutomatedMessagingChannel&lt;TChannel&gt;()</c> from the feature that
/// automates it.
/// </remarks>
public interface IAutomatedMessagingChannel
{
    /// <summary>
    /// Gets the channel's technical name, which is the messaging channel's name, such as <c>Email</c>.
    /// </summary>
    string Channel { get; }

    /// <summary>
    /// Gets the Omnichannel event type raised for an inbound message on the channel, such as <c>EmailReceived</c>.
    /// </summary>
    string ReceivedEventType { get; }

    /// <summary>
    /// Gets the kind of activity an automated conversation on the channel is.
    /// </summary>
    ActivityKind ActivityKind { get; }

    /// <summary>
    /// Gets the AI usage category the channel's requests are recorded under.
    /// </summary>
    string UsageCategory { get; }

    /// <summary>
    /// Gets the identifier of the prompt template the conclusion analysis is run with.
    /// </summary>
    string ConclusionPromptTemplateId { get; }

    /// <summary>
    /// Gets how the channel's conversation is named in the instructions given to the model and in the log, such as
    /// <c>email conversation</c>.
    /// </summary>
    string ConversationNoun { get; }

    /// <summary>
    /// Brings an address into the channel's canonical form, as the messaging channel does.
    /// </summary>
    /// <param name="address">The address as received.</param>
    /// <returns>The canonical address.</returns>
    string NormalizeAddress(string address);

    /// <summary>
    /// Determines whether an inbound message must be left unanswered and unrecorded by the engine because a machine wrote
    /// it (an automatic reply, a bounce), so two automated systems cannot answer each other forever.
    /// </summary>
    /// <param name="message">The inbound message.</param>
    /// <returns><see langword="true"/> when the message is ignored.</returns>
    bool IsAutomaticMessage(OmnichannelMessage message);

    /// <summary>
    /// Determines whether an inbound message asks to stop being contacted on the channel.
    /// </summary>
    /// <param name="message">The inbound message.</param>
    /// <param name="flowSettings">The subject flow settings of the conversation, when it has a subject.</param>
    /// <returns><see langword="true"/> when the contact opts out.</returns>
    bool IsOptOutRequest(OmnichannelMessage message, SubjectFlowSettings flowSettings);

    /// <summary>
    /// Records on a contact that they opted out of the channel.
    /// </summary>
    /// <param name="contact">The contact.</param>
    /// <param name="utcNow">The current time.</param>
    void ApplyOptOut(ContentItem contact, DateTime utcNow);

    /// <summary>
    /// Gets the subject flow's own default reply delay for the channel, used when the activity was given none.
    /// </summary>
    /// <param name="flowSettings">The subject flow settings.</param>
    /// <returns>The delay, or <see langword="null"/> when the flow sets none for the channel.</returns>
    TimeSpan? GetDefaultResponseDelay(SubjectFlowSettings flowSettings);

    /// <summary>
    /// Gets how long to wait before composing a reply: a person reads before answering, and waiting gathers several quick
    /// messages into one reply.
    /// </summary>
    /// <param name="configuredDelay">The reply delay the activity or the flow configured, if any.</param>
    /// <param name="pendingCharacters">The length of the customer's unanswered messages.</param>
    /// <returns>The delay.</returns>
    TimeSpan GetReadingDelay(TimeSpan? configuredDelay, int pendingCharacters);

    /// <summary>
    /// Gets how long to wait before sending a composed reply, so it does not arrive faster than a person could write it.
    /// </summary>
    /// <param name="replyCharacters">The length of the reply.</param>
    /// <returns>The delay.</returns>
    TimeSpan GetTypingDelay(int replyCharacters);

    /// <summary>
    /// Splits a rendered opening message into the subject and the body, on a channel whose messages carry a subject.
    /// </summary>
    /// <param name="renderedMessage">The opening message as rendered from the profile.</param>
    /// <returns>The subject (or <see langword="null"/>) and the body.</returns>
    (string Subject, string Body) SplitOpeningMessage(string renderedMessage);
}
