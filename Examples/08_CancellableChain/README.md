# 08 · Cancellable Chain — `Break.AndStop` + `.Continue()`

## Scenario

An operation chain **Load → Validate → Process**. The validation step fails and must
cancel the **entire** chain — not just itself, but the parent that launched it. The
parent never reaches its final step.

## Feature

- **`Break.AndStop`** — breaks the current task **and** propagates the break up to the
  parent task that continued it via `.Continue()`.
- **`.Continue()`** — schedules a child iterator on the same runner and makes the parent
  **wait** for it (unlike `.Forget()`). The parent resumes after the child completes,
  *unless* the child returns `Break.AndStop`.

## When / Why to use it

- Pipelined operations where a failure in one stage must abort the whole pipeline.
- Validation / precondition chains where "stop everything now" is the desired semantics.
- Anytime a child failure should cancel the parent's remaining work.

## How it works

1. `Parent` yields `Chain().Continue()`. The parent suspends, waiting on a
   `Continuation`.
2. `Chain` yields `LoadStep().Continue()`, runs it, then yields
   `ValidateStep().Continue()`.
3. `ValidateStep` runs, decides the data is bad, and yields
   `TaskContract.Break.AndStop`.
4. The runner's `SveltoTaskWrapper` sees `breakMode == Break.AndStop` on the completing
   child and returns `StepState.Completed` **without** advancing the parent — the
   parent's continuation is treated as done *and* the parent itself is marked completed.
5. `ProcessStep` is never reached. `Parent`'s final `yield return 42` is never reached.

### Chain diagram

```
┌────────┐    ┌──────────┐    ┌─────────┐
│  LOAD  │───▶│ VALIDATE │───▶│ PROCESS │
└────────┘    └──────────┘    └─────────┘
                   │
                   │ Break.AndStop
                   ▼
              💥 CHAIN SNAPS
                   │
                   ▼ propagation
              PARENT cancelled (never reaches final step)
```

## Key concepts

| Type / API | Purpose |
|---|---|
| `Break.AndStop` | Break self **and** the parent that did `.Continue()`. |
| `Break.It` | Break self only; parent continues. (Used for pooling — see Example 07.) |
| `.Continue()` | Schedule child on same runner; parent waits. |
| `.Complete(ms)` | Run an `IEnumerator<TaskContract>` synchronously to completion (uses a thread-local `SyncRunner`). |
| `TaskContract.Yield.It` | Yield one step. |

## Gotchas

- **`Break.AndStop` propagates to the parent; `Break.It` does NOT.** `Break.It` only
  ends the current iteration and the parent resumes normally. Choose carefully.
- **`yield break` does NOT propagate either.** It simply ends the current enumerator;
  the parent continues as if the child completed normally.
- `Break.AndStop` only works with `.Continue()` (same-runner parent/child). It does
  **not** work with `.RunOn(otherRunner)` — the parent has no way to be notified back.
- The parent's `Current` after cancellation is **not** the value it would have yielded
  at the end; the final `yield return 42` is skipped entirely.
- `.Complete()` runs on a thread-local `SyncRunner`; it blocks the calling thread until
  the task finishes (or times out).