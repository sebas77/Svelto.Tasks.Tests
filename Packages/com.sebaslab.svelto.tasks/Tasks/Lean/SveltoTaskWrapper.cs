using System;
using System.Collections.Generic;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.Lean
{
    struct SveltoTaskWrapper<TTask, TRunner>
        where TTask : IEnumerator<TaskContract> where TRunner : class, IRunner<LeanSveltoTask<TTask>>
    {
        public SveltoTaskWrapper(in TTask task, TRunner runner) : this()
        {
            _runner   = runner;
            this.task = task;
        }

        public StepState Step()
        {
            //this means that the previous MoveNext returned an enumerator, it may be a continuation case
            if (_current.isExtraLeanEnumerator(out var extraLeanEnumerator) == true)
            {
                //if the returned enumerator is NOT a taskcontract one, the continuing task cannot spawn new tasks,
                //so we can simply iterate it here until is done. This MUST run instead of the normal task.MoveNext()
                if (extraLeanEnumerator.MoveNext())
                    return StepState.Running;

                DBC.Tasks.Check.Assert(_current.continuation.Equals(default));
            }
            
            //This task cannot continue until the spawned task is not finished.
            //"continuation" signals that a spawned task is still running so this task cannot continue
            if (_current.continuation != null)
            {
                //a task is waiting to be completed, spin this one
                if (_current.continuation.Value.isRunning)
                    return StepState.Running;

                //if _continuingTask != null Continue() has been yielded
                //if _continuingTask == null RunOn() has been yielded
                if (_current.continuation != null)
                {
                    //the child task is telling to interrupt everything!
                    _current = default; //finish to wait for the continuator, reset it
                    _continuingTask = default;

                    if (_continuingTask.Current.breakMode == TaskContract.Break.AndStop) //what did the child task return?
                        return StepState.Completed;
                }
            }
            
            //continue the normal execution of this task
            try
            {
                if (task.MoveNext() == false)
                    return StepState.Completed;
            }
            catch (Exception e)
            {
                _current = new TaskContract(e);
                
                return StepState.Completed;
            }

            _current = task.Current;
#if DEBUG && !PROFILE_SVELTO //todo write unit test for this
            DBC.Tasks.Check.Ensure(_current.continuation?._runner != _runner,
                $"Cannot yield a new task running on the same runner of the spawning task, use Continue() instead {_current}");
#endif
            if (_current.yieldIt)
                return StepState.Running;

            if (_current.breakMode == TaskContract.Break.It || _current.breakMode == TaskContract.Break.AndStop || _current.hasValue)
                return StepState.Completed;

            //this means that the previous MoveNext returned an enumerator, it may be a continuation case
            if (_current.isTaskEnumerator(out (IEnumerator<TaskContract> enumerator, bool isFireAndForget) tuple) == true)
            {
                //Handle the Continue() case, the new task must "continue" using the current runner
                //the current task will continue waiting for the new spawned task through the continuation

                //a new TaskContract is created, holding the continuationEnumerator of the new task
                //it must be added in the runner as "spawned" task and must run separately from this task
                DBC.Tasks.Check.Require(tuple.enumerator != null);

                if (tuple.isFireAndForget == true)
                {
                    TTask tupleEnumerator = (TTask)tuple.enumerator;
                    new LeanSveltoTask<TTask>().Run(_runner, ref tupleEnumerator);
                }
                else
                {
#if DEBUG && !PROFILE_SVELTO
                    var continuation = new Continuation(ContinuationPool.RetrieveFromPool(), _runner);
#else
                    var continuation = new Continuation(ContinuationPool.RetrieveFromPool());
#endif
                    //note: this is a struct and this must be completely set before calling SpawnContinuingTask
                    //as it can trigger a resize of the datastructure that contains this, invalidating this
                    //TestThatLeanTasksWaitForContinuesWhenRunnerListsResize unit test covers this case
                    _current = new TaskContract(continuation);
                    _continuingTask = (TTask)tuple.enumerator; //remember the child task

                    //TODO:
                    //I would like to remove the parent task from the coroutine list, before to spawn a continuation
                    //in order to do this the task must be put back in the coroutine list once the continuation is completed
                    //also why did I introduced the spawnedCoroutines list? How would flow modifiers work with this?
                    new LeanSveltoTask<TTask>().SpawnContinuingTask(_runner, (TTask)tuple.enumerator, continuation);
                }
            }

            return StepState.Running;
        }

        internal TTask task { get; } //current task to wrap
        TTask _continuingTask; //if the task is waiting for a continuation, this will hold the child task
        TaskContract _current; //if the task is waiting for a continuation, this will hold the continuation
        readonly TRunner _runner; //runner that is running this task
    }
}