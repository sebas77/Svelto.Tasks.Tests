# Svelto.Tasks 2.0 (Preview): from zero to practical usage

> **Draft for sebaslab.com (WordPress-ready)**<br>
> Suggested slug: `svelto-tasks-2-0-from-zero`<br>
> Suggested excerpt: *A practical introduction to Svelto.Tasks 2.0 for readers new to Svelto: what it is, why it exists, and how to use runners, continuations, and flow modifiers with concrete examples.*

> **Preview disclaimer**<br>
> At the time of writing, **Svelto.Tasks 2.0 is in preview state**, so APIs and examples may evolve over time.

---

## Why this article exists

If you already know Unity coroutines or `.NET Task`, this article explains where **Svelto.Tasks** fits and why you may want to use it.

If you **don’t** know Svelto at all, this is your starting point:

- what Svelto.Tasks is,
- what a task and a runner are,
- how to run tasks on main thread or worker threads,
- how to synchronize tasks without callback hell,
- how to shape execution with flow modifiers.

The examples are intentionally incremental so the concepts build one on top of the other.

---

## What is Svelto.Tasks?

Svelto.Tasks is a **platform-agnostic asynchronous task library** built around `IEnumerator`.

Historically, one core motivation was: *run coroutine-style async logic from anywhere, without being tied to MonoBehaviours/GameObjects*. Over time, the library became broader: a lightweight orchestration model that can cover many scenarios often handled by Unity coroutines, job-like systems, or `.NET Task` pipelines.

Svelto.Tasks is especially game-friendly:

- low overhead,
- patterns that can be allocation-conscious,
- explicit scheduling through runners,
- easy fit with Svelto.ECS style architecture.

And because it is not intrinsically Unity-bound, it can run outside Unity too (Unity-specific features are naturally disabled when unavailable).

---

## Why Svelto.Tasks 2.0?

Svelto.Tasks 1.x was already mature and useful. 2.0 is mainly a **redesign for performance and clearer rules**, not a “new toy API for novelty”.

So the practical tradeoff is:

- **Pros**: improved architecture/perf potential, clearer modernized flow.
- **Cost**: existing 1.0/1.5 code usually needs a meaningful rewrite.

If you are migrating, treat it as an intentional upgrade pass, not a drop-in rename.

---

## The mental model in one minute

Svelto.Tasks revolves around three concepts:

1. **Tasks**: `IEnumerator` routines that yield control.
2. **Runners**: schedulers that decide where/when/how tasks move forward.
3. **Continuation**: pause one task until another task (possibly on another runner/thread) completes.

If you internalize these three, most of Svelto.Tasks becomes intuitive.

---

## Part 1 — Run your first task

Let’s start with the smallest useful example.

```csharp
using System.Collections;
using Svelto.Tasks;
using Svelto.Tasks.ExtraLean;

IEnumerator UpdateIt()
{
    while (true)
    {
        // work

        // IMPORTANT: always yield at least once in infinite loops
        yield return Yield.It;
    }
}

// Queue the task on a runner
UpdateIt().RunOn(StandardSchedulers.updateScheduler);
```

### Why this matters

- You can queue tasks from anywhere in code.
- You are not required to be inside a `MonoBehaviour`.
- Execution policy is explicit via the selected runner.

### Critical beginner rule

If a task has `while (true)`, it **must yield**; otherwise it becomes a hard infinite loop.

---

## ExtraLean vs Lean (early, simple explanation)

In 2.0 there are two common task “modes” you’ll see in practice:

### ExtraLean

- Minimal overhead path.
- Uses `IEnumerator` and limited yield semantics (`null` / `Yield.It` patterns).
- Great for hot/simple loops where you want the leanest possible behavior.

### Lean

- Uses `IEnumerator<TaskContract>`.
- Supports richer yielded values and continuation semantics naturally.
- Better when orchestration complexity grows.

Rule of thumb: start with **Lean** for clarity; move hotspots to **ExtraLean** when profiling justifies it.

---

## Part 2 — Running on another thread

Now move beyond “main-thread-only coroutine replacement”.

```csharp
using System.Collections.Generic;
using Svelto.Tasks.Lean;

IEnumerator<TaskContract> CalculateAndShowNumber(Renderer renderer)
{
    while (true)
    {
        int colorSeed = FindPrimeNumber(); // expensive CPU work

        // Switch to main-thread runner for Unity API usage
        yield return SetColor(renderer, colorSeed).RunOn(StandardSchedulers.updateScheduler);

        yield return TaskContract.Yield.It;
    }
}

IEnumerator<TaskContract> SetColor(Renderer renderer, int seed)
{
    renderer.material.color = new Color(seed % 255 / 255f, seed * seed % 255 / 255f, seed / 44 % 255 / 255f);
    yield break;
}

// Run heavy loop on multi-thread runner
CalculateAndShowNumber(renderer).RunOn(StandardSchedulers.multiThreadScheduler);
```

### What is happening?

- `CalculateAndShowNumber` runs on a worker-thread runner.
- Unity API access is marshaled back to main-thread runner.
- The worker task **continues only after** the main-thread sub-task finishes.
- This is continuation-based synchronization, not callback nesting.

That pattern is one of the biggest practical wins in Svelto.Tasks.

---

## Continuation, clearly

When you yield another runnable task/continuation, the current task suspends until that child completes.

Think of it as:

```csharp
// conceptual model
var continuation = ChildTask().RunOn(otherRunner);
while (continuation.MoveNext())
    yield return TaskContract.Yield.It;
```

But you write the concise form:

```csharp
yield return ChildTask().RunOn(otherRunner);
```

No callback pyramids, no manual state machine boilerplate.

---

## Part 3 — `Continue()` vs `Forget()`

This distinction is easy to misuse, so make it explicit.

### Wait for child completion

```csharp
IEnumerator<TaskContract> Parent()
{
    yield return Child().Continue();
    // executes after child ended
}
```

### Fire-and-forget child

```csharp
IEnumerator<TaskContract> Parent()
{
    yield return Child().Forget();
    // parent does not wait for child completion
}
```

Use `Continue()` when correctness depends on order; use `Forget()` only when decoupling is intentional.

---

## Part 4 — Runners: where and how tasks run

Runners are first-class in Svelto.Tasks.

### Common runner families

- **Main-thread / loop runners** (platform-specific variants, including Unity-oriented schedulers).
- **`MultiThreadRunner`** (one dedicated thread per runner instance).
- **Manual runners** like `SteppableRunner` for deterministic stepping/testing.

You can create multiple runners and assign domains deliberately (simulation, I/O, preprocessing, etc.).

### Practical note on multithread runners

A `MultiThreadRunner` behaves like a fiber-like queue on its own thread:

- tasks in that runner can share thread-local assumptions,
- cross-runner interaction needs explicit synchronization (usually via continuation),
- this makes synchronization points explicit and readable.

---

## Part 5 — Flow modifiers: control *how much* runs per iteration

By default, runners try to process queued work in a standard way. Flow modifiers let you enforce policies.

### Serial flow

Run task queues in strict order (task B won’t advance until task A is done).

### Staggered flow

Limit how many tasks can be processed per iteration.

### TimeBound / TimeSliced flow

Constrain work by time budget per iteration/frame.

These are essential in real-time apps where responsiveness matters as much as throughput.

---

## TimeSlice vs TimeBound (practical guidance)

Both are about frame budget, but they differ in behavior assumptions.

- Use **time slicing** when you want broad fairness and frame-budget enforcement across many tasks.
- Use **time bound** when you need stronger total-budget constraints for the runner iteration.

As always: verify with profiler in your target workload, not synthetic assumptions.

---

## Part 6 — Allocation-aware patterns

Iterator blocks are convenient but typically allocate when instantiated by C# compiler-generated classes.

Practical options:

- Reuse iterators where safe.
- Use local-function enumerator patterns where appropriate.
- For specialized cases, use struct-based enumerator implementations and compatible runners.

The goal is not dogmatic “zero alloc everywhere”; it is *predictable allocation where it matters*.

---

## Part 7 — Svelto.Tasks vs async/await vs Jobs

This is not a religious replacement discussion; each tool has strengths.

- Use **Svelto.Tasks** when you want explicit runner orchestration, continuation-based synchronization, and coroutine-like readability with low overhead.
- Use **`async/await`** for broader ecosystem integration and Task-based APIs.
- Use **job systems** for high-scale data-oriented parallel workloads where that model shines.

In many projects, Svelto.Tasks complements the others rather than banning them.

---

## A migration checklist (1.x → 2.0 preview)

1. Define runner boundaries first (what runs where).
2. Make synchronization points explicit with continuation.
3. Decide per domain whether Lean or ExtraLean is appropriate.
4. Apply flow modifiers from frame budget requirements, not guesswork.
5. Add deterministic runner-step tests for orchestration edge cases.
6. Profile in representative scenes before and after migration.

---

## Common mistakes to avoid

- Infinite task loops without yields.
- Calling thread-affine APIs (for example Unity APIs) from worker-thread runners.
- Using `Forget()` where ordering guarantees are required.
- Treating flow modifiers as “set once and forget” without profiling.
- Over-optimizing for zero allocations in cold code paths.

---

## Closing

Svelto.Tasks 2.0 preview is best understood as an explicit **asynchronous orchestration framework**:

- simple coroutine ergonomics,
- controlled scheduling via runners,
- powerful continuation across runners/threads,
- scalable policies through flow modifiers.

If you are new to Svelto, start from a small vertical slice: one runner on main thread, one worker runner, one explicit continuation point. Once this feels natural, the rest of the model scales elegantly.

---

## Suggested follow-up articles

1. Unity PlayerLoop integration patterns in depth.
2. Massive parallelism with task collections and synchronization points.
3. Profiling/debugging Svelto.Tasks in production.
4. Services, promises, and task routines in practical game architecture.
