# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Portuguese-language AI virtual assistant web application for Nersant (Torres Novas, Portugal). Built with ASP.NET Core 8 (minimal APIs) on the backend and vanilla HTML/CSS/JS on the frontend, with Ollama as the local LLM inference engine.

## Commands

**Build:**
```bash
dotnet build AssistenteVirtualIA.csproj
```

**Run (development):**
```bash
dotnet run --project AssistenteVirtualIA.csproj
# Serves on http://localhost:5000
```

**Publish for IIS:**
```bash
dotnet publish AssistenteVirtualIA.csproj -c Release -o publish
```

No test suite or linter is configured.

## Architecture

### Backend — [src/Program.cs](src/Program.cs)

All backend logic lives in a single file using .NET 8 minimal APIs:

- **`DataStore` class** — thread-safe JSON file persistence (SemaphoreSlim) for conversations (`data/conversas.json`), stats (`data/stats.json`), and the editable system prompt (`data/context_prompt.txt`). Single-instance design: `CurrentConversation` is shared in-memory state, so concurrent users would conflict.
- **Ollama integration** — POSTs to `localhost:11434`. Tries `v1/chat/completions` (OpenAI-compatible) first; if the model isn't found or the request fails, falls back to `api/chat` (native Ollama format).
- **Prompt assembly** — `BuildMessages()` constructs the message list: a system message combining the hardcoded Portuguese system prompt + editable context + first/subsequent-message instructions, then the full conversation history.
- **Automatic title generation** — first user message (truncated to 30 chars) becomes the conversation title.
- **Session tracking** — ASP.NET session (60-min timeout) stores `ActiveConversationId`; `request.ConversationId` from the frontend takes precedence over the session value.

**API surface:**

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/chat` | Send message to Ollama, persist reply |
| POST | `/api/new-chat` | Create new conversation |
| GET | `/api/history` | List all conversations |
| GET | `/api/conversation/{id}` | Get single conversation |
| DELETE | `/api/conversation/{id}` | Delete conversation |
| POST | `/api/clear-history` | Wipe all conversations |
| GET/POST | `/api/context-prompt` | Read/update editable system prompt |
| GET | `/api/stats` | Global usage statistics |
| POST | `/api/stats/update` | Increment message count / thinking time (exists but not called by the frontend) |

### Frontend — [publish/wwwroot/](publish/wwwroot/)

- [index.html](publish/wwwroot/index.html) — layout with sidebar (conversation list, search, actions), main chat area, model selector, and modals (stats, confirmations). Supported models in the selector: Qwen 2.5, Granite 4.1, Gemma 4, Llama 3.1, DeepSeek R1, GPT-OSS, Qwen 3.6 Vision.
- [script.js](publish/wwwroot/script.js) — vanilla JS. Manages conversation state, sends fetch requests to the backend, renders Markdown via marked.js (CDN). Stats (`totalThinkingTime`, `messagesSentCount`) are in-memory only and reset on page refresh.
- [style.css](publish/wwwroot/style.css) — all styling.

> The `publish/wwwroot/` directory is the static file root. During development, `dotnet run` serves from this path via `UseStaticFiles()` with `ContentRootPath` pointing to the project root.

### SQL Server migration (optional)

The `sql/` directory contains an alternative data layer (`DataStore_SQL.cs`, `Program_SQL_Example.cs`) and migration scripts for replacing JSON file storage with SQL Server. These files are explicitly excluded from compilation in the `.csproj`. Not active by default — see `sql/DATABASE_README.md` for integration steps.

## Key Constraints

- Ollama must be running on `localhost:11434` before starting the app.
- Data files are created on first run inside `data/` relative to the working directory (important when deploying to IIS — the app pool identity needs write access to that folder).
- CORS is open to all origins (`AllowAnyOrigin`) — restrict this before any public deployment.
- The app is designed for single-user use; `DataStore.CurrentConversation` is shared in-memory state with no per-user isolation.
