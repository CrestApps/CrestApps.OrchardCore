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

On its own, an AI model only knows what it learned in general. To answer questions about *your* prices, policies, products or help articles, give it your content. The assistant then looks up the passages that match each question and answers from them. For content such as the pages of a site, the answer carries small numbers and a numbered list of sources appears under it. Documents attached to a profile work quietly in the background.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Profiles (**Knowledge** tab); Artificial Intelligence > Data Sources; Artificial Intelligence > File Sources; Artificial Intelligence > Web Crawlers |
| **Permission** | Manage AI profiles; Manage AI data sources; Manage file sources; Manage web crawlers |
| **Feature** | AI Documents for Profiles, AI Documents for Chat Sessions, AI File Sources, AI Web Crawlers, and an indexing feature such as AI Data Sources - Elasticsearch |

<AskYourAdmin />

<YouTube video="ai-knowledge" title="Video overview of giving an assistant knowledge" />

The video covers why a general model needs your knowledge, the four ways to add it, creating the documents index, uploading a document to a profile and asking about it, and letting people attach files to a chat.

<YouTube video="ai-walkthroughs" title="Video of an Elasticsearch-backed AI data source grounding answers on Article content" start={260} end={351} />

## Choose how to add your knowledge

<YouTube video="ai-knowledge" title="Video of the four ways to add knowledge" start={57} end={95} />

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

<YouTube video="ai-knowledge" title="Video of creating the documents index" start={95} end={143} />

Knowledge is stored in a search index that understands meaning, not just words. Your technical team sets up the search service once. Then an administrator creates the indexes in **Search > Indexing** with **Add index**, choosing the type, and, for an **AI Knowledge Base Index**, an **Embedding deployment** (see [Connections and deployments](connections.md)):

| Index type | Used for |
| --- | --- |
| **AI Documents** (Azure AI Search or Elasticsearch) | Documents on profiles and attachments in chats. It has no embedding choice of its own: it uses the site's **Default embedding deployment**. Then choose it under **Settings > Artificial Intelligence > Documents > Index profile**. |
| **AI Knowledge Base Index** (Azure AI Search or Elasticsearch) | Data sources, file sources and web crawlers. |

The embedding deployment of an index cannot be changed after the index is created. To create an **AI Knowledge Base Index**, open **Search > Indexes**, click **Add index**, click **Add** on **AI Knowledge Base Index (Elasticsearch)** (or the Azure AI Search type), enter an **Index Name** and a **Name**, pick the **Embedding deployment** and click **Save**.

<YouTube video="ai-file-sources" title="Video of creating the knowledge base index" start={90} end={122} />

Avoid switching to a different index once people use it, because documents stored in the old one are no longer found.

## Upload documents to a profile

<YouTube video="ai-knowledge" title="Video of uploading a document to a profile" start={143} end={205} />

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

The assistant uses the documents quietly; it does not tell users that it has attached files unless they ask about sources. To try it, open the profile's **Actions** menu, click **New chat**, and ask something only the documents can answer.

<YouTube video="ai-knowledge" title="Video of asking a question about a profile document" start={205} end={243} />

## Let users attach files

<YouTube video="ai-knowledge" title="Video of letting people attach files to a chat" start={243} end={289} />

To let people attach documents or pictures to a chat, open the profile's **Knowledge** tab and tick:

| Field | What it does |
| --- | --- |
| **Allow session document uploads** | Users can attach documents to chats with this profile. |
| **Allow session image uploads** | Users can attach pictures. Needs a default vision deployment. |
| **Largest document that can be indexed** | The longest document, in characters of text, a user can attach. Bigger documents are refused when attached. **Use site default** keeps the site's limit; 0 means no limit. |
| **Describe figures in uploaded documents** | Whether the AI describes charts and pictures inside attached documents, so it can answer about them. This uses a vision model and costs more. **Use site default**, **Describe figures** or **Do not describe figures**. |

Attached files belong to one chat only. How users attach them is described in [Chat with an AI assistant](chat.md#attach-files-to-a-chat).

## Data sources

<YouTube video="ai-data-sources" title="Video overview of data sources" />

The video shows how a content index, a knowledge base index and a data source fit together, each step below, and a real answer with its source.

A data source connects the assistant to a knowledge base built from a search index or a database. Use it for large or changing content, such as all the articles on your site.

### Create a data source

<YouTube video="ai-data-sources" title="Video of indexing content and creating a data source" start={85} end={187} />

1. Open **Artificial Intelligence > Data Sources** and click **Add Data Source**.
2. In **Available Source Types**, click **Add** on the type you need:
   - **Search Index Profile**: an index of this site's own content.
   - **Elasticsearch** or **Azure AI Search**: an external index.
   - **PostgreSQL**: a database table.
   - **Files**: the target of a [file source](#file-sources).
   - **Web**: the target of a [web crawler](#web-crawlers).
3. Enter a **Name** and choose the **Destination index**, the AI Knowledge Base index that stores the passages. It cannot be changed after you save.
4. Fill in the source's own section, such as the **Source index**, or the connection details your technical team gives you for an external index or database. For an external source, **Use the globally configured connection** uses the connection your technical team set up.
5. For every type except **Files** and **Web**, fill in the **Field Mapping**: the **Content field** to search, an optional **Title field** for the source list, and a **Key field** that identifies each entry. Ask your technical team which fields to use.
6. Click **Save**.

The data source fills its knowledge base and keeps it in step with the source. To rebuild it by hand, open the data source's **Actions** menu and choose **Sync index**. When you pick the **Source index**, the **Content field**, **Title field** and **Key field** are filled in with sensible defaults, such as the full text of a content item, its display text and its content item id.

<YouTube video="ai-data-sources" title="Video of syncing a data source" start={187} end={207} />

### Use a data source in a profile

<YouTube video="ai-data-sources" title="Video of choosing a data source on a profile" start={207} end={244} />

On the profile's **Knowledge** tab:

| Field | What it does |
| --- | --- |
| **Data source** | The data source to answer from. **No data source** for none. |
| **Filter** | Searches only some entries, for example only active products. Ask your technical team for the expression. A filter that is not valid is not saved. A filter has no effect when the knowledge base is an Azure AI Search index. |
| **Restrict answers to retrieved data only** | The assistant answers only from the data source. When nothing matches, it says so instead of answering from general knowledge. Turn it on for a public assistant that must stay on topic. |
| **Strictness** | From 1 to 5. How closely a passage must match the question. Higher values use fewer, more relevant passages. Empty uses the site default (3). |
| **Retrieved documents** | From 3 to 20. How many passages are retrieved for each question. Empty uses the site default. |

The site defaults are under **Settings > Artificial Intelligence > Data Sources**: **Default strictness** and **Default top documents**.

<YouTube video="ai-data-sources" title="Video of an answer with its source and the site-wide defaults" start={244} end={287} />

With a data source chosen, an answer carries small numbers on the statements that come from your files, and a numbered list of sources under it.

<YouTube video="ai-file-sources" title="Video of an answer with its sources" start={335} end={354} />

## File sources

<YouTube video="ai-file-sources" title="Video overview of file sources" />

The video shows why uploads go stale, how the knowledge base index, a **Files** data source and a file source fit together, each step below, and a real answer with its source.

A file source reads a folder of files on a schedule and keeps a **Files** data source up to date, so nobody has to upload anything. Use it for documents another system exports, such as nightly product sheets on an FTP site.

1. Create a data source of type **Files** (see [Create a data source](#create-a-data-source)).

<YouTube video="ai-file-sources" title="Video of creating a Files data source" start={122} end={153} />

2. Open **Artificial Intelligence > File Sources** and click **Add File Source**.

<YouTube video="ai-file-sources" title="Video of adding a file source" start={153} end={223} />


3. In **Available Connectors**, click **Add** on **File system** (a folder on the site's server), **FTP / FTPS** or **SFTP**.
4. Fill in the fields below and the connection details, then click **Save**.

| Field | What it does |
| --- | --- |
| **Name** | The name shown in the list. |
| **Target File data source** | The **Files** data source that receives the content. |
| **Enabled** | Whether the scheduled reading runs. |
| **Re-read interval (minutes)** | How often the folder is read again. The site checks once an hour, so shorter intervals still run at most hourly. Empty uses the site default. |
| **Figures** | **Auto** (describe charts and pictures that look worth it), **All**, or **Off**. Describing figures needs a vision model and costs more. |
| **Figures described per document** | The most figures described in one document. |
| **Vision deployment** / **Utility deployment** | The models used to read the files. Leave them on the site defaults. |
| **Items per run** | The most files read in one run. |
| **Language** | The language of the files, such as `en-US`, when they all use one. |
| **Folder**, **Include sub-folders**, **Files per listing** | Which folder to read. For **File system**, the folder is inside the area your technical team set aside for this site. |

For FTP and SFTP, enter the **Host**, **Port**, **Username** and **Password** (or a **Private key** for SFTP) your technical team gives you. A stored password or key is never shown again; leave the field blank to keep it.

<YouTube video="ai-file-sources" title="Video of the FTP and SFTP connectors" start={252} end={303} />

The list shows each source's last run: how many files were found, ingested, unchanged, removed and failed, or **Never run**. To read a source right away, open its **Actions** menu and choose **Read now**.

<YouTube video="ai-file-sources" title="Video of reading the folder now" start={223} end={252} />

## Web crawlers

<YouTube video="ai-web-crawlers" title="Video overview of web crawlers" />

The video shows why copying pages goes stale, how the knowledge base index, a **Web** data source and a web crawler fit together, each step below, and an answer that links to its page.

A web crawler reads a public website, page by page, into a **Web** data source. Use it for your help center or product pages, so the assistant can answer from them and link to the page.

1. Create a data source of type **Web** (see [Create a data source](#create-a-data-source)).

<YouTube video="ai-web-crawlers" title="Video of creating a Web data source" start={108} end={137} />

2. Open **Artificial Intelligence > Web Crawlers** and click **Add Web Crawler**.

<YouTube video="ai-web-crawlers" title="Video of adding a web crawler" start={137} end={208} />

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

To read the site right away, open the crawler's **Actions** menu and choose **Synchronize now**. A new crawler also queues a first synchronization when you save it. Only new, changed and removed pages are processed. Sources in answers link back to the original page.

<YouTube video="ai-web-crawlers" title="Video of reading the website now" start={208} end={238} />

<YouTube video="ai-web-crawlers" title="Video of choosing the data source and asking a question" start={238} end={274} />

## How the assistant uses your knowledge

<YouTube video="ai-knowledge" title="Video of using knowledge well" start={326} end={371} />

- **Before each answer or on demand.** With **Enable preemptive retrieval-augmented generation (RAG)** on (under **Settings > Artificial Intelligence > Default Orchestrator**), the assistant searches your knowledge before every answer. With it off, the assistant searches when it decides it needs to. Preemptive search is always on for profiles without tools.
- **Sources.** Statements that come from your content get small numbers, and a numbered list of sources appears under the answer. Sources with a web address are links.
- **Staying on topic.** **Restrict answers to retrieved data only** keeps the assistant to your content.

:::tip[When answers are wrong or missing]
Check that the data source has finished syncing, that the passage really contains the answer, and try a lower **Strictness** or more **Retrieved documents**. If the assistant still answers from general knowledge, turn on **Restrict answers to retrieved data only** and tell it in the **System instructions** to answer only from the provided content.
:::
