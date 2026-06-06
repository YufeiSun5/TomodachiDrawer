---
description: "Use when: working on ASP.NET Core API, Worker, queue, storage, database, DTOs, CLI integration, or deployment"
applyTo: "backend/**/*,cli/**/*,docker/**/*,docker-compose*.yml,*.slnx,*.csproj"
---

# Backend Instructions

- Read `AI_BOARD.md` before changing APIs, DTOs, worker behavior, queue semantics, file paths, or deployment config.
- Reuse `TomodachiDrawer.Core` for drawing logic; do not reimplement image quantization or routing in the web layer.
- Keep API contracts, DTOs, config keys, database names, and logs in English.
- Uploaded files must be MIME-checked, size-limited, re-encoded, and stored with server-generated paths.
- Planned Redis keys must use the `tomodachi:` prefix.
- Planned PostgreSQL database must be separate from any existing 20k project database.
- Server deployment must add isolated services only; do not modify existing 20k service directories or compose files.
