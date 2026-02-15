# Svelto.Tasks 2.0 (Preview): practical guide for newcomers

> At the time of writing, **Svelto.Tasks 2.0 is in preview state**, so APIs and examples may evolve.

## What is Svelto.Tasks?

Svelto.Tasks is a platform-agnostic asynchronous library based on `IEnumerator` execution.

It was designed for standard C#, has been used in Unity for years in production codebases, and includes Unity-specific extensions/integration where relevant. In practice, that means you can keep a clean engine-agnostic core while still plugging into Unity lifecycle points where needed.

Originally it was created to run coroutine-like logic from anywhere in code, without MonoBehaviour coupling. Over time it evolved into a broader orchestration model that can cover many cases usually solved with Unity coroutines, `.NET Task`, or job-like pipelines. The important difference is that Svelto.Tasks makes scheduling explicit and inspectable: you choose the runner, you choose the flow policy, and you control lifecycle boundaries directly.

In game code, the practical advantages are:

- explicit scheduling through runners,
- low overhead and allocation-aware patterns,
- easy synchronization between main-thread and worker-thread work,
- good architectural fit with Svelto.ECS-style loops.

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

A second practical rule is to optimize *after* design clarity: start with the model that makes intent obvious, then move specific hotspots to the leaner path once profiling confirms it matters.

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

using (var runner = new Svelto.Tasks.ExtraLean.SteppableRunner("Update"))
{
    MoveForever().RunOn(runner);

    // typically called by your loop each tick/frame
    runner.Step();
}
```

This already gives you coroutine-like behavior without MonoBehaviour dependency.

In 2.0 you usually create/pass runner instances explicitly (for example `SteppableRunner`, `SyncRunner`, `MultiThreadRunner`) rather than relying on old static standard schedulers.

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

async Task SomeAsyncOperation(SteppableRunner runner)
{
    // Real .NET Task awaited with Svelto's runner-aware awaiter
    await Task.Delay(10).RunOn(runner);

    // this continuation executes through the runner
    OnFirstContinuation();

    runner.Stop();

    // after Stop(), this continuation won't run unless the runner resumes/runs again
    await Task.Delay(10).RunOn(runner);
    OnSecondContinuation();
}
```

This is the exact pattern used in the unit tests to verify that continuations are posted to the runner and do not proceed once the runner is stopped.

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

A simplified pattern from unit tests (`IteratorBlockPoolTests`) looks like this:

```csharp
class PoolData
{
    public int value;
}

IEnumerator<TaskContract> MyIterator(PoolData data)
{
    while (true)
    {
        data.value++;
        yield return TaskContract.Break.It; // return control so the pooled block can be reused
    }
}

var pool = new IteratorBlockPool<PoolData>(MyIterator, "GameplayPool");

var (data1, block1) = pool.Get();
data1.value = 0;
block1.MoveNext(); // value = 1
block1.MoveNext(); // value = 2 and block released back to pool

var (data2, block2) = pool.Get();
// data2/block2 can be the same reused instances
```

This is the key trick: keep the iterator logic long-lived (`while (true)`), but hand control back each step (`Break`/`Yield`) so the runtime can recycle the iterator block instead of constantly allocating new ones.

Why this matters for truly zero-allocation-style runtime code:

- The expensive part is often *re-creating state machines repeatedly*, not ticking an existing one.
- A persistent `while (true)` routine amortizes setup cost and avoids per-cycle iterator instantiation churn.
- Pooling closes the loop by reusing both the iterator block and its companion data, so hot paths can run for long periods without GC pressure spikes.

In other words: if the behavior is conceptually a long-lived service (AI update loop, streaming loop, gameplay subsystem loop), model it as a long-lived task too. Start it once, tick it forever, stop it explicitly with runner lifecycle control.

### Practical takeaway

- Yield at least once per loop iteration.
- Prefer long-lived reusable routines for persistent systems.
- Pre-allocate/pool the task blocks and their data where possible.
- Combine runner lifetime control (`Stop`/`Dispose`) with pooled/reused iterator patterns to avoid pointless task recreation.

This is one of the main reasons Svelto.Tasks can be used to write allocation-conscious real-time systems that stay stable under sustained load.

---

## Flow modifiers with a runner (example: `TimeSlicedFlow`)

A very common real-world use case is protecting frame time when many tasks are queued.
Instead of letting a runner drain too much work in one iteration, you can cap execution budget per step/frame.

```csharp
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Lean;

using (var runner = new SteppableRunner("TimeSliced"))
{
    // Budget ~2 ms of task processing per Step().
    runner.UseFlowModifier(new TimeSlicedFlow(2.0f));

    int completed = 0;

    IEnumerator<TaskContract> SmallWork()
    {
        Thread.Sleep(1); // simulate tiny but non-trivial work
        completed++;
        yield return TaskContract.Yield.It;
    }

    for (int i = 0; i < 20; i++)
        SmallWork().RunOn(runner);

    runner.Step();
    // With a 2ms slice and 1ms tasks, only a subset should run this frame.

    runner.Step();
    // Remaining tasks continue in later frames, smoothing spikes.
}
```

Use `TimeSlicedFlow` when you care about responsiveness and smoothness more than single-frame throughput, which is usually the right tradeoff for gameplay/update loops.

---

## MultiThreadRunner vs MultiThreadedParallelTaskCollection

`MultiThreadRunner` and `MultiThreadedParallelTaskCollection` both use multithreading, but they solve different problems.

### `MultiThreadRunner`

- General-purpose runner that executes scheduled Svelto tasks on its own thread.
- Best when you want runner semantics (continuation, pausing/resuming/stopping a task domain, orchestration over time).

### `MultiThreadedParallelTaskCollection`

- A parallel batch executor designed to run many tasks concurrently with a configured worker count.
- Best for “run this whole set in parallel and complete it” workloads.
- Has collection-style APIs/lifecycle (`Add`, `Complete`, `Reset`, `Stop`, `Dispose`) and reuse semantics.

Use `MultiThreadRunner` for long-lived asynchronous flows; use `MultiThreadedParallelTaskCollection` for explicit parallel batches.

---

## Why stopping runners explicitly is a big win

A runner groups tasks by execution domain (for example: AI update, background loading, networking).

When that domain must shut down, pausing/stopping/disposal is centralized:

- stop one runner,
- all tasks in that runner stop together,
- no need to discover/cancel each task independently.

This encourages clean lifecycle boundaries and avoids “zombie task” behavior.

---

## Migration checklist (2.0 preview)

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
