# Project Guidelines

## Project Summary

TomodachiDrawer is a GPL-3.0 toolset that generates controller input data for drawing arbitrary images in Tomodachi Life on Switch/Switch 2. This working tree is being initialized for the TomodachiDrawer-CN plan: a domestic web tool site that reuses upstream core drawing logic while adding upload, queued generation, download, gallery, and admin review flows.

## Technology Stack

| Area | Stack |
| --- | --- |
| Upstream app | C# / .NET 10, Avalonia UI |
| Core drawing | `TomodachiDrawer.Core`, SkiaSharp, Google OR-Tools, JeremyAnsel.ColorQuant |
| Firmware | RP2040/RP2350 Pico SDK, ESP32-S3 ESP-IDF v6.0 |
| Planned CN frontend | React, Vite, TypeScript, i18n, Tailwind CSS or local components |
| Planned CN backend | ASP.NET Core Web API, Worker Service, Redis/Hangfire, PostgreSQL |
| Delivery | GitHub Actions, Docker Compose/Nginx for CN deployment |

## Core Modules

| Module | Responsibility |
| --- | --- |
| `TomodachiDrawer.Core/` | Image processing, color matching, route planning, `.tdld` controller data generation. |
| `TomodachiDrawer.UI.Avalonia/` | Cross-platform desktop UI, RP2040/RP2350 and ESP32-S3 flashing flows. |
| `TomodachiDrawer.Firmware/` | RP2040/RP2350 firmware build targets. |
| `TomodachiDrawer.Firmware.ESP32S3/` | ESP32-S3 firmware and board variants. |
| `TomodachiDrawer.SerialPlayer/` | Serial playback utilities. |
| `frontend/` | Planned CN web UI. Create only when implementing the web MVP. |
| `backend/` | Planned CN API, worker, contracts, and infrastructure. Create only when implementing the web MVP. |

## Core Conventions

1. Preserve GPL-3.0 license files, upstream attribution, and third-party notices.
2. Do not rewrite `TomodachiDrawer.Core` algorithms unless a tested upstream-compatible change is necessary.
3. CN web work must keep local generation and server generation clearly separated.
4. Public gallery content must require admin approval before display.
5. User uploads must be size-limited, type-checked, re-encoded, and path-safe.
6. Deployment must not modify any existing `20k-days-resonance` server assets.
7. Formal frontend user text must go through i18n resources once the frontend exists.
8. Never commit local deployment secrets, SSH passwords, production `.env` files, or private plan files.

## Required Reading

- `MEMORY.md`
- `AI_BOARD.md`
- `.ai/instructions/ai-workflow.md`
- `.ai/instructions/gpl-compliance.md`
- Relevant domain instruction under `.ai/instructions/`

## On-Demand Resources

| Resource | Use when |
| --- | --- |
| `.ai/docs/` | Stable architecture, compliance, deployment, and closed stage records. |
| `.ai/skills/` | Reusable project-specific procedures after they become proven and repeated. |
| `.ai/agents/` | Role definitions for specialized AI agents. |
| `.ai/prompts/` | Task entry prompts for common workflows. |

## AI Identity Model

| Identity | Scope |
| --- | --- |
| `frontend-ai` | Frontend UI, upload flow, gallery/admin pages, i18n, API wrappers. |
| `backend-ai` | API, worker, queue, storage, database, DTOs, deployment config. |
| `test-ai` | Unit tests, smoke tests, E2E, lab verification, release gates. |
| `review-ai` | Architecture review, risk review, compliance review, cross-module checks. |

## Mandatory Workflow

1. Before editing, read `MEMORY.md` and `AI_BOARD.md`.
2. Before acting, state the active identity in progress notes or final response.
3. Add or update `AI_BOARD.md` for open/blocked cross-role work.
4. After material changes, update `MEMORY.md`.
5. API, DTO, error semantics, queue behavior, page scope, and deployment boundary changes must be reflected in the board and stable docs.
6. Final replies must state identity, handled board items, verification, and any open/blocked items.

## Test Gate

- Upstream .NET: `dotnet restore`, `dotnet build --configuration Debug`, `dotnet test`.
- Release-sensitive .NET: repeat in `Release` when packaging or touching shared logic.
- Firmware: use CI or platform toolchains for RP2040/RP2350 and ESP32-S3; do not claim firmware validation without build artifacts.
- CN frontend: `npm install`, `npm run lint`, `npm run build`, and a browser smoke test when UI exists.
- CN backend: `dotnet test`, API health smoke test, worker queue smoke test, and file output smoke test when services exist.
- Deployment: `docker compose config`, service health checks, `nginx -t`, and isolation checks from existing 20k services.

## Language

- Code, config keys, protocol fields, API paths, database names, and logs: English.
- User-facing CN site docs and UI copy: Chinese by default, with i18n-ready English/Japanese where applicable.
- AI collaboration docs may use Chinese for operational clarity while preserving exact code identifiers in English.
