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
            //if the tasks returned an extraLeanEnumerator, the parent task takes responsibility to run it
            if (_current.isExtraLeanEnumerator(out var extraLeanEnumerator) == true)
            {
                //TODO unit test Lean that yield ExtraLean
                //if the returned enumerator is NOT a taskcontract one, the continuing task cannot spawn new tasks,
                //so we can simply iterate it here until is done. This MUST run instead of the normal task.MoveNext()
                try
                {
                    if (extraLeanEnumerator.MoveNext() == false)
                        return StepState.Completed;
                    
                    var current = extraLeanEnumerator.Current;
#if DEBUG && !PROFILE_SVELTO //todo write unit test for this
            DBC.Tasks.Check.Ensure(_current.continuation?._runner != _runner,
                $"Cannot yield a new task running on the same runner of the spawning task, use Continue() instead {_current}");
#endif
                    if (current == null)
                        return StepState.Running;

                    if (current == TaskContract.Break.It || current == TaskContract.Break.AndStop)
                        return StepState.Completed;
                    
                    throw new SveltoTaskException("ExtraLean enumerator can return only null, Yield.It, Break.It, Break.AndStop and yield break");
                }
                catch (Exception e)
                {
                    Svelto.Console.LogException(e);

                    throw;
                }

                DBC.Tasks.Check.Assert(_current.continuation.Equals(default));
            }
            else
            //This task cannot continue until the spawned task is not finished.
            //"continuation" different from null signals that a spawned task is still running so this task cannot continue
            
            //continuation is != null both in the RunOn case (continuation returned directly) and in the Continue case (continuation generated
            //by this wrapper)
            if (_current.continuation != null)
            {
                //a task is waiting to be completed, spin this one
                if (_current.continuation.Value.isRunning)
                    return StepState.Running; //todo: if I implement the idea of waiting tasks, this should never get here and so can throw an exception

                //if _continuingTask != null Continue() has been yielded
                //if _continuingTask == null RunOn() has been yielded
                //Break And Stop works only with .Continue() and not with .RunOn()
                //child task is completed, what to do next?
                if (_current.isContinued)
                {
                    //OK THIS IS COMPLICATED AND TODO MUST BE UNIT TESTED:
                    //Case Task spawn another task that return an object, like loading a gameobject
                    //the child task is returning the object, the parent task must continue with the object returned by the child
                    //current must be set to the child task result.
                    //now the tricky part: this can become a hierarchy, in fact I need to unit test:
                    //parent -> middle -> child return object
                    _current = _continuingTask.Current; //the parent task must return the child task result 
                    _continuingTask = default; //finish to wait for the continuator, reset it

                    //the child task is telling to interrupt everything! No need to do the next move next
                    if (_current.breakMode == TaskContract.Break.AndStop)
                        return StepState.Completed;
                }
            }
            
            //child task is completed, continue the normal execution of this task
            //continue the normal execution of this task
            try
            {
                if (task.MoveNext() == false)
                    return StepState.Completed;
            }
            catch (Exception e)
            {
                Svelto.Console.LogException(e);

                throw;
            }

            _current = task.Current;
#if DEBUG && !PROFILE_SVELTO //todo write unit test for this
            DBC.Tasks.Check.Ensure(_current.continuation?._runner != _runner,
                $"Cannot yield a new task running on the same runner of the spawning task, use Continue() instead {_current}");
#endif
            if (_current.yieldIt)
                return StepState.Running;

            //hasValue stops the execution early, to Unit Test. It seems to b e necessary too!
            if (_current.breakMode == TaskContract.Break.It || _current.breakMode == TaskContract.Break.AndStop || _current.hasValue)
                return StepState.Completed;

            //this means that the previous MoveNext returned an enumerator, it may be a continuation case
            //.Continue() also generate this case.
            
            //.RunOn() instead generates directly a continuation, doesn't pass through the if 
            //as _current.continuation is set instead
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
                    _current = new TaskContract(continuation, true);
                    _continuingTask = (TTask)tuple.enumerator; //remember the child task
                    
                    //TODO:
                    //I would like to remove the parent task from the coroutine list, before to spawn a continuation
                    //in order to do this the task must be put back in the coroutine list once the continuation is completed
                    //also why did I introduced the spawnedCoroutines list? How would flow modifiers work with this?
                    new LeanSveltoTask<TTask>().SpawnContinuingTask(_runner, (TTask)tuple.enumerator, continuation);

                    //return StepState.Waiting; todo: if I want to proceed with this idea I have to return this 
                    //also in the case of _current.isContinued
                }
            }

            return StepState.Running;
        }

        internal TTask task { get; } //current task to wrap
        
        //Todo would be much better to hold an index to the task in the runner to save memory 
        TTask _continuingTask; //if the task is waiting for a Continue() case, this will hold the task continued
        
        TaskContract _current; //if the task is waiting for a continuation (Continue or RunOn), this will hold the continuation
        readonly TRunner _runner; //runner that is running this task
    }
}