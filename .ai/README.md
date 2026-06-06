# .ai

This directory is the source of truth for AI collaboration material that is too detailed for root `AGENTS.md`.

## Structure

- `instructions/`: executable domain rules.
- `docs/`: stable documents, closed records, stage summaries, and archives.
- `skills/`: reusable project-specific procedures.
- `agents/`: custom AI role definitions.
- `prompts/`: task entry prompts.

## Create New Files When

- The content has a clear recurring use.
- The file belongs to one stable boundary.
- The root `AGENTS.md` would become too large if it contained the detail.

## Do Not Create Files When

- The content is an active open/blocked task; use root `AI_BOARD.md`.
- The content contains secrets or private local deployment credentials.
- The content duplicates another file without adding a useful entry point.
