---
description: "Use when: changing licensing, release packages, download pages, footer text, dependencies, firmware, CLI, or web distribution"
applyTo: "README*,NOTICE.md,CHANGES.md,THIRD_PARTY_LICENSES.md,Docs/**/*,frontend/**/*,backend/**/*,TomodachiDrawer.UI.Avalonia/**/*"
---

# GPL Compliance Instructions

- Keep upstream GPL-3.0 license and attribution intact.
- Update `CHANGES.md` for CN-specific modifications.
- Update `THIRD_PARTY_LICENSES.md` when adding dependencies.
- Distributed binaries, firmware, Docker images, or hosted derivative source must have corresponding source availability.
- Download pages and footers must link to source, upstream project, license, and changes.
- Do not include private deployment secrets or credentials in compliance docs.
- Read `AI_BOARD.md` if a compliance issue blocks release.
