---
sidebar_label: Unreleased
sidebar_position: 1
title: Unreleased
description: Changes that are available in the current development branch and not yet included in a tagged release.
---

# Unreleased

## Documentation

- Added a new **Framework** documentation section that documents each framework feature independently — covering core services, AI core, orchestration, chat, document processing, AI templates, tools, response handlers, context builders, SignalR, data storage, providers, data sources, MCP, and A2A.
- Each framework doc follows a consistent structure: quick start extension method, problem & solution, core interfaces, configuration, and implementation examples.
- Added an [MVC Example walkthrough](../framework/mvc-example.md) that documents all 12 sections of the `CrestApps.Mvc.Web` startup configuration.
- Updated the Introduction and Getting Started pages to present both Framework (standalone .NET) and Orchard Core paths.
- Updated the AI Suite index to reference framework docs for core concepts (orchestration, context builders, tools, etc.).
- Created `AddCrestAppsMcpClient()` and `AddCrestAppsMcpServer()` DI extensions that bundle previously scattered MCP service registrations into two cohesive calls.
- Cleaned up `CrestApps.Mvc.Web/Program.cs` with comprehensive section documentation aligned with the new framework docs.

## AI

- Restored shared framework-level A2A client support through a new `CrestApps.AI.A2A` project so Orchard Core and `CrestApps.Mvc.Web` now consume the same A2A connection models, authentication services, agent-card cache, tool registry provider, and discovery functions.
- Added MVC admin CRUD management for A2A host connections plus A2A capability selectors on AI profiles, AI profile templates, and chat interactions so stored connections flow into orchestration and remote A2A agents can be selected without duplicating MVC-only A2A service layers.
- Moved `SearchDocumentsTool`, `DataSourceSearchTool`, the default plain-text document reader, and the default AI document processor into the shared framework so Orchard Core and the MVC sample use the same host-agnostic document/data-source search pipeline.
- Added MVC JSON-backed settings for the default **AI Documents** index and default retrieval behavior, then updated MVC document uploads to process, embed, and index document chunks into that configured search index instead of only storing raw uploads.
- Moved the OpenXml and PDF ingestion readers into the shared framework so MVC and Orchard Core use the same document extraction path for embeddable uploads.
- Added MVC admin management for MCP connections, prompts, and resources, backed by shared framework MCP client/server services instead of MVC-specific protocol implementations.
- Added shared framework MCP FTP/SFTP resource metadata, handlers, and SSE/OAuth client plumbing so Orchard Core and MVC can expose the same MCP protocol capabilities with host-specific UI only.
- Updated the MVC admin experience with the requested dashboard section split, capability-oriented profile selection surfaces, persisted MCP server settings, and the A2A authentication form fix that reveals the correct fields per authentication mode.
- Replaced the MVC `ChatApiController` with three minimal API endpoints and aligned the sample app with the Orchard Core endpoint-style bootstrapping pattern.
- Moved chat interaction and chat session document upload/remove endpoints into the shared framework chat layer, added host-pluggable authorization and event hooks, and updated Orchard Core plus MVC to reuse the same HTTP contract.
- Updated the MVC chat UIs so chat interactions upload/remove knowledge documents immediately via AJAX, AI chat sessions expose session document uploads only when the profile enables them, AI Template list buttons match the AI Connections styling, and the MCP Hosts list no longer shows the redundant Details column.
