---
sidebar_label: Speaking Style, Guardrails and Privacy
title: Speaking Style, Guardrails and Privacy
description: Turn on tested speaking styles, refuse topics with a placeholder reply, and choose what is kept of each conversation and for how long.
technical_manual:
  - ai/speaking-style-guardrails-and-privacy
---

Three sets of options on each AI profile shape how the AI talks, what it refuses to discuss, and what is kept of its conversations once they end.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > AI Profiles, Speaking Styles and Guardrail Topics |
| **Permission** | Manage AI profiles |
| **Feature** | AI Services and AI Chat. Workflows for the AI Guardrail Triggered event. |

<AskYourAdmin />

## Speaking style

On the profile's **Content** tab, the **Speaking Style** card lists sections you can add to the AI's instructions instead of writing them yourself. Each one adds a short, tested paragraph to the end of the system message, after the persona and the instructions, on every channel.

| Style | What the AI does |
| --- | --- |
| **AI disclosure** | Says it is an AI assistant when asked, and never claims to be a person. |
| **Scope limits** | Helps only with what its instructions cover, and steers other questions back. |
| **High empathy** | Acknowledges how an upset customer feels in one sentence before solving the problem. |
| **Echo verification** | Reads back names, numbers, addresses, dates and amounts, and asks the customer to confirm before using them. |
| **NATO spelling** | Spells letters that matter with the NATO alphabet: "B as in Bravo". |
| **Speech normalization** | Writes numbers, dates, times and money the way they are said, for replies read aloud on calls. |
| **Fuzzy name matching** | Treats names that sound alike as possible matches before saying a record can't be found. |
| **Filler words** | Sounds natural on calls, with contractions and an occasional "so" or "let me see". |

Echo verification, NATO spelling, speech normalization and filler words are written for phone calls. The others suit every channel.

### Add your own speaking style

The styles are prompt templates in the **Speaking style** category, so you can add your own, such as your brand's tone of voice, and use it on as many profiles as you like.

1. Open **Artificial Intelligence > Speaking Styles** and click **Add Speaking Style**, or click **Add a speaking style** under the styles on a profile.
2. Give it a **Title** and a **Description**: the profile editor shows them beside the checkbox. The category is already set to **Speaking style**.
3. Write the section in **System message**, the way you want the AI to read it, and click **Save**.

The **Speaking Styles** page lists every style, the ones the site comes with included, to edit or delete.

The style now appears in the **Speaking Style** card of every profile. Templates travel with the **AI Profile Templates** recipe step and deployment step, so your styles move from one site to another with the profiles that use them. The styles and topics the site comes with are added once, when the AI feature is enabled, so you can reword them or delete the ones you don't use; a deleted one doesn't come back.

## Guardrails

Guardrails refuse topics the AI must not discuss. They are in the **Guardrails** card of the profile's **Content** tab, after the **Prompt Security** card, and work on top of it.

1. Turn on **Check what the customer says**, **Check the AI's replies**, or both.
2. Tick the **Topics to refuse**. The site comes with editable topics for harassment and abuse, hate, self-harm, sexual content, violence and weapons, illegal activity, medical, legal or financial advice, and attempts to override the instructions, and lists your own topics with them.
3. Optionally describe **Other topics to refuse** in a sentence or two, such as "competitors' prices", for topics of this profile alone.
4. Optionally write the **Placeholder reply** the AI gives instead. Left empty, it says "I'm sorry, I can't help with that. Is there something else I can help you with?"

Each message checked is one more request to the utility model, which judges what the message asks for rather than single words: a refund for a damaged knife is not violence. A message it refuses is answered with the placeholder reply and never reaches the model or the profile's tools. A reply it refuses is replaced before it is sent or spoken.

| Channel | What is checked |
| --- | --- |
| Website and admin chat | What the customer types, and the AI's reply. The reply streams as it is written, so a refused reply is replaced once it is complete. A refused message and its placeholder are not saved in the conversation, like a message Prompt Security blocks. |
| Text messages | The customer's texts, and the reply before it is sent. |
| Phone calls, turn-based | What the caller said, and the reply before it is spoken. Each check adds a moment before the AI answers. |
| Phone calls, realtime voice | What the caller said, checked while the call goes on, so normal answers are not slowed down. When a request is refused, the AI is told not to act on it. If it is still answering, it is cut off and says the placeholder reply instead; an answer it has already finished is left as it was. Its replies are not checked: they are spoken as they are made. |

If the utility model can't be reached, the conversation goes on unchecked rather than leaving the customer without an answer.

### Add your own topics

Topics are prompt templates in the **Guardrail topic** category: the template's title names the topic, and its text tells the utility model what falls under it. Add one when several profiles must refuse the same thing, such as your competitors or a pending lawsuit.

1. Open **Artificial Intelligence > Guardrail Topics** and click **Add Guardrail Topic**, or click **Add a topic** under the topics on a profile.
2. Give it a **Title**, such as "Competitors". The category is already set to **Guardrail topic**.
3. In **System message**, describe what is not allowed in a sentence or two, for example: "Questions about Acme or Globex, their prices or how we compare to them."
4. Click **Save**. The topic appears in the **Guardrails** card of every profile.

Like speaking styles, topics travel with the **AI Profile Templates** recipe step and deployment step. A profile's guardrails, with every other setting, travel with the **AI Profile** recipe step and deployment step.

### Act when a guardrail triggers

The **AI Guardrail Triggered** workflow event starts a workflow each time a guardrail refuses a message, for every profile or one. It can notify a supervisor, flag the contact, or hand the conversation to a person. The workflow input has:

| Value | What it is |
| --- | --- |
| `Category` | The topic's template name, such as `guardrail-topic-self-harm`, or `other` for the profile's own other topics. |
| `Topic` | The topic's title, such as "Self-harm". |
| `Direction` | `Customer` or `Reply`. |
| `Reason` | The utility model's one-sentence reason. |
| `Text` | The refused message. |
| `SessionId`, `ProfileId` | The conversation and the profile. |
| `Contact`, `Channel`, `Variables` | On calls and texts, the conversation's contact, channel and variables. |

## Privacy

On the **Data Processing & Metrics** tab, **Privacy** chooses what is kept of each conversation and for how long.

| What to keep | What is stored |
| --- | --- |
| **Everything** | The full transcript, summaries, extracted fields and evaluations. |
| **Everything except personal details** | The same, with names, street addresses, dates of birth, phone numbers, email addresses, and card, account and identity numbers replaced by placeholders such as `[NAME]` and `[PHONE]`. |
| **Times, outcomes and scores only** | The transcript is deleted, and the title, summaries, extracted values and evaluation notes are cleared. Times, status, analysis task names, criteria results and scores are kept. |

The choice applies about 15 minutes after a conversation ends, once its workflows, its post-session analysis and contact memory have read it, so they still work on the full conversation. It applies to conversations that end after you choose it; conversations kept before are left as they were. Personal details are found by patterns and by the utility model; a detail it misses stays, so check a sample of redacted conversations before relying on it.

**Delete conversations after** deletes a conversation, with its transcript, extracted data and evaluations, after that many days without activity. Leave it empty to keep conversations. Usage records are kept, because they are what usage is billed and reported on.

:::note[Contact memory]
Contact memory is kept apart from conversations, so the storage mode does not change it. Turn off **Save to contact memory** if it should not hold personal details either. See [Contact memory and integrations](contact-memory-and-integrations.md).
:::

### Bleeped call recordings

With **Everything except personal details**, the recordings of AI phone calls are bleeped too, once the conversation's transcript has been redacted: every customer line that held a personal detail (a placeholder such as `[CARD]`, or a long run of digits the redaction missed) is covered with a tone, from just before the line starts to when the next line starts, and the recording is stored again as WAV in place of the original. The **Call Recording Bleeping** background task does this every 10 minutes for recordings of the last 14 days, and the recordings API marks a bleeped recording. Lines are placed by when they were said, not word by word, so a whole line is covered, including any words around the detail. MP3 and 16-bit WAV recordings are read; others are left as they are.

A single conversation can also be deleted through the API, for example when a customer asks to be forgotten. See the [AI conversations API](../../ai/conversations-api.md).
