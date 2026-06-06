# GPL Compliance Notes

TomodachiDrawer-CN is derived from `Lucas7yoshi/TomodachiDrawer`, which is licensed under GPL-3.0.

## Required Rules

1. Keep `LICENSE` in the repository and in distributed release packages.
2. Keep upstream attribution in `NOTICE.md`, README files, and user-facing download pages.
3. Keep `CHANGES.md` updated for CN-specific changes.
4. Keep `THIRD_PARTY_LICENSES.md` updated when dependencies change.
5. If `.uf2`, firmware, desktop clients, CLI binaries, API/Worker images, or other modified binaries are distributed, provide the corresponding source code.
6. Footer and download pages must link to the source, upstream project, license, and change notes.
7. Do not include Nintendo official logos, UI assets, or copyrighted game assets in the web UI unless a clear license allows it.

## Suggested Footer Text

```text
本项目基于 TomodachiDrawer 构建，采用 GPL-3.0 许可证。
本项目衍生源码可在 GitHub 获取。
本项目与 Nintendo 无官方关联。
```

## Source Release Checklist

- `LICENSE` present.
- `NOTICE.md` present.
- `CHANGES.md` present and current.
- `THIRD_PARTY_LICENSES.md` present and current.
- Build instructions are present for every distributed artifact.
- No private deployment secrets are included.
