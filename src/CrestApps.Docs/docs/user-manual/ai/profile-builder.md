---
sidebar_label: Profile Builder
title: Profile Builder
description: Build an AI profile by playing the person it will talk to and telling the AI what it got wrong; it rewrites its own instructions until it behaves the way you want.
technical_manual:
  - ai/profile-builder
---

The **Profile Builder** helps you write the instructions for an AI profile without writing them yourself. In a chat, you play the person the AI will talk to: a customer, a patient, a lead, whoever it serves. When the AI answers in a way you do not like, you say what was wrong, and the AI rewrites its own instructions. When it behaves the way you want, you save the result as an [AI profile](profiles.md).

It suits profiles that hold a conversation on their own, such as automated text messages or phone calls, where you need to know how the AI will react before a real person talks to it.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Profile Builder |
| **Permission** | Manage AI profiles; Edit own chat interactions |
| **Feature** | AI Profile Builder |

<AskYourAdmin />

This video shows why the Profile Builder exists, how it works, and how to build, test and save a profile with it.

<YouTube video="ai-profile-builder" title="Video overview of the AI Profile Builder" />

## Start a session

<YouTube video="ai-profile-builder" title="Video of starting a Profile Builder session from an existing AI profile" start={99} end={146} />

1. Open **Artificial Intelligence > Profile Builder**.
2. Click **New Session** to start empty. To begin from an AI profile you already have, open the arrow beside **New Session**, choose **Improve an existing AI profile...**, pick the profile and click **Start session**. The session starts with the profile's instructions and settings, and can update that profile when you finish.

The list shows your sessions with when each last changed and how much feedback and how many revisions it has. Use **Search** to find one by title, **Open** to continue it, and **Actions > Delete** to remove it. AI profiles saved from a session are kept.

The session page has three parts: the **Settings** on the left, the chat in the middle, and the feedback panel on the right.

In **Settings**, choose the **Chat deployment** (the model to test) and, if you like, write a first version of the **System instructions**. Tools, knowledge and other settings work as in a [chat interaction](chat-interactions.md).

## Role-play and give feedback

<YouTube video="ai-profile-builder" title="Video of giving feedback on a reply and regenerating it with the rewritten instructions" start={146} end={203} />

1. Type what that person would say and send it. The AI answers.
2. Under a reply, click **Feedback**. The reply is highlighted and shown in the feedback panel.
3. In **Your feedback**, write what the AI should have done differently, for example *"Too long. Keep text messages under two sentences"* or *"Never quote a price; offer to connect them with sales"*.
4. Click one of:
   - **Feedback & regenerate**: rewrites the instructions, removes the reply and answers your message again with the new instructions. Use it on the latest reply.
   - **Feedback only**: rewrites the instructions and keeps the reply.

After a moment the instructions in **Settings** change, and the panel says what was changed and why.

When a reply is just right, click **Looks good** under it. The AI keeps that behavior in mind when it changes its instructions later.

:::tip[Keep going until it holds]
If the regenerated reply is still wrong, give feedback again. The AI considers every comment you gave on that message and on earlier ones, so fixing one thing does not undo another.
:::

You can also edit the instructions in **Settings** yourself at any time. Your edit is kept as its own revision.

### Voice

If the chat deployment has a conversation deployment, you can role-play by voice. Feedback works on spoken replies too, but they cannot be regenerated, because they were already heard. After feedback, end the voice conversation and start it again to use the new instructions.

## Improve it with simulated customers

Instead of playing the customer yourself, you can let models do it. On the **Simulate** tab:

1. Under **Customers to play**, describe each customer: who they are, what they want and how they behave, with a blank line between customers. For example: *Dana is angry that her order 1001 is a week late and wants a refund.* Up to five.
2. Under **What every conversation must achieve**, write one condition per line, such as *Confirms the order number before acting on it*.
3. Choose the **Customer model** that plays the customers and the **Judge model** that scores each conversation. The assistant answers with the session's own deployment, so three models take part; a different customer and judge give a fairer test.
4. Set the **Turns** per conversation, the most **Rounds**, and the **Passing score** out of 100.
5. Leave **Rewrite the instructions when a conversation fails** on, and click **Run simulation**.

Each round, every customer talks to the current instructions, with the session's tools, and the judge scores each conversation and says what went wrong. When a conversation fails, the coach rewrites the instructions from what the judge found, as it does from your feedback, and the next round tests the new revision. The run stops when every conversation passes, when the rounds run out, or when you click **Stop after this round**.

The results list each conversation with its score, the judge's findings and the transcript, newest first. Each rewrite is a revision marked **Simulation** under **Revisions**, so you can compare or restore it like any other, and its findings join the feedback that **Replay** checks.

Tools run during a simulation as they do in a real conversation, told it is a simulation through `Workflow.Input.Simulation`. Make tool workflows that act on the outside world answer with test data when it is true.

## Revisions

<YouTube video="ai-profile-builder" title="Video of the revision history and of replaying earlier feedback" start={203} end={237} />

The **Revisions** tab lists every version of the instructions, with what changed and why. Expand **System message** to read a version, and click **Restore** to make it the current one again.

## Check that earlier fixes still work

On the **Replay** tab, click **Run replay**. Every message you gave feedback on is answered again with the current instructions, and each answer is marked:

- **Pass**: the answer respects your feedback.
- **Fail**: the answer repeats a mistake you pointed out.
- **Unclear**: your feedback was too vague to judge.

Tools are not used during a replay, so nothing is sent or booked.

## Save the result

<YouTube video="ai-profile-builder" title="Video of saving the session to an AI profile" start={237} end={271} />

On the **Finish** tab:

- **Opening message**: what the AI is told when it starts a conversation itself, such as an automated text. Click **Try the opening** to see how it opens, and **Save** to keep it. It becomes the profile's opening message.
- **Coach deployment**: the model that reads your feedback and rewrites the instructions. A capable model writes better instructions. By default it is the chat deployment.
- **Copy system message** copies the instructions.
- **Update the AI profile** saves the result to the profile the session started from.
- **Save as a new AI profile**: enter a **Title** and **Technical name**, then click **Create AI profile**. The new profile opens in the editor.

The session stays in the list, so you can come back to it later.
