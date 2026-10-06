---
sidebar_label: AI Answers Customers
title: Let the AI Answer Your Customers
description: Have an AI agent answer the texts and calls customers send to your numbers, or reach out to a list itself, and hand the customer to a person when needed.
technical_manual:
  - telephony/telnyx
  - omnichannel/sms
  - ai/realtime-voice
---

## Who it's for and what you get

For **managers** and **AI content managers** who want an AI agent on the front line: answering your numbers around the clock, or working through a list of contacts.

When it is done:

- the AI answers every text or call to a number you choose, greets the customer, and holds the conversation;
- or the AI starts the conversations itself, texting or calling the contacts you load;
- at the end, the AI picks an outcome from your dispositions, saves a summary, and your subject flow runs, just as if an agent had completed the work;
- when the customer asks for a person, looks like a qualified lead, or is frustrated, the AI can hand them to an agent in a queue;
- you can read every conversation afterwards.

<video controls preload="metadata" width="100%" aria-label="Screencast of opening a contact's completed AI call and reading its transcript with Review AI conversation">
  <source src="/img/docs/um-review-ai.mp4" type="video/mp4" />
</video>

## Before you start

| You need | Who sets it up |
| --- | --- |
| An AI provider connection and a chat deployment | Your administrator or AI content manager; see [AI connections](../ai/connections.md) |
| For texts: an SMS provider and the features **SMS Omnichannel Automation** and **SMS Messaging Channel** | Your administrator |
| For calls: Telnyx as the phone provider and the feature **Telnyx AI Voice Agent**. For a live (realtime) spoken conversation, also **Contact Center Voice Media** and a realtime-capable deployment. | Your administrator |
| To answer your numbers: **Contact Center Inbound Entry Points**, and **Contact Center Inbound Voice** for calls | Your administrator |
| For hand-off to a person: **Contact Center Work Distribution** (queues), and the [messaging workspace](../messaging.md) for texts | Your administrator |
| A manager role with Manage Contact Center queues, Manage subject flows and Manage dispositions, plus access to AI profiles | Your administrator; see [Roles and permissions](../getting-started/roles-and-permissions.md) |

<AskYourAdmin />

## Steps

1. **Create the AI profile.** Under **Artificial Intelligence > Profiles**, click **Add Profile** and pick a starting point: **Answer calls at the front desk** for calls, **Customer care by text** for texts, or **Qualify leads by phone**, **Qualify leads by text** or **Confirm appointments by phone** for outreach. Name your business and fill in the **About the business** part of the instructions. See [AI profiles](../ai/profiles.md) and [Automated AI SMS and voice](../automated-ai.md).
2. **Teach it about your company (optional).** Attach documents or data sources so it answers from your own knowledge. See [Build an AI knowledge assistant](ai-knowledge-assistant.md).
3. **List the number.** The number must be on **Interaction Center > Management > Omnichannel Addresses**, with **Voice calls** or **Text messages (SMS)** ticked. See [Omnichannel addresses](../channel-endpoints.md).
4. **Set the outcomes and the hand-off.** Give the number an **Inbound**, **Automated** subject with the **Phone** or **SMS** channel, whose **Channel endpoint** is that number, with dispositions and a subject flow. Fill in **When to choose this disposition** on each action. To allow hand-off, turn on **Allow the AI to hand off to a live agent**, pick the **Handoff queue**, and choose **Escalate when**. Without a subject, the AI still answers texts, but cannot pick an outcome or hand over. See [Subjects](../subjects.md) and [Subject flows](../subject-flows.md).
5. **Staff the hand-off queue.** Make sure agents work the handoff queue: calls ring an agent in the queue, and texts appear in the messaging workspace. See [Queues](../queues.md) and [Messaging workspace](../messaging.md).
6. **Point the number at the AI.** Under **Interaction Center > Management > Inbound entry points**, edit the number's **Voice calls** or **Text messages** entry point. On **Routing**, set **Route to** to **AI voice agent** (calls) or **AI agent** (texts), and pick the profile under **AI agent**. Pick a business hours calendar if the AI should only answer at certain times. See [Inbound entry points and IVR menus](../entry-points-and-ivr.md) and [Automated AI SMS and voice](../automated-ai.md).
7. **Or let the AI reach out.** To have the AI start the conversations instead, create an **Automatic** activity load with the profile, the channel and the number, and load it. Add a cadence to follow up on silence. See [Automated AI SMS and voice](../automated-ai.md) and [Follow up automatically](automatic-follow-ups.md).
8. **Review.** Open a contact's **List Activities** and click **Review AI conversation** on a completed AI activity to read the transcript. The **AI handoff & containment** report shows how often the AI handed a conversation to a person. See [Automated AI SMS and voice](../automated-ai.md) and [Reports](../reports.md).

## Check that it works

1. Call or text the number from your own phone while the entry point is open. The AI greets you, or replies to your text after its usual pause.
2. Ask a question about your business, and check the answer.
3. Ask to speak to a person. If hand-off is allowed, a text conversation appears in the messaging workspace, or the call rings an agent in the handoff queue.
4. End the conversation, then open your contact's **List Activities** and read it with **Review AI conversation**.

## Tips

- An entry point that is **closed** does not use the AI: calls go to voicemail (or are refused with **Reject**), and texts go to people with the closed auto-reply.
- A text goes to people instead of the AI when a person already has an open conversation with the customer on that number, or when the customer's last AI conversation there ended less than an hour ago.
- AI-answered text conversations do not appear in the messaging workspace unless the AI hands them over.
- If the AI voice feature is turned off after an entry point points at the AI, its calls are refused until you pick another target.
- Start with a test number and a few colleagues before you point your main number at the AI.
