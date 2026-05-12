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

All backend logic lives in a single ~560-line file using .NET 8 minimal APIs:

- **`DataStore` class** — thread-safe JSON file persistence (SemaphoreSlim) for conversations (`data/conversas.json`), stats (`data/stats.json`), and the editable system prompt (`data/context_prompt.txt`).
- **Ollama integration** — POSTs to `localhost:11434`. Tries `v1/completions` first, falls back to `api/generate`. Supported models: Granite 4.1, Gemma 4, Llama 3.1, DeepSeek R1.
- **Prompt assembly** — system prompt (Portuguese) + editable context + full conversation history are concatenated before each request.
- **Automatic title generation** — first user message is used as the conversation title.

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
| POST | `/api/stats/update` | Increment message count / thinking time |

### Frontend — [publish/wwwroot/](publish/wwwroot/)

- [index.html](publish/wwwroot/index.html) — layout with sidebar (conversation list, search, actions), main chat area, model selector, and modals (stats, confirmations).
- [script.js](publish/wwwroot/script.js) — ~488 lines of vanilla JS. Manages conversation state, sends fetch requests to the backend, renders Markdown via marked.js (CDN), and tracks per-session stats.
- [style.css](publish/wwwroot/style.css) — all styling.

> The `publish/wwwroot/` directory is the compiled static file root. During development, `dotnet run` serves from this path directly via `UseStaticFiles()`.

### SQL Server migration (optional)

The `sql/` directory contains an alternative data layer (`DataStore_SQL.cs`, `Program_SQL_Example.cs`) and migration scripts for replacing JSON file storage with SQL Server. Not active by default — see `sql/DATABASE_README.md` for integration steps.

## Key Constraints

- Ollama must be running on `localhost:11434` before starting the app.
- Data files are created on first run inside `data/` relative to the working directory (important when deploying to IIS — the app pool identity needs write access to that folder).
- CORS is open to all origins (`AllowAnyOrigin`) — restrict this before any public deployment.
