---
sidebar_label: Automated AI SMS & Voice
sidebar_position: 18
title: Automated AI SMS and Voice Campaigns
description: Set up an AI profile and an automatic activity load so the AI texts or calls your contacts, follows up, picks the disposition, and hands off to a person when needed.
technical_manual:
  - omnichannel/sms
  - telephony/telnyx
  - ai/realtime-voice
  - ai/profile-templates
  - omnichannel/management
---

<YouTube video="contact-center-ai-outbound-calls" title="Video overview of AI outbound calls: the profile, the subject, the automatic load, screening, what the agent does and how to review it" />

This page is about conversations the AI starts. To have the AI answer the calls and texts customers send to your numbers, see [Let the AI answer incoming calls](#let-the-ai-answer-incoming-calls) and [Let the AI answer incoming texts](#let-the-ai-answer-incoming-texts).

An **automatic** activity load creates activities that an **AI profile** works by itself: it sends the opening text (or places the call), holds the conversation, and completes the activity with a disposition and a summary. If the subject allows it, the AI hands the customer to a live agent.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Load Activities > Add Activity Load > Automatic |
| **Permission** | Manage activity batches |
| **Features** | **SMS Omnichannel Automation** for texts; **Telnyx AI Voice Agent** (which turns on **Automated Voice**) for calls. Both turn on the AI features they need; an AI provider connection must also be set up. |

<YouTube video="omnichannel-automated-sms" title="Video overview of automated SMS: the AI profile, the automatic load, opt-outs and the text entry point" />

<AskYourAdmin />

## Before you start

1. **An AI profile.** Under **Artificial Intelligence > Profiles**, create a profile of type **Chat** with **Start the conversation automatically** turned on. The **Opening message** is the first message the customer receives. It can use Liquid, for example `{{ Contact.DisplayText }}`. The quickest way is **Add Profile** and one of the **Text messaging** or **Phone calls** starting points (**Qualify leads by text**, **Customer care by text**, **Answer calls at the front desk**, **Qualify leads by phone**, **Confirm appointments by phone**), which fill in the opening message and the instructions for you. Then name your business in the opening message and fill in the **About the business** section of the system prompt. See [Text messaging and phone call starting points](../ai/profile-templates.md#text-messaging-and-phone-call-starting-points).
2. **A subject and its flow**, with the dispositions the AI may choose and a **When to choose this disposition** hint on each action. See [Subject flows](subject-flows.md).
3. **An omnichannel address** for the number you send from or call from, with **Text messages (SMS)** or **Voice calls** ticked. See [Omnichannel Addresses](channel-endpoints.md).
4. **The SMS provider connected to the site**, for texts. Your administrator points the provider's incoming-message webhook at the site, so that customers' replies reach the AI. For Twilio, the address to give Twilio shows as **Webhook URL** on the Twilio tab of **Settings > Communication > SMS**. See the [technical manual](../omnichannel/sms.md).
5. **Optionally a cadence** for follow-ups ([Cadences](cadences.md)) and a **business hours** calendar ([Business hours](business-hours.md)).

## Load automated SMS activities

<YouTube video="contact-center-setup-walkthroughs" title="Video of creating an automatic SMS activity load driven by an AI profile and loading it to generate automated activities" start={1231} end={1290} />

1. Open **Interaction Center > Management > Load Activities**, click **Add Activity Load** and choose **Automatic**.
2. Enter a **Title**, pick the **Subject content type** and the **Campaign**.
3. Pick the **AI profile**. An outbound subject cannot hold a profile, so the load must name one. An inbound subject can, and then the field may stay empty to use the subject's profile.
4. Pick **SMS** as the **Channel**, and the **Address** to send from. Only addresses used for text messages are listed.
5. Set the AI options (below), the record filters, and **Save**. For a lead type you can also let the AI convert the leads it qualifies; see [Let the AI convert leads](leads-accounts-opportunities.md#let-the-ai-convert-leads).
6. Choose **Actions > Load activities**.

A background task picks up due automated activities every five minutes and sends the opening message. Replies are answered by the AI after the reply delay. Each time the task starts a text or a call, it checks the contact's opt-out first. A workflow that starts the conversation with the **Place Call or Send Message** task begins it at once and runs none of these checks; see [Workflows](workflows.md).

While the conversation runs:

- When the customer texts an opt-out keyword, such as *STOP*, the activity is cancelled and the contact is marked **Do not SMS**, even when the AI is not allowed to update the contact. The keywords are set on the [subject](subjects.md#subject-settings).
- When the subject has a **No-response timeout (minutes)** and the customer does not answer in time, the activity is marked failed.
- The reply delay chosen on the load wins. The subject's **Response delay (seconds)** applies only when the load has none. Replies are paced naturally on top of it.

## Load automated voice calls

<YouTube video="contact-center-setup-walkthroughs" title="Video of creating an automatic voice activity load with an AI profile, background office sound and reply delay" start={1293} end={1348} />

<YouTube video="contact-center-ai-outbound-calls" title="Video of creating the AI profile, the outbound subject and the automatic voice load, and reviewing the conversation" start={63} end={229} />

Follow the same steps with **Phone** as the channel and an address used for voice calls. The AI places each call, talks with the person who answers, and hangs up when the conversation is done. An AI call is not a dialer mode and uses no dialer profile; it is an automated activity. Only the **Telnyx** provider can place it: the Asterisk provider cannot place or answer AI calls. If the **Address** is left empty, the call shows the **Default outbound caller ID** of the Telnyx settings.

The **Phone calls** starting points of **Add Profile** (**Answer calls at the front desk**, **Confirm appointments by phone** and **Qualify leads by phone**) need the **Automated Voice** feature, which the **Telnyx AI Voice Agent** feature turns on. The load refuses a profile that does not have **Start the conversation automatically** turned on.

An outbound subject shows only the **Live agent handoff** card (**Allow the AI to hand off to a live agent**, **Handoff queue** and **Escalate when**): the speech and voice settings belong to inbound subjects. A call is handed over only when the card is ticked, a queue is chosen and at least one **Escalate when** reason is ticked. The agent who takes it sees the **AI assistant's summary**.

### When the call starts and what is checked

The background task runs every five minutes, so the first call can start up to five minutes after the load. Each time it starts an activity it checks, in this order, that the number is not known to be out of service (the activity is *Cancelled*), that the contact has not asked not to be reached on the channel, and then the screeners the site has: the do-not-call lists, which need the **Contact Center Outbound Dialer** feature, and a calling window, which is checked only when your administrator has turned it on for automated calls. The first reason that refuses a call is written to the activity's notes, and the activity is *Cancelled*.

### Before and after the call

The recording notice: under **Settings > Contact Center > Recording governance** (which needs **Contact Center Call Recording**), **Tell callers the call is recorded** sets the text, and **Have automated voice agents say it first** makes the AI say it before anything else. A turn-based call speaks it before the profile's opening message; in a live conversation the model is told to say it and the platform checks that it did.

To review a finished call, open the contact's activities. **Dispositioned by** reads *AI voice agent (profile: ...)*, and the disposition and notes hold the AI's summary. **Review AI conversation** opens the transcript read only. It appears when the activity has an AI conversation, and it needs the **AI Chat** feature and permission to query AI profiles and to list activities.

### Phone menus, keys and call screening

On every AI phone call the AI can press keys. When a business answers with an automated menu, the AI listens to the options and presses the key, or enters the extension it was given, that gets it to the right person. When a phone screens the call by asking who is calling and why, the AI says its name, the business and the reason in one sentence, and waits.

When the caller presses keys on their phone, the AI hears them as part of the conversation, for example when it asks them to enter an account number.

## When a voicemail answers

When a voicemail or answering machine answers, the AI does not talk to the recording. It lets the greeting and the tone finish, leaves one short message (who is calling, why, and that it will try again, with no questions) and hangs up. On a profile that holds the call as a live conversation, the message is spoken in the same voice and the same language as the rest of the call. The activity is completed with the disposition for the *Answering machine* outcome when the subject flow has one (see [Dispositions](dispositions.md#outcomes)), and its notes say the call reached voicemail and left a message.

The AI is told who answered by your phone system's **Answering machine detection** setting (see [Telephony settings](telephony-settings.md)). With it off, the AI has to recognise the greeting for what it is before it leaves its message, so turn detection on for campaigns that reach many voicemails.

### When the line goes quiet

When nobody speaks for 8 seconds, the AI asks whether the person is still there. It asks twice, 8 seconds apart, and after another 8 seconds of silence it says goodbye and hangs up, about half a minute after its last word. Anything the person says starts the count again. Calls the AI answers work the same way, with one difference: on a call the platform placed, silence from a person who has never spoken is treated as a voicemail, and the AI leaves its message instead of asking.

## Let the AI answer incoming calls

An automatic load is for conversations the AI starts. To have an AI profile **answer** the calls customers make to one of your numbers, set it on the number's **inbound entry point**, not on a queue. A queue only holds callers for people.

1. Make the profile under **Artificial Intelligence > Profiles**. **Add Profile** with the **Answer calls at the front desk** starting point fills in the greeting and the instructions. Every **Chat** profile can be picked.
2. Open **Interaction Center > Management > Inbound entry points** and edit (or add) the **Voice calls** entry point that picks the number under **Numbers**.
3. On the **Routing** card, set **Route to** to **AI voice agent** and choose the profile under **AI agent**. Click **Save**.
4. Optionally, give the number an inbound **Phone** [subject](subjects.md) whose **Address** is that number. Its subject, campaign and [subject flow](subject-flows.md) are used for the call: the dispositions the AI picks from, and whether it may hand the caller to a person.

While the entry point is open, the AI picks up every call to the number, greets the caller with the profile's opening message and holds the conversation. The welcome message, phone menu and queue are not used. While the entry point is closed, callers go to voicemail, or are refused when its closed action is **Reject**. Each call is an automated activity with the AI's transcript. It reaches an agent only if the AI hands the caller over. See [AI voice agent](entry-points-and-ivr.md#ai-voice-agent).

A caller who says nothing at first is never taken for a voicemail, as a silent line on a call the AI placed can be: only a call the platform places can reach one.

This needs the **Telnyx AI Voice Agent** feature, which adds **AI voice agent** to the **Route to** list.

## Let the AI answer incoming texts

Texts work the same way as calls. Set the AI profile on the number's **text entry point**, and the AI takes the customer's first text itself.

1. Make the profile under **Artificial Intelligence > Profiles**. **Add Profile** with the **Customer care by text** starting point fills in the instructions. Every **Chat** profile can be picked. The AI replies to the customer, so its opening message is not used.
2. Open **Interaction Center > Management > Inbound entry points** and edit (or add) the **Text messages** entry point that picks the number under **Numbers**.
3. On the **Routing** card, set **Route to** to **AI agent** and choose the profile under **AI agent**. Click **Save**.
4. Optionally, give the number an inbound **SMS** [subject](subjects.md) whose **Address** is that number. Its subject, campaign and [subject flow](subject-flows.md) are used for the conversation: the dispositions the AI picks from, the no-response timeout, and whether it may hand the customer to a person. Without one, the AI still answers, but it has no dispositions to choose from and cannot hand the conversation over.

While the entry point is open, the first text from a customer starts an automated conversation. The AI replies after its usual pause and answers each later text until the conversation ends. The entry point's auto-reply is not sent. The conversation does not appear in the messaging workspace unless the AI hands it to a person.

A text goes to people instead, in the shared inbox and with the entry point's auto-reply, when:

- the entry point is closed (its closed auto-reply is sent);
- a person already has an open conversation with the customer on that number;
- the customer's last AI conversation on that number ended less than an hour ago, so a "thanks" after the goodbye does not start a new one;
- the chosen AI profile was deleted.

This needs the **SMS Omnichannel Automation** and **SMS Messaging Channel** features, which add **AI agent** to the **Route to** list of text entry points.

## AI options on an automatic load

| Field | What it does |
| --- | --- |
| **AI profile** | The profile that runs the conversation. Only chat profiles with **Start the conversation automatically** turned on are listed. It is saved on each activity. Leave it empty only when an inbound subject names one; an outbound subject cannot. |
| **Allow AI to update the contact or lead** / **Allow AI to update the subject** | Lets the AI save what it learns onto the records, for example a corrected phone number or email. |
| **Play background office sound on calls** | Voice only: a quiet office ambience (faint room tone and typing) behind the AI voice, instead of a silent line. It applies only to calls that use a realtime voice profile. |
| **Reply delay** | **No delay**, **Fixed** (wait **Delay (seconds)**), or **Random (base ± jitter)** (wait a random time between **Base (seconds)** minus and plus **Jitter ± (seconds)**), so replies do not feel instant. |
| **Re-engagement** | The [cadence](cadences.md) that follows up when the customer goes quiet, or *No follow-up cadence*. |
| **Business hours** | Shown when at least one [business hours](business-hours.md) calendar exists. Follow-ups are only sent while this calendar is open, in the customer's time zone. A reply to a customer who is texting right now is always sent. **Always open (no restriction)** sends follow-ups at any time. |

## When the conversation ends

The AI picks a disposition using your hints and what the customer said, the summary is saved as the activity's notes, and the subject flow runs exactly as if an agent had completed it. When the customer never spoke, or decided nothing your dispositions record, no disposition is picked for them: the call takes the one for its outcome, such as the one wired to **Try Again** so the contact is called again, or the one for *No answer* or *Disconnected* (see [Dispositions](dispositions.md#outcomes)). Open **Review AI conversation** on the activity to read the full transcript. When the profile's [AI persona](../ai/personas.md) has a **Language**, the summary is written in that language, and so is the briefing an agent reads when a call is handed to them.

To find it, open the contact from **Interaction Center > Contacts**, click **List Activities**, and look under **Completed Activities**: each completed AI activity shows its disposition, channel and summary, with a **Review AI conversation** button. The transcript is read-only: the page says the conversation was handled automatically, and there is no reply box. The screencast opens a completed AI phone call and reads the whole conversation:

<YouTube video="contact-center-agent-walkthroughs" title="Video of opening a contact's completed AI call and reading its transcript with Review AI conversation" start={695} end={717} />

### When an AI call is cut off

If the AI's live voice connection drops in the middle of a call, the platform first tries to reconnect, up to two times, and the AI carries on with the conversation so far. If that does not work, the caller is handed to a live agent when the subject allows it. Otherwise the caller hears a short apology and the call ends. The activity then takes the disposition the subject's **Try Again** action is wired to, so the contact is called again, and its notes say the conversation was cut short. A subject with no **Try Again** action keeps the disposition the AI picked.

### Live agent handoff

<YouTube video="omnichannel-ai-handoff" title="Video overview of live agent handoff from an AI conversation" />

If the subject allows **live agent handoff**, the AI can pass the customer to the handoff queue when they ask for a person, look like a qualified lead, or are frustrated. SMS conversations appear in the [Messaging workspace](messaging.md); calls ring an agent in the queue.

When the handoff queue is closed (outside its business hours) as the call begins, the AI tells a caller who asks for a person that nobody can take the call now. With paced dialing on, the transfer then books a callback for when the queue opens, and the AI says someone will call back; without it, nothing could be booked, so the AI is not offered the transfer and offers to take a message instead. Otherwise, on a call, the AI tells the caller in its own words that it is connecting them. When nobody in the queue is free, the caller then hears that they are in the queue; when the queue is closed and a callback is booked instead, they hear that someone will call them back, and the call ends. Those two lines are English, read by the telephony provider's voice, so they are said only when the profile's [AI persona](../ai/personas.md) speaks English or names no language; on a call held in another language the caller hears only the AI's own words.
