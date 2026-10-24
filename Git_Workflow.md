# MoneyBoard — Git Workflow

This document defines how Git and GitHub are used for the MoneyBoard project.

## Branch Structure

MoneyBoard uses three permanent branches:

| Branch | Purpose | Stability |
|---|---|---|
| `master` | Production / stable version | Highest |
| `staging` | Pre-production testing | High |
| `dev` | Active development | Changes frequently |

### `master` — Production

`master` is the **stable, working version** of MoneyBoard.

Rules:

- Do not develop directly on `master`.
- Only merge code into `master` after it has passed testing in `staging`.
- `master` should always represent a version that is safe to treat as the current production release.
- Avoid experimental changes here.

### `staging` — Pre-production

`staging` is the **testing/release-candidate environment**.

Rules:

- Changes arrive here from `dev`.
- Test the complete application here before promoting it to `master`.
- Database changes, API changes, frontend changes and integrations should be tested here.
- Do not use `staging` as the normal development branch.

### `dev` — Active Development

`dev` is where normal MoneyBoard development happens.

Rules:

- New features and fixes are developed here.
- It is acceptable for `dev` to be incomplete or temporarily unstable.
- Test changes locally before moving them to `staging`.

---

## Normal Development Flow

The normal promotion path is:

```text
dev
 ↓
staging
 ↓
master