---
sidebar_label: AI File Sources
sidebar_position: 20
title: AI File Sources
description: Ingest a folder of files on a schedule into a File AI data source, with local file system, FTP, and SFTP connectors.
---

# AI File Sources

| | |
| --- | --- |
| **Feature Name** | AI File Sources |
| **Feature ID** | `CrestApps.OrchardCore.AI.FileSources` |

A **file source** reads a folder on a schedule and ingests every file it finds into a **File** AI data
source. Each file's text, figures, charts, and tables are stored as separately retrievable knowledge, so an
AI profile attached to that data source can answer from the folder's contents and cite the figure it used.

Where [AI Documents](./documents/) covers files a user uploads through a chat surface, a file source covers
files that already live somewhere — a folder on the server, an FTP drop, an SFTP export — and keeps the
knowledge base in step with them without anyone uploading anything.

## Enable the features

1. Enable **AI File Sources** (`CrestApps.OrchardCore.AI.FileSources`). It brings **AI Data Sources** and
   **AI Documents for Chat Interactions** with it, and ships the local file-system connector.
2. Optionally enable a remote connector:
   - **AI File Sources - FTP** (`CrestApps.OrchardCore.AI.FileSources.Ftp`) for FTP and FTPS.
   - **AI File Sources - SFTP** (`CrestApps.OrchardCore.AI.FileSources.Sftp`) for SFTP.

Enabling a connector adds its own section to the file source editor; it does not start an MCP server. The
separate [MCP FTP](./mcp/ftp) and [MCP SFTP](./mcp/sftp) resource features are unrelated to ingestion.

Managing file sources requires the **Manage file sources** (`ManageFileSources`) permission. The screens live
under **Artificial Intelligence → File Sources**.

## Create a file source

1. Create a **File** data source under **Artificial Intelligence → Data Sources**. A file source has to
   target one, and the editor says so when none exists.
2. Go to **Artificial Intelligence → File Sources** and add a source. Every connector shares these fields:

   | Field | Purpose |
   | --- | --- |
   | **Name** | The display name of the source. |
   | **Data source** | The File AI data source that receives the ingested knowledge. |
   | **Enabled** | Whether the scheduled run picks this source up. |
   | **Re-index interval (minutes)** | How often the source is re-read. Empty uses the host default (`DefaultRunIntervalMinutes`). |
   | **Figure mode** | `Auto` describes a figure when it looks worth describing, `All` describes every figure that is kept, and `Off` ignores figures entirely. |
   | **Vision deployment** | The deployment that describes figures. Empty uses whatever fills the site's `vision` slot. |
   | **Utility deployment** | The deployment that answers the utility prompts ingestion runs. Empty uses the site's `utility` slot. |
   | **Max figure descriptions per document** | Ceiling on how many figures one document may have described (default `25`). |
   | **Max items per run** | The most files one run may read. Empty uses the host default. |
   | **Language** | The BCP-47 language tag the corpus is written in, when it is known and uniform. |

3. Fill in the connector section, then save.

:::note
Figures are only described when a deployment that declares the `imageInput` capability fills the **vision**
slot (or is named on the source). Without one, a file's figures are stored but never described. See
[Model Capabilities](model-capabilities.md).
:::

## The local file system connector

The file-system connector reads a folder on the server. To keep one tenant from reaching another tenant's
files — or any file the host process can open — the folder it may read is **fixed per tenant**:

```
App_Data/Sites/{TenantName}/file-sources
```

That path is computed from the tenant's own identity. It is never read from configuration and never taken
from a request, so a tenant administrator cannot widen their own reach by editing either.

| Field | Purpose |
| --- | --- |
| **Root path** | The folder to read, **relative to the tenant's `file-sources` folder**. Empty means that folder itself. |
| **Recursive** | Whether sub-folders are read too (default on). |
| **Max items** | The most files one listing will take on. Empty uses the host default. |

Containment is decided on canonical, fully-resolved paths, so a relative path that climbs out of the tenant
folder is rejected rather than followed.

## FTP and SFTP connectors

Enabling **AI File Sources - FTP** or **AI File Sources - SFTP** adds a connector section for a folder on a
remote server. Neither connector is confined to the tenant folder, because the remote folder is already
scoped by the credentials you supply.

Both connectors share the remote folder fields:

| Field | Default | Purpose |
| --- | --- | --- |
| **Remote root path** | `/` | The folder on the server to read. |
| **Remote recursive** | on | Whether sub-folders are read too. |
| **Remote max items** | host default | The most files one listing will take on. |

**FTP** adds the host, port (21 when empty), username, password, encryption mode, data connection type, an
*accept any certificate* switch, connect and read timeouts in milliseconds, and a retry count.

**SFTP** adds the host, port, username, and either a password or a private key with an optional passphrase,
plus optional proxy settings, a connection timeout, and a keep-alive interval.

A stored password, private key, or passphrase is never sent back to the editor. Leaving the field blank on
save keeps the stored value; type a new one to replace it.

Both feed the same ingestion pipeline as the local connector, so the shared fields above behave identically.

## Scheduling

An hourly Orchard background task, **File Source Ingestion**, evaluates every enabled source and runs the
ones whose interval has elapsed. It runs per tenant, so a source only ever reads and writes within the
tenant that owns it.

Because the task fires hourly, a re-index interval shorter than 60 minutes does not make a source run more
often than once an hour.

You can also run a source immediately from its entry on the **File Sources** list, which ignores the
interval.

## Configuration

The effort knobs bind from the `CrestApps:AI:FileSources` shell configuration section. In the application's
root `appsettings.json` that section sits under the `OrchardCore` key:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "FileSources": {
          "MaxItemsPerRun": 200,
          "MaxConcurrentFetches": 2,
          "RunCheckIntervalMinutes": 15,
          "DefaultRunIntervalMinutes": 1440
        }
      }
    }
  }
}
```

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `MaxItemsPerRun` | `int` | `200` | The most items a single run may take on when the source does not set its own limit. |
| `MaxConcurrentFetches` | `int` | `2` | How many files are fetched at a time. |
| `RunCheckIntervalMinutes` | `int` | `15` | Poll interval of the framework's own hosted scheduler. **It has no effect under Orchard Core**, where the hourly background task drives the schedule instead. |
| `DefaultRunIntervalMinutes` | `int` | `1440` | The re-index interval used by a source that names none. |

:::warning
`AllowedLocalRoots` is deliberately **not** configurable per tenant. Whatever configuration supplies is
discarded and replaced with the single tenant folder described above, because a host-wide allow-list is the
wrong shape for a multi-tenant site: a tenant administrator who can edit their own tenant's configuration
would otherwise be able to widen their own reach.
:::

:::note
A tenant that overrides these values in its own `App_Data/Sites/{tenant}/appsettings.json` writes the same
keys **without** the `OrchardCore` wrapper, starting at `CrestApps`, because that file is already scoped to
the tenant.
:::

## Related

- [AI Data Sources](./data-sources/) — the data source the ingested knowledge lands in.
- [AI Documents](./documents/) — user-uploaded files on a chat surface.
- [Model Capabilities](model-capabilities.md) — the `vision` and `utility` slots ingestion draws on.
