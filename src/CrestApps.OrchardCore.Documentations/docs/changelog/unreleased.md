---
sidebar_label: Unreleased
sidebar_position: 1
title: Unreleased
description: Changes that are available in the current development branch and not yet included in a tagged release.
---

# Unreleased

## AI

- Restored shared framework-level A2A client support through a new `CrestApps.AI.A2A` project so Orchard Core and `CrestApps.Mvc.Web` now consume the same A2A connection models, authentication services, agent-card cache, tool registry provider, and discovery functions.
- Added MVC admin CRUD management for A2A host connections plus A2A capability selectors on AI profiles, AI profile templates, and chat interactions so stored connections flow into orchestration and remote A2A agents can be selected without duplicating MVC-only A2A service layers.
- Moved `SearchDocumentsTool`, `DataSourceSearchTool`, the default plain-text document reader, and the default AI document processor into the shared framework so Orchard Core and the MVC sample use the same host-agnostic document/data-source search pipeline.
- Added MVC JSON-backed settings for the default **AI Documents** index and default retrieval behavior, then updated MVC document uploads to process, embed, and index document chunks into that configured search index instead of only storing raw uploads.
- Moved the OpenXml and PDF ingestion readers into the shared framework so MVC and Orchard Core use the same document extraction path for embeddable uploads.
- Added MVC admin management for MCP connections, prompts, and resources, backed by shared framework MCP client/server services instead of MVC-specific protocol implementations.
- Added shared framework MCP FTP/SFTP resource metadata, handlers, and SSE/OAuth client plumbing so Orchard Core and MVC can expose the same MCP protocol capabilities with host-specific UI only.
- Updated the MVC admin experience with the requested dashboard section split, capability-oriented profile selection surfaces, persisted MCP server settings, and the A2A authentication form fix that reveals the correct fields per authentication mode.
