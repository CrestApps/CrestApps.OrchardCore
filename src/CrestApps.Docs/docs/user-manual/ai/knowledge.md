---
sidebar_label: Knowledge
title: Give the AI Your Knowledge
description: Let an assistant answer from your own documents, search indexes, folders of files and websites, and show where each answer came from.
technical_manual:
  - ai/documents/index
  - ai/data-sources/index
  - ai/data-sources/web-crawlers
  - ai/file-sources
---

On its own, an AI model only knows what it learned in general. To answer questions about *your* prices, policies, products or help articles, give it your content. The assistant then looks up the passages that match each question, answers from them, and lists them as numbered sources under the answer.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Profiles (**Knowledge** tab); Artificial Intelligence > Data Sources; Artificial Intelligence > File Sources; Artificial Intelligence > Web Crawlers |
| **Permission** | Manage AI profiles; Manage AI data sources; Manage file sources; Manage web crawlers |
| **Feature** | AI Documents for Profiles, AI Documents for Chat Sessions, AI File Sources, AI Web Crawlers, and an indexing feature such as AI Data Sources - Elasticsearch |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of an Elasticsearch-backed AI data source grounding answers on Article content">
  <source src="/img/docs/ai-elasticsearch-datasource.mp4" type="video/mp4" />
</video>

## Choose how to add your knowledge

| Your content | Use | Who keeps it up to date |
| --- | --- | --- |
| A handful of files, such as a price list or a policy PDF | [Documents on a profile](#upload-documents-to-a-profile) | You, by uploading a new version |
| Files a user wants to ask about in one chat | [Attachments in a chat](#let-users-attach-files) | The user, for that chat only |
| Content already on this site, such as articles or products | A [data source](#data-sources) of type **Search Index Profile** | The site, automatically when content changes |
| An external search index or database table | A [data source](#data-sources) of type **Elasticsearch**, **Azure AI Search** or **PostgreSQL** | The site, when you sync |
| A folder of files on a server, an FTP site or an SFTP site | A [file source](#file-sources) | The site, on a schedule |
| A public website, such as your help center | A [web crawler](#web-crawlers) | The site, on a schedule |
| A public documentation site, searched only when needed | A documentation search [tool instance](tools-and-agents.md#tool-instances) | Nobody: it searches the live site |

## Before you start

Knowledge is stored in a search index that understands meaning, not just words. Your technical team sets up the search service once. Then an administrator creates the indexes in **Search > Indexing** with **Add index**, choosing the type and an **Embedding deployment** (see [Connections and deployments](connections.md)):

| Index type | Used for |
| --- | --- |
| **AI Documents** (Azure AI Search or Elasticsearch) | Documents on profiles and attachments in chats. Then choose it under **Settings > Artificial Intelligence > Documents > Index profile**. |
| **AI Knowledge Base Index** (Azure AI Search or Elasticsearch) | Data sources, file sources and web crawlers. |

The embedding deployment cannot be changed after the index is created. Avoid switching to a different index once people use it, because documents stored in the old one are no longer found.

## Upload documents to a profile

Documents on a profile are background knowledge the assistant uses in every chat.

1. Open **Artificial Intelligence > Profiles**, edit the profile, and open the **Knowledge** tab.
2. Drag files onto **Drag and drop files here**, or click **Browse Files**. New files show a **Pending** badge.
3. Click **Save**. The files are read, split into passages and indexed.

To remove a document, click its **Remove document** icon and save. **Download document** gets the file back.

**Supported formats** under the upload area lists the file types you can use. Usually that is text, Markdown, JSON, XML, HTML, YAML and log files, plus PDF, Word (`.docx`) and PowerPoint (`.pptx`) when those features are on. Spreadsheets (CSV and Excel) cannot be uploaded to a profile, because they are meant for calculations rather than for looking up passages. Old Office formats (`.doc`, `.xls`, `.ppt`) are not supported; save them in the new format first.

| Field | What it does |
| --- | --- |
| **Document Top N** | How many matching passages the assistant reads for each question, from 1 to 20. Default 3. More passages give more context, but cost more and slow the answer. |
| **Document retrieval mode** | **Chunk** gives the assistant only the matching passages. **Hierarchical** finds the matching passages, then gives it the whole documents they come from: better for short documents, much more text for long ones. **Default setting** uses the site's choice. |

The assistant uses the documents quietly; it does not tell users that it has attached files unless they ask about sources.

## Let users attach files

To let people attach documents or pictures to a chat, open the profile's **Knowledge** tab and tick:

| Field | What it does |
| --- | --- |
| **Allow session document uploads** | Users can attach documents to chats with this profile. |
| **Allow session image uploads** | Users can attach pictures. Needs a default vision deployment. |
| **Largest document that can be indexed** | The longest document, in characters of text, a user can attach. Bigger documents are refused when attached. **Use site default** keeps the site's limit; 0 means no limit. |
| **Describe figures in uploaded documents** | Whether the AI describes charts and pictures inside attached documents, so it can answer about them. This uses a vision model and costs more. **Use site default**, **Describe figures** or **Do not describe figures**. |

Attached files belong to one chat only. How users attach them is described in [Chat with an AI assistant](chat.md#attach-files-to-a-chat).

## Data sources

A data source connects the assistant to a knowledge base built from a search index or a database. Use it for large or changing content, such as all the articles on your site.

### Create a data source

1. Open **Artificial Intelligence > Data Sources** and click **Add Data Source**.
2. In **Available Source Types**, click **Add** on the type you need:
   - **Search Index Profile**: an index of this site's own content.
   - **Elasticsearch** or **Azure AI Search**: an external index.
   - **PostgreSQL**: a database table.
   - **File**: the target of a [file source](#file-sources).
   - **Web**: the target of a [web crawler](#web-crawlers).
3. Enter a **Name** and choose the **Destination index**, the AI Knowledge Base index that stores the passages. It cannot be changed after you save.
4. Fill in the source's own section, such as the **Source index**, or the connection details your technical team gives you for an external index or database. For an external source, **Use the globally configured connection** uses the connection your technical team set up.
5. For every type except **File** and **Web**, fill in the **Field Mapping**: the **Content field** to search, an optional **Title field** for the source list, and a **Key field** that identifies each entry. Ask your technical team which fields to use.
6. Click **Save**.

The data source fills its knowledge base and keeps it in step with the source. To rebuild it by hand, open the data source's **Actions** menu and choose **Sync index**.

### Use a data source in a profile

On the profile's **Knowledge** tab:

| Field | What it does |
| --- | --- |
| **Data source** | The data source to answer from. **No data source** for none. |
| **Filter** | Searches only some entries, for example only active products. Ask your technical team for the expression. |
| **Restrict answers to retrieved data only** | The assistant answers only from the data source. When nothing matches, it says so instead of answering from general knowledge. Turn it on for a public assistant that must stay on topic. |
| **Strictness** | From 1 to 5. How closely a passage must match the question. Higher values use fewer, more relevant passages. Empty uses the site default (3). |
| **Retrieved documents** | From 3 to 20. How many passages are retrieved for each question. Empty uses the site default. |

The site defaults are under **Settings > Artificial Intelligence > Data Sources**: **Default strictness** and **Default top documents**.

## File sources

A file source reads a folder of files on a schedule and keeps a **File** data source up to date, so nobody has to upload anything. Use it for documents another system exports, such as nightly product sheets on an FTP site.

1. Create a data source of type **File** (see [Create a data source](#create-a-data-source)).
2. Open **Artificial Intelligence > File Sources** and click **Add File Source**.
3. In **Available Connectors**, click **Add** on **File system** (a folder on the site's server), **FTP / FTPS** or **SFTP**.
4. Fill in the fields below and the connection details, then click **Save**.

| Field | What it does |
| --- | --- |
| **Name** | The name shown in the list. |
| **Target File data source** | The **File** data source that receives the content. |
| **Enabled** | Whether the scheduled reading runs. |
| **Re-read interval (minutes)** | How often the folder is read again. The site checks once an hour, so shorter intervals still run at most hourly. Empty uses the site default. |
| **Figures** | **Auto** (describe charts and pictures that look worth it), **All**, or **Off**. Describing figures needs a vision model and costs more. |
| **Figures described per document** | The most figures described in one document. |
| **Vision deployment** / **Utility deployment** | The models used to read the files. Leave them on the site defaults. |
| **Items per run** | The most files read in one run. |
| **Language** | The language of the files, such as `en-US`, when they all use one. |
| **Folder**, **Include sub-folders**, **Files per listing** | Which folder to read. For **File system**, the folder is inside the area your technical team set aside for this site. |

For FTP and SFTP, enter the **Host**, **Port**, **Username** and **Password** (or a **Private key** for SFTP) your technical team gives you. A stored password or key is never shown again; leave the field blank to keep it.

The list shows each source's last run: how many files were found, ingested, unchanged, removed and failed, or **Never run**. To read a source right away, open its **Actions** menu and choose **Read now**.

## Web crawlers

A web crawler reads a public website, page by page, into a **Web** data source. Use it for your help center or product pages, so the assistant can answer from them and link to the page.

1. Create a data source of type **Web** (see [Create a data source](#create-a-data-source)).
2. Open **Artificial Intelligence > Web Crawlers** and click **Add Web Crawler**.
3. In **Available Crawl Strategies**, click **Add** on **Sitemap**.
4. Fill in the fields below and click **Save**.

| Field | What it does |
| --- | --- |
| **Name** | The name shown in the list. |
| **Target Web data source** | The **Web** data source that receives the pages. Several crawlers can feed one data source. |
| **Enabled** | Whether the crawler runs on its schedule. |
| **Re-index interval (minutes)** | How often the site is read again. Empty means once a day. |
| **Base URL** | The website to read, such as `https://help.contoso.com`. The crawler finds its sitemap. |
| **Sitemap URL (optional)** | A sitemap address, when the crawler should start there. |
| **Max pages** | The most pages read per run. Default 500. |
| **Max concurrent requests** | How many pages are read at once. Default 4. Keep it low to be polite to the website. |
| **Request timeout (seconds)** | How long to wait for one page. Default 30. |
| **Include URL patterns** / **Exclude URL patterns** | Which pages to read or skip, one pattern per line. Ask your technical team to write these. Exclude wins over include. |
| **User-Agent (optional)** | How the crawler introduces itself to the website. |

To read the site right away, open the crawler's **Actions** menu and choose **Synchronize now**. Only new, changed and removed pages are processed. Sources in answers link back to the original page.

## How the assistant uses your knowledge

- **Before each answer or on demand.** With **Enable preemptive retrieval-augmented generation (RAG)** on (under **Settings > Artificial Intelligence > Default Orchestrator**), the assistant searches your knowledge before every answer. With it off, the assistant searches when it decides it needs to. Preemptive search is always on for profiles without tools.
- **Sources.** Statements that come from your content get small numbers, and a numbered list of sources appears under the answer. Sources with a web address are links.
- **Staying on topic.** **Restrict answers to retrieved data only** keeps the assistant to your content.

:::tip[When answers are wrong or missing]
Check that the data source has finished syncing, that the passage really contains the answer, and try a lower **Strictness** or more **Retrieved documents**. If the assistant still answers from general knowledge, turn on **Restrict answers to retrieved data only** and tell it in the **System instructions** to answer only from the provided content.
:::
