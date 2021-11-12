using System.Collections.Generic;

namespace Svelto.Tasks.Lean
{
    struct SveltoTaskWrapper<TTask, TRunner>
        where TTask : IEnumerator<TaskContract> where TRunner : class, IRunner<LeanSveltoTask<TTask>>
    {
        public SveltoTaskWrapper(ref TTask task, TRunner runner) : this()
        {
            _taskContinuation._runner = runner;
            this.task = task;
        }

        public bool MoveNext()
        {
            #region ALL_CODE_THAT_RUNS_INSTEAD_OF_task_MoveNext 
            //This task cannot continue until the spawned task is not finished.
            //"continuation" signals that a spawned task is still running so this task cannot continue
            if (_current.continuation != null)
            {
                //a task is waiting to be completed, spin this one
                if (_current.continuation.Value.isRunning == true)
                    return true;

                //this is a continued task
                if (_taskContinuation._continuingTask != null)
                {
                    //the child task is telling to interrupt everything!
                    var currentBreakIt = _taskContinuation._continuingTask.Current.breakIt;
                    _taskContinuation._continuingTask = null;

                    if (currentBreakIt == Break.AndStop)
                        return false;
                }
            }
            
            //this means that the previous MoveNext returned an enumerator, it may be a continuation case
            if (_current.enumerator != null)
            {
                if (_current.isTaskEnumerator == false)
                {
                    //if the returned enumerator is NOT a taskcontract one, the continuing task cannot spawn new tasks,
                    //so we can simply iterate it here until is done. This MUST run instead of the normal task.MoveNext()
                    if (_current.enumerator.MoveNext() == true)
                        return true;
                    
                    _current = new TaskContract(); //end of the enumerator, reset TaskContract?
                }
            }
            #endregion
            
            //continue the normal execution of this task
            if (task.MoveNext() == false)
                return false;

            _current = task.Current;
            
            DBC.Tasks.Check.Ensure(_current.continuation?._runner != _taskContinuation._runner,
                $"Cannot yield a new task running on the same runner of the spawning task, use Continue() instead {_current}");

            if (_current.yieldIt == true)
                return true;

            if (_current.breakIt == Break.It || _current.breakIt == Break.AndStop || _current.hasValue == true)
                return false;
            
            //this means that the previous MoveNext returned an enumerator, it may be a continuation case
            if (_current.enumerator != null)
            { 
                //Handle the Continue() case, the new task must "continue" using the current runner
                //the current task will continue waiting for the new spawned task through the continuation
                if (_current.isTaskEnumerator)
                {
                    //a new TaskContract is created, holding the continuationEnumerator of the new task
                    //it must be added in the runner as "spawned" task and must run separately from this task
                    //TODO Optimize this:
                    _taskContinuation._continuingTask = (IEnumerator<TaskContract>) _current.enumerator;
                    TTask continuingTask = ((TTask) _taskContinuation._continuingTask);
                    var continuation =
                        new LeanSveltoTask<TTask>().SpawnContinuingTask(_taskContinuation._runner, ref continuingTask);

                    //to remember, why isRunnering could be false?
                    DBC.Tasks.Check.Assert(continuation.isRunning == true, "why is it false? Update comment");
                    
                    _current = continuation.isRunning == true ? 
                        new TaskContract(continuation) : 
                        new TaskContract(); //end of the enumerator, reset TaskContract?
                }
            }

            return true;
        }

        internal TTask task { get; }

        ContinueTask _taskContinuation;
        TaskContract _current;

        struct ContinueTask
        {
            internal TRunner                   _runner;
            internal IEnumerator<TaskContract> _continuingTask;
        }
    }
}