---
sidebar_label: Automate AI Conversations
title: Automate AI Conversations with Workflows
description: Look the customer up before the AI answers, let the AI call your own workflows as tools, and act on what was said after a call, a text thread or a chat.
technical_manual:
  - ai/conversation-workflows
---

An AI conversation can be a website chat, a text thread or a phone call. Workflows can take part in all three, at three moments:

- **Before the first message.** Look the customer up, give the AI the facts it should know, choose which AI profile answers, or decide the conversation should not happen at all.
- **While the conversation is going on.** The AI calls one of your workflows as a tool, for example to check an order, book a time or text a link, and uses what the workflow answers.
- **After it ends.** Save what the AI learned on the contact, update another system, send a follow-up, or alert someone.

| | |
| --- | --- |
| **Menu** | Design > Workflows, and Artificial Intelligence > Tool Instances |
| **Permission** | Manage workflows, and Manage AI tools for tool instances |
| **Feature** | Workflows and AI Services. Calling a workflow as a tool also needs AI Tool Instances. |

<AskYourAdmin />

## Conversation variables

Every AI conversation keeps a set of named values, called **conversation variables**. A workflow can set them before the conversation starts or while it runs, and the AI captures some of them itself when the profile collects information (see **Enable data extraction** on the AI profile).

The AI profile's instructions can use them. Write a variable's name between double braces in the **System message**, such as `{{ Variables.first_name }}`, and the AI sees the value instead. For example, `You are talking to {{ Variables.first_name }}, a {{ Variables.tier }} customer.`

To include a sentence only when a value is set, put it between `{% if Variables.balance_due %}` and `{% endif %}`.

The instructions can also read the conversation itself:

| Write | To get |
| --- | --- |
| `{{ Conversation.Channel }}` | `Phone`, `Sms` or `Chat` |
| `{{ Conversation.Direction }}` | `Inbound` when the customer reached out, `Outbound` when you did |
| `{{ Conversation.CustomerAddress }}` | The customer's phone number |
| `{{ Contact.DisplayText }}` | The contact's name, when the customer is a known contact |
| `{{ Campaign.DisplayText }}` | The campaign of a call or text you started |

A message without any of these is used exactly as written.

## Run a workflow before the conversation starts

1. Open **Design > Workflows** and click **Create Workflow**.
2. Click **Add Event**, find **AI Conversation Starting** under **AI Conversations**, and click **Add**.
3. Optionally choose a **Channel** and a **Profile**, so the workflow only runs for phone calls, say, or for one AI profile. Click **Save**.
4. Add the tasks that look the customer up, for example an **HTTP Request** to your system, using `{{ Workflow.Input.CustomerAddress }}` or `{{ Workflow.Input.ContactId }}`.
5. Add one or more of these tasks, found under **AI Conversations**:

| Task | What it does |
| --- | --- |
| **Set Conversation Variables** | Sets variables the conversation starts with. Write one `name: value` per line, for example `tier: {{ Workflow.Output.Customer.tier }}`. |
| **Use AI Profile** | Answers with a different AI profile, for example a VIP assistant, or half of your callers with a new profile you are trying out. |
| **Decline Conversation** | Stops the conversation: a call or text you were about to send is cancelled with the reason you give, and an incoming call is refused. A website chat cannot be declined. |

The conversation waits for the workflow before its first message: up to 3 seconds when the customer called or texted, and up to 30 seconds for a call or text you start. Keep these workflows quick. If one takes longer, or fails, the conversation starts anyway with what was set by then.

:::tip[Choose who answers]
Combine **Use AI Profile** with an **If/Else** on the time of day, the caller's number or a value from your own system to send each caller to the right assistant.
:::

## Let the AI call a workflow as a tool

A **Workflow** tool instance gives the AI one of your workflows as a tool. The AI decides when to call it, from the tool's description.

First, create the workflow:

1. In **Design > Workflows**, create a workflow that starts with the **AI Tool Called** event (under **AI Conversations**).
2. Add the steps the tool needs. What the AI sent is in `Workflow.Input.Arguments`, for example `{{ Workflow.Input.Arguments.date }}`, and the conversation is in `Workflow.Input` as above.
3. End with **Set Output** for each value the AI should get back, for example an output named `slots` with the times you found. If the workflow sets no output, the AI is told the work was done.

Then create the tool:

1. Open **Artificial Intelligence > Tool Instances**, click **Add Tool Instance** and choose **Workflow**.
2. Give it a **Name** and a **Description**. The description is what the AI reads to decide when to call it, so say what it does and when to use it, for example *Finds free appointment times on a date the customer asks for*.
3. Choose the **Workflow**.
4. Optionally, set a **Time limit on calls**. On a phone call the AI waits this many seconds for the workflow, then tells the caller it could not get an answer. Leave it empty for 8 seconds, or set 0 to wait as long as the workflow takes.
5. Under **Parameters**, declare what the workflow needs, such as a `date`. For each one choose who fills it: the AI, a value you fix, or the conversation, such as **Customer phone or email** or a conversation variable (type `variables.` followed by its name). Without parameters, the AI sends a short description of what it needs as `request`.
6. Click **Save**, then add the tool instance to the AI profile under **Capabilities**.

The tool works in website chats, text threads and on phone calls. On a phone call, when any tool takes longer than a second and a half, the caller hears a short "One moment, please." so the silence is not taken for a dropped call, and a tool that takes longer than its time limit (8 seconds unless you set one) is stopped. A workflow that has to wait for something, such as a timer or someone's approval, keeps running after the AI moves on, and the AI tells the customer the request is in hand.

:::note[Inside a tool workflow]
**Set Conversation Variables** in a tool workflow changes the live conversation, so the AI's next answers see the new values.
:::

## Act on a live AI call

The **AI Call Action** task (under **AI Conversations**) acts on a phone call the AI is holding. Choose an **Action**:

| Action | **Value** |
| --- | --- |
| **Message the AI** | The note, for example *Offer the 10% retention discount*. The AI acts on it in its own words. |
| **Hand the call to an agent** | The agent's user name. The AI stops and the call rings that agent. |
| **Transfer the call to a phone number** | The number, with its country code. The AI stops and the caller is connected to whoever answers. |
| **Answer with another AI profile** | The profile's identifier. It answers from the caller's next turn, with the same transcript and variables. Turn-based calls only. |
| **End the call** | Nothing. |

**Call** says which call, and defaults to `{{ Workflow.Input.ActivityId }}`: the call the AI conversation event or the AI tool was raised for. So a workflow the AI calls as a tool can transfer its own call, and an **AI Conversation Turn** workflow can hand a caller who says *lawyer* to a person.

## React while the conversation is going on

The **AI Conversation Turn** event (under **AI Conversations**) starts a workflow after each turn is saved, on every channel, while the conversation is still going on. What was said is in `Workflow.Input.Text`, and who said it in `Workflow.Input.Role` (`Customer` or `Assistant`). Choose **What the customer says** to run only on the customer's turns.

For example, an **If/Else** on `{{ Workflow.Input.Text | downcase }}` containing *cancel* followed by **Notify** tells a supervisor, who can follow the call on the dashboard, message the AI or take the call over. These workflows run between turns, so keep them short: a slow one delays the AI's next reply.

## Act on a conversation after it ends

These events start a workflow when a conversation ends or the AI captures something. Each one passes the conversation's `Transcript` (the whole conversation as text, one `Customer:` or `Assistant:` line per turn), its `Variables`, the `Session` and the `Profile`.

| Event | When it runs |
| --- | --- |
| **AI Chat Session Field Extracted** | The AI captured a value the profile collects. The value is also a conversation variable from then on. |
| **AI Chat Session Closed** | The conversation ended. |
| **AI Chat Session Post-Processed** | The conversation's summary and analysis are ready, in `Workflow.Input.Results`. |

For phone calls and texts, the **Contact Center Event** also has **AI call answered**, **AI conversation ended** and **AI handoff requested**.

## Starting points

Each of these is a short workflow you build once.

**Save what the AI learned on the contact.** Start with **AI Chat Session Field Extracted**, then an **Update Content Item** task on the contact with the value in `{{ Workflow.Input.Value }}`.

**Remember each customer.** Start with **AI Chat Session Post-Processed**. Add **AI Completion using Profile** with a prompt such as *Merge these notes with this conversation, keep what is still open, in under 2,000 characters:* followed by the contact's notes field and `{{ Workflow.Input.Transcript }}`. Save the answer on the contact with **Update Content Item**. In the AI profile's system message, add `Notes about this customer: {{ Contact.Content.Customer.Notes.Text }}` (use your own part and field names).

**Update your CRM or another system.** Start with **AI Chat Session Post-Processed** and add an **HTTP Request** that sends the summary, the transcript and the variables to the other system.

**Post to Slack, Teams, Zapier, Make or n8n.** Add an **HTTP Request** to the incoming webhook address the service gives you, with a body such as `{"text": "{{ Workflow.Input.Results.summary.Value }}"}`.

**Start an AI call or text from another system.** Start with an **HTTP Request Event** and add **Create AI Activity** (under **AI Conversations**): choose the channel, the AI profile and the number it comes from, set **To** to the customer's number from the request, for example `{{ Workflow.Input.phone }}`, and give it the variables the conversation starts with. The call or text goes out on the next pass of the automated activities, after the usual opt-out, do-not-call and calling-hours checks. Put it inside a **For Each** to start a list of them.

**Follow up after a call.** Start with **Contact Center Event** filtered to **Activity disposition applied**, then **Send Text Message**, **Schedule Callback** or **Schedule Dialer Retry**.

**Alert someone when things go wrong.** Start with a **Timer** (every hour, say), run a report whose rows are the problem, such as conversations handed to a person in the last hour, with **Run Report**, and connect its **Done** outcome (there are rows) to **Notify** or an email. The **No rows** outcome means all is well.
