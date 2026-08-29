---
name: commit
description: Commit pending changes as small atomic commits — one commit per group of related files, each with its own Conventional Commits message. Use whenever the user asks to commit ("commit", "commitar", "faz o commit", "salva no git") or when the working tree has multiple unrelated changes. Never commit everything together in a single commit.
---

# Atomic commits

Commit the working tree as a series of small, focused commits instead of one large commit.

## Rules

1. **Never `git add -A` / `git add .` into a single commit.** Always stage explicit paths.
2. Run `git status --porcelain` and `git diff` (and `git diff --stat` for scale) to see every pending change before deciding anything.
3. **Group files by concern**, not by directory alone. Typical groups:
   - documentation (`README.md`, `CLAUDE.md`, docs/)
   - build/tooling/config (`.gitignore`, `docker-compose.yml`, CI, scripts)
   - one feature or module of source code per commit (e.g. `src/CameraVision/Tracking/**`)
   - data/fixtures
   - Claude Code assets (`.claude/skills/**`)
4. A file goes in exactly one commit. If one file contains two unrelated changes, use `git add -p` to split hunks when practical; otherwise put it with the group it most belongs to and mention the extra change in the body.
5. **Order commits** so each one leaves the repo consistent: config/tooling first, then core code, then dependents (clients, docs referencing new behavior last is fine).

## Messages

- Conventional Commits, in English: `type(scope): summary` — types `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `perf`.
- Summary ≤ 72 chars, imperative mood, describes only the files in that commit.
- Add a short body when the "why" is not obvious from the summary.
- End every message with:

  ```
  Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
  ```

## Procedure

For each group, in order:

```powershell
git add <explicit paths for this group>
git commit -m "<message for exactly these files>"
```

Finish by showing `git log --oneline` of the new commits and confirming `git status` is clean (or listing what was intentionally left uncommitted).
