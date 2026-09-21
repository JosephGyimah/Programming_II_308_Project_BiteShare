# Contributing to BiteShare

This is the team's one-pager on conventions, so 12 people's code doesn't diverge. Keep it short; add to it only when a real conflict comes up.

## Branching

- `main` is protected — no direct pushes.
- One feature branch per Jira ticket: `feature/<ticket-id>-short-description`, e.g. `feature/BS-42-cart-signalr-hub`.
- Bug fixes: `fix/<ticket-id>-short-description`.
- Rebase or merge `main` into your branch before opening a PR, don't let it drift.

## Commit messages

```
<ticket-id>: <short, imperative summary>

<optional body — what changed and why, not what's obvious from the diff>
```

Example: `BS-42: add OrderHub broadcast for cart-add events`

## Pull requests

- PR review required, **2 approvals minimum** before merging to `main`.
- Use the PR template (`.github/PULL_REQUEST_TEMPLATE.md`) — link the Jira ticket, describe what changed, note how it was tested.
- Keep PRs scoped to one ticket. If it's sprawling, split it.
- If your change touches shared models in `BiteShare.Shared` or the `OrderHub` contract, flag it in the team channel **before** merging — Streams A/B/C all depend on these, and a silent breaking change costs everyone else time.

## Coding standards

- **Naming:** PascalCase for classes/methods/properties, camelCase for locals/parameters, `_camelCase` for private fields. Interfaces prefixed `I`.
- **Async:** suffix async methods with `Async`; always accept a `CancellationToken` on API-facing async methods where feasible.
- **DTOs vs entities:** never return EF Core entities directly from API endpoints — map to a DTO in `BiteShare.Shared`.
- **Nullable reference types** are enabled solution-wide — don't suppress warnings with `!` without a comment explaining why it's safe.
- **Formatting:** run `dotnet format` before pushing (a pre-commit hook or CI check will catch anything missed).

## Tests

- New business logic (especially anything in the cost-splitter) needs a unit test in the same PR.
- Don't merge with a red CI build.

## Ticket sizing

Tickets should be sized to 1–3 days. If a ticket is bigger than that, break it down in Jira before starting.

## Scope discipline

With 12 people, the biggest risk isn't lack of manpower — it's each sub-team over-building their own feature. If you're adding something not in the ticket's acceptance criteria, flag it to the Scrum Master / Project Lead before building it, don't just build it.
