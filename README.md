# Svelto.Tasks 2.0 (Preview)

> At the time of writing, Svelto.Tasks 2.0 is in preview state, so APIs and examples may evolve.

Svelto.Tasks is a platform-agnostic asynchronous library based on `IEnumerator` execution.
It was designed for standard C#, has been used in Unity for years, and includes Unity-specific extensions/integration where relevant.

## Core idea

Svelto.Tasks is runner-driven:

- tasks are `IEnumerator` routines,
- runners tick tasks (`Step`, update loops, dedicated threads),
- continuation is explicit (`Continue`, `RunOn(otherRunner)`),
- runner lifecycle is explicit (`Pause`, `Resume`, `Stop`, `Dispose`).

This is different from `.NET Task` pipelines, which are continuation/thread-context oriented by default. In Svelto.Tasks, ticking is the default model, and threading is opted in by selecting thread-based runners.

## Lean vs ExtraLean

### Lean (`IEnumerator<TaskContract>`)
Use Lean when you need richer task-like orchestration (continuations, task contracts, explicit flow control).

### ExtraLean (`IEnumerator`)
Use ExtraLean for more job-like ticking where orchestration complexity is unnecessary and minimal overhead is preferred.

In game logic, ExtraLean is often enough for many hot loops; Lean is ideal when orchestration logic becomes more complex.

## First example: explicit runner usage (2.0 style)

```csharp
using System.Collections;
using Svelto.Tasks.ExtraLean;

IEnumerator MoveForever()
{
    while (true)
    {
        // incremental work
        yield return Yield.It; // always yield in infinite loops
    }
}

using (var runner = new SteppableRunner("Update"))
{
    MoveForever().RunOn(runner);

    // called by your main loop/frame loop
    runner.Step();
}
```

In Svelto.Tasks 2.0 you typically create/pass runner instances directly (`SteppableRunner`, `SyncRunner`, `MultiThreadRunner`) rather than relying on old static scheduler APIs.

## Same-runner vs cross-runner continuation

```csharp
using System.Collections.Generic;
using Svelto.Tasks.Lean;

IEnumerator<TaskContract> Child()
{
    yield return TaskContract.Yield.It;
}

IEnumerator<TaskContract> ParentSameRunner()
{
    yield return Child().Continue(); // same-runner continuation
}
```

```csharp
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
    yield return HeavyCpu().RunOn(workerRunner);
    yield return ApplyOnMainThread().RunOn(mainThreadRunner);
}
```

## Awaiting an actual .NET task with a runner

```csharp
using System.Threading.Tasks;
using Svelto.Tasks.Lean;

async Task SomeAsyncOperation(SteppableRunner runner)
{
    await Task.Delay(10).RunOn(runner);

    // continuation runs through runner ticks
    OnFirstContinuation();

    runner.Stop();

    // won't continue while runner is stopped
    await Task.Delay(10).RunOn(runner);
}
```

## `while (true)` + IteratorBlockPool pattern

Long-lived `while (true)` loops are useful when paired with yielding and iterator reuse.

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
        yield return TaskContract.Break.It;
    }
}

var pool = new IteratorBlockPool<PoolData>(MyIterator, "GameplayPool");

var (data1, block1) = pool.Get();
data1.value = 0;
block1.MoveNext();
block1.MoveNext(); // released back to pool

var (data2, block2) = pool.Get();
// reused instances are possible
```

This helps avoid unnecessary allocations for persistent systems.

## Flow modifiers with runners (example)

```csharp
using System.Collections.Generic;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Lean;

using (var runner = new SteppableRunner("SerialFlow"))
{
    runner.UseFlowModifier(new SerialFlow());

    IEnumerator<TaskContract> Task1()
    {
        StepA();
        yield return TaskContract.Yield.It;
        StepB();
    }

    IEnumerator<TaskContract> Task2()
    {
        StepC();
        yield return TaskContract.Yield.It;
        StepD();
    }

    Task1().RunOn(runner);
    Task2().RunOn(runner);

    runner.Step(); // Task1 starts
    runner.Step(); // Task1 completes, then Task2 starts
}
```

## MultiThreadRunner vs MultiThreadedParallelTaskCollection

`MultiThreadRunner` and `MultiThreadedParallelTaskCollection` both use multithreading, but they solve different problems.

### `MultiThreadRunner`
- General-purpose runner that executes scheduled Svelto tasks on its own thread.
- Best when you want runner semantics (continuation, pausing/resuming/stopping a task domain, orchestration).

### `MultiThreadedParallelTaskCollection`
- A parallel batch executor designed to run multiple parallel tasks at once, typically with a configured worker count.
- Best for “run this set in parallel, wait for completion” workloads.
- Supports collection-style lifecycle (`Add`, `Complete`, `Reset`, `Stop`, `Dispose`) and reuse.

Use `MultiThreadRunner` for ongoing orchestrated asynchronous flows; use `MultiThreadedParallelTaskCollection` for explicit parallel batches.
