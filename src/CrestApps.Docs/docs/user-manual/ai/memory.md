---
sidebar_label: Memory
title: AI Memory
description: Understand what the AI remembers about you between chats, turn memory on for a profile, and clear what it saved.
technical_manual:
  - ai/memory
  - ai/memory-azure-ai
  - ai/memory-elasticsearch
---

**AI memory** lets an assistant remember stable things about a signed-in user from one chat to the next, such as how they like answers written, their role, or the project they are working on. Use it when the same people chat with an assistant often and should not have to repeat themselves.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Profiles (turn it on per profile); Settings > Artificial Intelligence (site settings) |
| **Permission** | Manage AI profiles (to turn it on); Clear AI memory (to clear your own memory); Clear AI memory for other users |
| **Feature** | AI Memory, turned on through one of the AI Memory indexing features |

<AskYourAdmin />

<YouTube video="ai-memory" title="Video overview of AI memory" />

The video covers why memory helps, how to set it up (the index, the site settings and the profile), a chat that remembers, and how to clear what was saved.

## What the AI remembers

Memory is private to each user. The AI only ever reads and changes the memory of the person it is talking to, and nobody else's chats can see it.

The AI saves a memory when you tell it something worth keeping, for example "Call me Sam" or "I manage the Northwind account. Keep answers short." In a later chat it looks up what it saved before it answers, so it can say "Hi Sam" and keep its answers short without being asked again.

Good things to remember:

- how you like answers written, such as short bullet points or a formal tone
- your preferred name and your role
- the projects or products you work on most
- topics you ask about often

Memory is not for secrets. The AI refuses to save obvious passwords, keys, card numbers or similar data, and you should never ask it to.

:::note
Memory works only for **signed-in** users. Website visitors who are not signed in never get memory, even when the profile has it turned on.
:::

## Ask the AI about its memory

You can talk to the AI about what it remembers, in plain words:

- "Remember that I prefer answers in Spanish."
- "What do you remember about me?"
- "Forget my project name."

## Clear your saved memory

<YouTube video="ai-memory" title="Video of seeing and clearing the saved memory" start={248} end={286} />

When you want the AI to forget everything it saved about you:

1. Open your user profile (the **Profile** item in the user menu at the top right of the admin).
2. Find the **AI Memory** section. When you have saved memories, it shows a **Danger zone** warning.
3. Click **Clear saved AI memory** and confirm with **Clear**.

This permanently removes everything the AI remembered about you. It cannot be undone. When nothing is saved, the section says "No saved AI memory was found for your account."

An administrator with the **Clear AI memory for other users** permission can do the same for another person: open **Access Control > Users**, edit the user, and use **Clear saved AI memory** in the **AI Memory** section.

## Turn on memory (administrators)

Memory needs a search index to store the memories in. Your technical team sets up the search service once (see the [Technical Manual](../../ai/memory.md)). After that, three steps turn memory on.

### Create the memory index

<YouTube video="ai-memory" title="Video of creating the memory index" start={88} end={128} />

1. Open **Search > Indexes** and click **Add index**.
2. Click **Add** on **AI Memory (Elasticsearch)**. The **AI Memory (Azure AI Search)** type works the same way, when that feature is on.
3. Enter an **Index Name** and a **Name**, the name the index has in the list.
4. Pick the **Embedding deployment**. It turns each memory into numbers that the search compares. It cannot be changed after the index is created, and the index then shows it as read-only.
5. Leave the other fields as they are, and click **Save**.

### Choose the memory index

<YouTube video="ai-memory" title="Video of choosing the memory index in the site settings" start={128} end={171} />

Open **Settings > Artificial Intelligence** and find the **Memory** section. Until an index exists, it shows a **No index profiles configured** warning.

| Field | What it does |
| --- | --- |
| **Index profile** | The index that stores every user's memories. Avoid changing it once people use memory, because memories saved in the old index are no longer found. |
| **Default top N** | How many saved memories the AI gets back when it searches a user's memory. |
| **Enable user memory** | Turns memory on for [chat interactions](chat-interactions.md). It is on by default. |

In the **General** section, **Enable preemptive memory retrieval** (on by default) looks up a person's memories before the model answers, and gives the matching ones to the model as background. Turn it off to keep the memory tools but skip that step.

### Turn it on for a profile

<YouTube video="ai-memory" title="Video of turning memory on for a profile" start={171} end={209} />

1. Open **Artificial Intelligence > Profiles** and edit the profile.
2. On the **Knowledge** tab, tick **Enable user memory**.
3. Click **Save**.

Memory is off by default on every profile, so you choose where it makes sense. Turn it on for an internal assistant your staff use every day; leave it off for a public website assistant. If no memory index is chosen yet, the profile shows an **Index not configured** warning when you tick the box.

Profile templates have the same checkbox, so profiles created from a template can start with memory on. See [Templates](prompt-templates.md).

### Try it

<YouTube video="ai-memory" title="Video of a chat that remembers" start={209} end={248} />

Open **Artificial Intelligence > Profiles**, open the **Actions** menu of the profile and click **New chat**. Tell the assistant something worth keeping, such as "Call me Sam, and keep your answers short." Then click **New Chat** and ask "What do you remember about me?" The new chat has no history, but the assistant looks up its memories first, and answers with the name it saved.
