---
description: "Use when: adding tests, running verification, preparing release gates, or recording smoke/lab evidence"
applyTo: "**/*test*,tests/**/*,.github/workflows/**/*,frontend/**/*,backend/**/*"
---

# Testing Instructions

- Read `AI_BOARD.md`; `test-ai` must record commands, pass/fail results, and skipped reasons.
- Upstream .NET verification: `dotnet restore`, `dotnet build --configuration Debug`, `dotnet test`.
- Release-sensitive .NET changes require Release build verification.
- Frontend verification once present: install, lint, build, and browser smoke.
- Backend verification once present: API health, queue/worker smoke, generated output path smoke.
- Firmware claims require actual RP2040/RP2350 or ESP32-S3 build artifacts.
- If tools are unavailable locally, mark the test as blocked with the missing tool and next verification environment.
