# CHANGES

## 2026-06-06

- Initialized TomodachiDrawer-CN working tree from upstream `Lucas7yoshi/TomodachiDrawer`.
- Added AI collaboration documentation: `AGENTS.md`, `MEMORY.md`, `AI_BOARD.md`, `.ai/`, Copilot adapter, and Cursor adapter.
- Added CN initialization README, NOTICE, and GPL compliance notes.
- Added minimum testable CN skeleton: React/Vite frontend, ASP.NET Core API, Worker service, Dockerfiles, and Docker Compose.
- Added placeholder upload/job/download API flow for deployment smoke testing before real `TomodachiDrawer.Core` generation is wired in.
- Verified the minimum API/Worker path on the server with .NET 10 SDK, including health, upload, job creation, and `.tdld` download smoke.
- Restyled the frontend as an original cozy friends/dream-life workshop UI without Nintendo assets.
- Deployed the MVP through systemd and Nginx at `/tomodachi/` on the public server IP.
