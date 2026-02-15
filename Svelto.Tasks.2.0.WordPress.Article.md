# Svelto.Tasks 2.0 (Preview): practical guide for newcomers

> **Draft for sebaslab.com (WordPress-ready)**<br>
> Suggested slug: `svelto-tasks-2-0-practical-guide`<br>
> Suggested excerpt: *What Svelto.Tasks 2.0 is, why it differs from .NET Tasks, and how to use Lean/ExtraLean, runners, continuation, cross-thread execution, and allocation-friendly patterns with real examples.*

> **Preview disclaimer**<br>
> At the time of writing, **Svelto.Tasks 2.0 is in preview state**, so APIs and examples may evolve.

---

## What is Svelto.Tasks?

Svelto.Tasks is a platform-agnostic asynchronous library based on `IEnumerator` execution.

Originally it was created to run coroutine-like logic from anywhere in code, without MonoBehaviour coupling. Over time it evolved into a more general orchestration model that can cover many cases usually solved with Unity coroutines, `.NET Task`, or job-like pipelines.

In game code, the practical advantages are:

- explicit scheduling through runners,
- low overhead and allocation-aware patterns,
- easy synchronization between main-thread and worker-thread work,
- good architectural fit with Svelto.ECS-style loops.

---

## Why Svelto.Tasks 2.0?

Svelto.Tasks 1.x was already mature. 2.0 is mostly a redesign for cleaner rules and better performance characteristics.

The tradeoff is clear:

- **benefit**: stronger architecture and runtime behavior,
- **cost**: 1.x code usually needs a non-trivial migration.

---

## Lean vs ExtraLean (important first decision)

This is the most useful distinction to understand early.

### Lean tasks

Lean tasks (`IEnumerator<TaskContract>`) are closer to **`.NET Task`-style orchestration**.

- They support richer yield contracts.
- They are great when tasks need to wait/continue other tasks.
- They are ideal when you want “await-like” logic in iterator form.

### ExtraLean tasks

ExtraLean tasks (`IEnumerator`) are more **job-like**.

- Minimal overhead, minimal semantics.
- Best when you need pure ticking work and not complex task orchestration.
- In gameplay code, this is often enough for many hot loops.

A practical rule: use **Lean** when orchestration complexity matters; use **ExtraLean** when you just need fast ticked jobs.

---

## The key difference from .NET Tasks

Beyond syntax, the deepest difference is execution direction.

### .NET Task mental model

`.NET Task` is designed to run continuations on threadpool/contexts naturally. If you force single-thread execution, you typically introduce a custom ticking/pump mechanism.

### Svelto.Tasks mental model

Svelto.Tasks starts from the opposite side: tasks are naturally **ticked by runners**.

- Runners decide when tasks move (`Step`, update loop, dedicated thread loop).
- Multithreading is added by choosing thread-based runners.
- Single-thread orchestration is first-class and explicit.

### Why explicit runners are powerful

A runner is a controllable execution domain. You can `Stop()` a runner and stop all queued/running tasks in that domain together.

That “stop this whole task domain now” behavior is straightforward in Svelto.Tasks and usually less direct in pure `.NET Task` pipelines (where cancellation is often distributed/token-driven across many independent tasks).

---

## First task: simple ExtraLean ticking

```csharp
using System.Collections;
using Svelto.Tasks;
using Svelto.Tasks.ExtraLean;

IEnumerator MoveForever()
{
    while (true)
    {
        // do incremental work
        yield return Yield.It; // never forget to yield in infinite loops
    }
}

MoveForever().RunOn(StandardSchedulers.updateScheduler);
```

This already gives you coroutine-like behavior without MonoBehaviour dependency.

---

## Yielding a task on the same runner vs a different runner

This is a core pattern and should be explicit.

### 1) Same-runner continuation

```csharp
using System.Collections.Generic;
using Svelto.Tasks.Lean;

IEnumerator<TaskContract> Child()
{
    // step A
    yield return TaskContract.Yield.It;
    // step B
}

IEnumerator<TaskContract> ParentSameRunner()
{
    // parent waits child on the SAME runner
    yield return Child().Continue();

    // executes after Child completed
}
```

### 2) Different-runner continuation (single-thread + multi-thread mix)

```csharp
using System.Collections.Generic;
using Svelto.Tasks.Lean;

IEnumerator<TaskContract> HeavyCpu()
{
    DoCpuWork();
    yield break;
}

IEnumerator<TaskContract> ApplyOnMainThread()
{
    UseUnityApi();
    yield break;
}

IEnumerator<TaskContract> ParentCrossRunner(
    IRunner<LeanSveltoTask<IEnumerator<TaskContract>>> mainThreadRunner,
    IRunner<LeanSveltoTask<IEnumerator<TaskContract>>> workerRunner)
{
    // run heavy step on worker thread and wait
    yield return HeavyCpu().RunOn(workerRunner);

    // then run Unity-safe step on main thread and wait
    yield return ApplyOnMainThread().RunOn(mainThreadRunner);
}
```

This pattern gives very readable synchronization points without callback pyramids.

---

## Awaiting an actual .NET Task from Svelto context

Svelto.Tasks includes awaiter integration so a `.NET Task` continuation can resume on a chosen runner.

```csharp
using System.Threading.Tasks;
using Svelto.Tasks.Lean;

async Task LoadDataAndContinueOnRunner(SteppableRunner runner)
{
    // Real .NET Task API call
    int value = await Task.Run(() => 42).RunOn(runner);

    // continuation is runner-driven
    Consume(value);
}
```

This is useful when integrating existing async APIs into runner-controlled execution.

---

## Fire-and-forget vs strict ordering

```csharp
IEnumerator<TaskContract> ParentOrdered()
{
    yield return Child().Continue(); // wait child
}

IEnumerator<TaskContract> ParentDecoupled()
{
    yield return Child().Forget(); // don't wait child
}
```

Use `Forget()` only when decoupling is intentional and safe.

---

## Runners can process iterator blocks *and* struct enumerators

Runners are flexible in what they execute:

- regular iterator blocks (`yield`-generated classes),
- custom class-based `IEnumerator`,
- struct-based enumerators in compatible paths.

That flexibility is important because you can keep convenient iterator blocks for readability, then optimize hot paths with struct enumerators where needed.

---

## Why `while (true)` + iterator block pool is smart

At first glance, `while (true)` tasks seem scary. In Svelto.Tasks they are often intentional and efficient when used correctly.

### The idea

- Long-lived systems (game loops, service loops) naturally fit `while (true)` tasks.
- Instead of continuously recreating iterator instances, Svelto.Tasks can reuse iterator blocks through iterator block pools in suitable patterns.
- Reusing long-lived routines reduces allocation churn and helps keep frame behavior stable.

### Practical takeaway

- Yield at least once per loop iteration.
- Prefer long-lived reusable routines for persistent systems.
- Combine runner lifetime control (`Stop`/`Dispose`) with pooled/reused iterator patterns to avoid pointless task recreation.

This is one reason Svelto.Tasks is attractive for real-time code that must stay allocation-conscious.

---

## Flow modifiers (how tasks are processed per iteration)

Runners control not only *where* tasks run, but also *how much* they run per iteration.

- **Serial flow**: strict sequencing.
- **Staggered flow**: limit tasks advanced per iteration.
- **TimeBound / TimeSliced flow**: enforce time budgets.

These are practical tools to trade throughput for responsiveness in frame-based applications.

---

## Why stopping runners explicitly is a big win

A runner groups tasks by execution domain (for example: AI update, background loading, networking).

When that domain must shut down, pausing/stopping/disposal is centralized:

- stop one runner,
- all tasks in that runner stop together,
- no need to discover/cancel each task independently.

This encourages clean lifecycle boundaries and avoids “zombie task” behavior.

---

## Migration checklist (1.x to 2.0 preview)

1. Decide runner boundaries first.
2. Pick Lean or ExtraLean per subsystem intent.
3. Make continuation points explicit (same-runner and cross-runner).
4. Add flow modifiers based on measured frame budgets.
5. Refactor hot loops toward reuse/pooling patterns.
6. Use explicit runner stop/dispose semantics in teardown.

---

## Closing

Svelto.Tasks 2.0 is best seen as explicit async orchestration:

- Lean for task-like orchestration,
- ExtraLean for job-like ticking,
- runners as first-class execution domains,
- continuation as readable synchronization,
- lifecycle control through runner stop/dispose.

If you start with one main-thread runner, one worker runner, and one clear continuation point, the rest of the model becomes natural very quickly.
