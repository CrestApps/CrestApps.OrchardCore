---
sidebar_label: Automatic Follow-ups
title: Follow Up Automatically
description: Nudge customers who stop replying to an automated text conversation with a series of follow-up messages, sent only during your business hours.
technical_manual:
  - omnichannel/cadences
---

## Who it's for and what you get

For **managers** who run automated text outreach and do not want conversations to die when a customer goes quiet.

When it is done:

- the AI texts your contacts and holds the conversation;
- when a customer stops replying, a **cadence** sends the next follow-up after the silence you chose, for example after an hour and again after a day;
- each follow-up is your own fixed text or written by the AI from the conversation;
- follow-ups go out only while your business hours calendar is open, in the customer's own time zone;
- a customer who replies, or texts STOP, is never nudged again.

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a cadence with a defined message step and an AI-generated step">
  <source src="/img/docs/um-cadences.mp4" type="video/mp4" />
</video>

## Before you start

Cadences follow up **automated** text conversations: the ones the AI starts from an **Automatic** activity load. They do not send messages in conversations agents have in the messaging workspace.

| You need | Who sets it up |
| --- | --- |
| An SMS provider and a texting number | Your administrator; see [Phone and SMS setup](../telephony-settings.md) |
| An AI provider connection the profile can use | Your administrator or AI content manager; see [AI connections](../ai/connections.md) |
| The feature **SMS Omnichannel Automation**, which also turns on Omnichannel Management and Contact Center Business Hours | Your administrator, in **Tools > Features** |
| A manager role with Manage cadences, Manage Contact Center business hours, Manage activity batches, Manage subject flows and Manage dispositions, plus access to AI profiles | Your administrator; see [Roles and permissions](../getting-started/roles-and-permissions.md) |

<AskYourAdmin />

## Steps

1. **Create the AI profile.** Under **Artificial Intelligence > Profiles**, click **Add Profile** and pick a **Text messaging** starting point such as **Qualify leads by text** or **Customer care by text**. It fills in the opening message and the instructions. Make sure **Start the conversation automatically** is on. See [AI profiles](../ai/profiles.md) and [Automated AI SMS and voice](../automated-ai.md).
2. **Set up the subject and its outcomes.** Create an outbound subject, its dispositions, and a subject flow. On each flow action, fill in **When to choose this disposition** so the AI knows which outcome to pick. See [Subjects](../subjects.md), [Dispositions](../dispositions.md) and [Subject flows](../subject-flows.md).
3. **Add the texting number.** The number the AI sends from must be on **Omnichannel Addresses** with **Text messages (SMS)** ticked. See [Omnichannel addresses](../channel-endpoints.md).
4. **Set your business hours.** Create the calendar that says when follow-ups may be sent. See [Business hours](../business-hours.md).
5. **Build the cadence.** Under **Interaction Center > Management > Cadences**, add a step for each follow-up, with how many minutes of silence come first, and a **Defined message** or **AI-generated** text. See [Cadences](../cadences.md).
6. **Load the conversations.** Create an **Automatic** activity load with the subject, the campaign, the AI profile, **SMS** as the channel and your texting number. Under the AI options, pick the cadence as **Re-engagement**, pick the **Business hours** calendar, and choose a reply delay. Filter the contacts, save, and choose **Actions > Load activities**. See [Automated AI SMS and voice](../automated-ai.md) and [Load activities](../load-inventory.md).
7. **Review the results.** Each finished conversation is completed with a disposition and a summary. Open **Review AI conversation** on the contact's activity to read it. See [Automated AI SMS and voice](../automated-ai.md).

## Check that it works

1. Make a test contact with your own mobile number, and load a small automatic batch for just that contact, with a cadence whose first step is a few minutes.
2. Within about five minutes, your phone receives the opening message.
3. Do not reply. After the first step's minutes, while the calendar is open, the follow-up arrives.
4. Reply to the follow-up. The AI answers, and because you replied, no more follow-ups are sent.
5. Load another test batch for yourself and text **STOP**: no follow-up is ever sent.

## Tips

- The number of steps is the most follow-ups a customer will ever get. After the last step, the conversation ends on its normal no-response timeout.
- Each step's minutes are counted from the last message the automation sent, not from the start of the conversation.
- The cadence is copied onto each activity when the load runs, so editing the cadence later does not change conversations already in progress.
- Use **AI guidance** on an AI-generated step to steer its tone, for example *keep it short and friendly, and offer a call back*.
- A customer who asks for a person can be handed to an agent, if the subject allows it. See [Let the AI answer your customers](ai-answers-customers.md).
