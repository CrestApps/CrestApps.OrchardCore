---
sidebar_label: Test and Monitor AI Conversations
title: Test and Monitor AI Conversations
description: Keep every version of an AI profile, test a change with simulated customers, compare two profiles on live traffic, score conversations for quality, and get alerted when something slips.
technical_manual:
  - ai/conversation-quality
---

Before a change to an AI profile goes live you can test it against simulated customers. Once it is live you can send part of your traffic to it, score real conversations for quality, compare the results in a report, and be alerted when a number slips. If the change makes things worse, restore the version before it.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > AI Profiles, Design > Workflows, and Reports |
| **Permission** | Manage AI profiles, Manage workflows, and View AI chat analytics for the reports |
| **Feature** | AI Services and Workflows. The reports need Reports, and the conversation metrics need AI Chat Session Analytics. |

<AskYourAdmin />

## Profile versions

Every time an AI profile is saved, the profile as it was saved is kept as a new version, numbered from 1. Each conversation records the version of the profile that answered it.

To see them, open **Artificial Intelligence > AI Profiles**, open the profile's actions menu and click **Versions**.

- **Compare** shows a version beside the profile as it is now: the instructions, the deployments, the model settings, and every other setting that differs. Changed settings are highlighted.
- **Restore** gives the profile that version's settings back. The restore is saved as a new version, so the version you replaced is kept too.

The profile's name, its technical name and its owner are not changed by a restore.

## Test a change with simulated customers

The **Simulate AI Conversation** task (under **AI Conversations**) plays an AI profile against a customer played by a model, then scores the conversation.

1. In **Design > Workflows**, create a workflow. Start it with whatever should run the tests: a **Timer**, an **HTTP Request Event** your release process calls, or nothing at all if you start it by hand.
2. Add **Simulate AI Conversation** and fill in:

| Field | What to write |
| --- | --- |
| **Profile** | The AI profile to test. |
| **Customer** | Who the customer is, what they want and how they behave. For example: *You are Dana. You want to move tomorrow's 3 pm cleaning to a morning next week. You are in a hurry.* |
| **Criteria** | One condition the conversation must meet per line, such as *The assistant offered a morning slot next week*. |
| **Variables** | The conversation variables the test starts with, if the profile's instructions use any. |
| **Most customer turns** | Where the test stops if the customer has not finished. |
| **Passing score** | The lowest score out of 100 that passes. |
| **Label** | A name for the test run, such as *Release tests*, so you can find the results in a report. |

3. Connect **Failed** and **Error** to a **Notify** task or an email.

The task runs the conversation in text, with the profile's instructions, knowledge and tools. Tools that run a workflow get `Workflow.Input.Simulation` set to true, so the workflow can answer with test data instead of booking, charging or texting for real. Add an **If/Else** on `{{ Workflow.Input.Simulation }}` at the start of each tool workflow that acts on the outside world.

The transcript is in `Workflow.Output.Transcript` and the score in `Workflow.Output.Evaluation`.

:::tip[Run a batch of tests]
Put the test cases in a **For Each** loop: a list of customers, each with their own criteria, and use the loop's current item, `{{ Workflow.Output.Current }}` unless you renamed it, in **Customer** and **Criteria**. Every result is kept, so the **Conversation evaluations** report shows the pass rate of the whole run.
:::

## Compare two profiles on live traffic

The **Split Traffic** task sends each run of a workflow down one of several outcomes, in proportion to their weights. Combined with **Use AI Profile** in an **AI Conversation Starting** workflow, it answers part of your conversations with a different profile.

1. Copy the AI profile and make your change on the copy.
2. Create a workflow that starts with **AI Conversation Starting**, filtered to the original profile.
3. Add **Split Traffic** with the outcomes `Current: 50` and `New: 50`. In **Keep the same outcome for**, write `{{ Workflow.Input.CustomerAddress }}` so a customer who comes back gets the same profile.
4. Connect **New** to **Use AI Profile** with the copy. Connect both outcomes to **Set Conversation Variables** with `variant: {{ Workflow.Properties.Variant }}`, so the variant is kept on the conversation.

Compare the two in the reports by profile, or by the `variant` variable.

## Score conversations for quality

The **Evaluate AI Conversation** task scores a finished conversation with the utility model. It reads the transcript and the profile's instructions and gives:

- a **score** out of 100;
- each of your **criteria**, met or not, with the reason;
- whether the customer's need was **resolved**, and their **sentiment** at the end;
- the **issues** found, each with the line of the transcript that shows it: unresolved, an answer with no source, a failed tool, broken instructions, tone, repetition, a transfer that went wrong.

The conversation passes when the score reaches the passing score and every criterion is met.

To score a sample of your conversations:

1. Create a workflow that starts with **AI Chat Session Closed**.
2. Add **Split Traffic** with `Evaluate: 20` and `Skip: 80` to score one conversation in five.
3. Connect **Evaluate** to **Evaluate AI Conversation**. Write your criteria, and a **Label** such as *Weekly QA*.
4. Connect **Failed** to whatever should happen: a **Notify** to a supervisor, or a task in your own system.

Every evaluation, real or simulated, also starts the workflows that begin with **AI Conversation Evaluated**. Tick **Only conversations that failed** to react to the failures only. The evaluation is in `Workflow.Input.Evaluation`.

## Reports

In **Reports**, the **AI Chat** data source has a **Conversation evaluations** data set: one row per evaluation, with its profile, profile version, label, score, whether it passed, and a count of each kind of issue. The **Chat sessions** data set has the **Profile version** of each conversation.

For example, a table grouped by **Profile version** with the average **Score** and the count of **Passed** compares version 3 with version 4.

## Get alerted when something slips

The **Check AI Conversation Metric** task measures one number over the last window, compares it with a threshold, and remembers whether an alert is already open. Start it with a **Timer** that runs every few minutes.

| Field | What it means |
| --- | --- |
| **Metric** | What to measure: conversations started, resolution rate, conversation length, response time, rated down, the average QA score, QA failures, tool failures, unsupported answers, AI calls, AI calls handed to a person, AI calls that failed, or the time to the AI's first words on a call. Rates are percentages. |
| **Profile** | One profile, or all of them. |
| **Alert when the value** | Is above or below the threshold, or rises or falls by more than the threshold, in percent of the window before. |
| **Window (minutes)** | How far back to measure. |
| **Fewest samples** | The fewest conversations, evaluations or calls the number must be measured over before it can alert, so two calls in a quiet hour do not raise a 50% failure rate. |

The task goes on by one of four outcomes:

| Outcome | When |
| --- | --- |
| **Alert started** | The threshold is crossed and no alert was open. |
| **Still alerting** | The threshold is still crossed. |
| **Resolved** | An alert was open and the number is back. |
| **Normal** | No alert, nothing crossed. |

Connect **Alert started** and **Resolved** to **Notify**, **Send Email**, or an **HTTP Request** to Slack or Teams. The measurement is in `Workflow.Output.Alert`, for example `{{ Workflow.Output.Alert.Value }}`.

For example, to be emailed when more than 5% of a profile's evaluated conversations had a failed tool in the last hour: **Metric** *Tool failures (%)*, **Alert when the value** *Is above the threshold*, **Threshold** `5`, **Window** `60`.

## Analyze a conversation again

The **Rerun AI Conversation Analysis** task runs a finished conversation's post-session tasks and conversion goals again with the profile's current settings, for example after you added a post-session task. The new results are in `Workflow.Output.Results`. To rerun a set of conversations, put it in a **For Each** over their session IDs.
