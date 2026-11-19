---
sidebar_label: AI Search
title: Let AI Answer from Your Knowledge Base
description: "Step by step: index your help articles, turn them into an AI knowledge base, give it to an AI profile, and let visitors ask it questions on your website, or staff in the admin chat, with links back to the articles."
technical_manual:
  - modules/knowledge-base/ai-search
---

An AI assistant can answer questions from your own help articles instead of from what a model learned in general. It looks up the passages of your articles that match each question (this is called *retrieval*, or RAG), answers from them, and lists the articles it used, with links. This page sets it up from start to finish.

| | |
| --- | --- |
| **Menu** | Search > Indexes; Artificial Intelligence > Data Sources; Artificial Intelligence > Profiles; Design > Widgets; Access Control > Roles |
| **Permission** | Manage Indexes; Manage AI data sources; Manage AI profiles; Manage layers; Manage Roles |
| **Feature** | Knowledge Base, Elasticsearch, AI Data Sources - Elasticsearch, AI Chat, Widgets, Layers |

<AskYourAdmin />

Here is how the pieces fit together:

1. A **content index** holds your published articles in Elasticsearch.
2. An **AI knowledge base index** holds the same articles split into passages, with their meaning (embeddings), so the AI can find the passages that answer a question.
3. An **AI data source** copies the articles from the first index into the second one and keeps them in step when articles change.
4. An **AI profile** answers from that data source.
5. A **chat widget** on your website, or the admin chat, lets people ask the profile questions.

:::warning[Articles not everyone may read stay out]
The AI knowledge base leaves out every article restricted with **Which roles can see this content**: only articles with no role picked (**Any role**) are copied into it. An article you restrict later is removed from it at its next sync. So a public assistant never answers from an article meant for your staff. Keep articles for staff out of public answers by restricting them; there is nothing else to set.
:::

## Before you start

You need:

| What | Who sets it up |
| --- | --- |
| An Elasticsearch server. The AI knowledge base needs Elasticsearch or Azure AI Search; this page uses Elasticsearch. A local development host can run one for you. | IT; see the [Technical Manual](../../modules/knowledge-base/ai-search.md) |
| An AI connection with a **chat** deployment (the model that writes answers) and an **embedding** deployment (the model that turns text into meaning). | Your administrator; see [AI connections](../ai/connections.md) |
| The Knowledge Base with published articles. | See [Write articles](./write-articles.md) |

Turn on these features under **Tools > Features**:

| Feature | Why |
| --- | --- |
| **Knowledge Base** | Your articles |
| **AI Data Sources - Elasticsearch** | The AI knowledge base index and the data source. It turns on **Elasticsearch** (the content index of your articles), **AI Data Sources** and **Indexing** too. |
| **AI Chat** | Chatting with the profile, in the admin and in the website widget |
| **Widgets** and **Layers** | The chat widget on your website. The Knowledge Base recipe already turns them on. |

## 1. Choose what the articles' text holds

The AI reads the *full text* of each article. By default that is the title and the body. Add the summary too, so short questions find the right article:

1. Go to **Design > Content Definition > Content Types** and edit **Knowledge Article**.
2. Leave **Include display text** and **Include body parts** ticked.
3. Tick **Use custom full-text** and enter `{{ Model.Content.KnowledgeArticlePart.Summary }}` in its box.
4. Click **Save**.

## 2. Create the content index of your articles

1. Go to **Search > Indexes** and click **Add index**.
2. On **Content in Elasticsearch**, click **Add**.
3. Enter an **Index Name**, such as *knowledgearticles*, and a **Name**, such as *Knowledge articles*.
4. Under **Content types**, tick only **Knowledge Article**.
5. Leave **Index Latest version** unticked, so drafts stay out.
6. Tick **Store Source Data**. The data source reads the articles from the index, and can't without it.
7. Click **Save**.

The new index shows in the list, and the site adds your published articles to it in the background. To fill it again at any time, open its **Actions** menu and choose **Rebuild**.

## 3. Create the AI knowledge base index

1. Go to **Search > Indexes** and click **Add index**.
2. On **AI Knowledge Base Index (Elasticsearch)**, click **Add**.
3. Enter an **Index Name**, such as *helpcenterknowledge*, and a **Name**, such as *Help center knowledge*.
4. Choose the **Embedding deployment** of your AI connection. It can't be changed once the index is created.
5. Click **Save**.

## 4. Create the AI data source

1. Go to **Artificial Intelligence > Data Sources** and click **Add Data Source**.
2. On **Search Index Profile**, click **Add**.
3. Enter a **Name**, such as *Help center articles*.
4. Under **Source**, choose the **Source index** *Knowledge articles* from step 2.
5. Choose the **Destination index** *Help center knowledge* from step 3. It can't be changed once you save.
6. Under **Field Mapping**, keep the values filled in for you: **Content field** `Content.ContentItem.FullText`, **Title field** `Content.ContentItem.DisplayText.Keyword` (or another display text field), and **Key field** left as the content item id. The key is what links each answer's sources to the articles, so don't change it.
7. Click **Save**.

The data source copies your articles into the knowledge base in the background. To run it again by hand, open the data source's **Actions** menu and choose **Sync index**.

Give it a minute, then go on to step 5 and ask the profile a question one of your articles answers: an answer with sources shows the knowledge base is filled.

## 5. Create the AI profile

1. Go to **Artificial Intelligence > Profiles** and create a profile of type **Chat**, such as *Help center assistant*. The **Website assistant** starting point is made for a public assistant. See [AI profiles](../ai/profiles.md).
2. Choose your chat connection and deployment.
3. Write its instructions, for example:

   > You are the help center assistant of our website. Answer only from the help articles you are given. Keep answers short and in plain words, and point to the article each answer comes from. When the articles don't answer the question, say so and suggest contacting support.

4. Open the **Knowledge** tab and choose the **Data source** *Help center articles*.
5. Tick **Restrict answers to retrieved data only**, so the assistant answers only from your articles and says so when it can't find the answer. Leave it unticked if it may also answer from general knowledge.
6. Leave **Strictness** and **Retrieved documents** empty to use the site's defaults, or see [Knowledge](../ai/knowledge.md#use-a-data-source-in-a-profile).
7. Click **Save**.

To try it, open the profile's **Actions** menu and click **New chat**. Ask a question one of your articles answers. The answer carries small numbers, and a numbered list of sources under it names the articles. Each source links to the article's page.

## 6A. Let anyone ask on your website

1. Go to **Design > Widgets**. In the zone where the chat button should live, such as **Footer**, click **Add Widget** and choose **Artificial Intelligence Chat**.
2. Choose **The layer** that decides where it shows:
   - **Help center**, made by the Knowledge Base recipe, shows it on `/help` and on every article;
   - a layer whose rule is a **Boolean** condition set to true, often named *Always*, shows it on every page.
3. Choose the **Profile** *Help center assistant* and publish the widget.
4. Go to **Access Control > Roles** and edit **Anonymous**. Tick only **Query AI profile - Help center assistant** and save. Never tick **Query any AI profile**: it would open every profile of the site to visitors.
5. To let signed-in readers use it too, give the same permission to **Authenticated**.

Check it as a visitor: open `/help` in a private browser window, where you are signed out. The round chat button shows in the corner; ask a question and check the sources under the answer.

More about the widget, what visitors see, and how a public assistant is protected (limits on messages, blocked prompt tricks) is in [Chat widgets](../ai/chat-widgets.md).

## 6B. Let staff ask in the admin

Staff can chat with the profile without a widget:

- Open **Artificial Intelligence > Profiles**, then **New chat** in the profile's **Actions** menu.
- Or tick **Show on admin menu** on the profile, so it shows by its name under **Artificial Intelligence**.

Give the staff roles **Query AI profile - Help center assistant** under **Access Control > Roles**. See [Chat with an AI assistant](../ai/chat.md). The admin chat answers from the same knowledge base, so staff get the articles for everyone too.

## Keep it up to date

- Publishing, changing or deleting an article updates the content index, and the data source updates the knowledge base soon after. Restricting an article removes it from the knowledge base.
- Rebuilding the content index under **Search > Indexes** syncs the data source again.
- **Sync index** in the data source's **Actions** menu rebuilds it by hand.
- Every night the site also repairs any difference between the two indexes.

## When it doesn't work

| What you see | What to check |
| --- | --- |
| The assistant says it can't find an answer that is in an article | The article is published and has no role picked. Run **Sync index** on the data source (step 4) and ask again. If the question is worded quite differently from the article, try **Strictness** 2. |
| No answer ever has sources | **Store Source Data** is ticked on the content index (step 2); after ticking it, **Rebuild** the content index. The **Embedding deployment** of the knowledge base index works: an embedding error is in the site's logs. |
| Visitors don't see the chat button | **Anonymous** has **Query AI profile - *profile name*** (step 6A), and the widget's layer is active on that page. |
| Visitors get an error or "access denied" when they chat | The same permission, on the role of the visitor. |
| Sources have no links | The data source's **Key field** must stay the content item id. |
| An article for staff appears in answers | It has no role picked. Pick the staff roles in **Which roles can see this content** and publish it; it leaves the knowledge base at the next sync. |
