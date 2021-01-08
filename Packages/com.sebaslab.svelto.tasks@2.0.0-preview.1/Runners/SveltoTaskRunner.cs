using System.Threading;
using Svelto.Common;
using Svelto.DataStructures;
using Svelto.Tasks.DataStructures;

namespace Svelto.Tasks.Internal
{
    public static class SveltoTaskRunner<T>  where T : ISveltoTask
    {
        public static void StopRoutines(FlushingOperation flushingOperation)
        {
            //note: _coroutines will be cleaned by the single tasks stopping silently. in this way they will be put
            //back to the pool. Let's be sure that the runner had the time to stop and recycle the previous tasks
            flushingOperation.Stop();
        }
        
         public static void KillProcess(FlushingOperation flushingOperation)
         {
             flushingOperation.Kill();
         }

        internal class Process<TFlowModifier> : IProcessSveltoTasks where TFlowModifier: IFlowModifier
        {
            public override string ToString()
            {
                return _info.runnerName;
            }

            public Process
            (ThreadSafeQueue<T> newTaskRoutines, FasterList<T> coroutines, FlushingOperation flushingOperation
           , TFlowModifier info)
            {
                _newTaskRoutines   = newTaskRoutines;
                _coroutines        = coroutines;
                _flushingOperation = flushingOperation;
                _info              = info;
            }    

            public bool MoveNext<PlatformProfiler>(in PlatformProfiler platformProfiler) 
                where PlatformProfiler : IPlatformProfiler
            {
                 DBC.Tasks.Check.Require(_flushingOperation.paused == false
                   || _flushingOperation.kill == false
                    , $"cannot be found in pause state if killing has been initiated {_info.runnerName}");
                 DBC.Tasks.Check.Require(_flushingOperation.kill == false 
                                      || _flushingOperation.stopping == true
                                       , $"if a runner is killed, must be stopped {_info.runnerName}");
                 
                //the difference between stop and pause is that pause freeze the tasks states, while stop flush
                //them until there is nothing to run. Ever looping tasks are forced to be stopped and therefore
                //can terminate naturally
                if (_flushingOperation.stopping == true)
                {
                    if (_coroutines.count == 0)
                    {
                        if (_flushingOperation.kill == true)
                        {
                            _newTaskRoutines.Clear();
                            return false;
                        }

                        //once all the coroutines are flushed the loop can return accepting new tasks
                        _flushingOperation.Unstop();
                    }
                }

                //a stopped runner can restart and the design allows to queue new tasks in the stopped state
                //although they won't be processed. In this sense it's similar to paused. For this reason
                //_newTaskRoutines cannot be cleared in paused and stopped state.
                if (_newTaskRoutines.count > 0 && _flushingOperation.acceptsNewTasks == true)
                {
                    _coroutines.EnsureCapacity((uint) (_coroutines.count + _newTaskRoutines.count));
                    _newTaskRoutines.DequeueAllInto(_coroutines);
                }

                var coroutinesCount = _coroutines.count;
                
                if (coroutinesCount == 0 || (_flushingOperation.paused == true && _flushingOperation.stopping == false))
                {
                    return true;
                }
                
#if TASKS_PROFILER_ENABLED
                Profiler.TaskProfiler.ResetDurations(_info.runnerName);
#endif
                _info.Reset();

                //Note: old comment, left as memo, when I used to allow to run tasks immediately
                //I decided to adopt this strategy instead to call MoveNext() directly when a task
                //must be executed immediately. However this works only if I do not update the coroutines count
                //after the MoveNext which on its turn could run immediately another task.
                //this can cause a stack of MoveNext, which works only because I add the task to run immediately
                //at the end of the list and the child MoveNext executes only the new one. When the stack
                //goes back to the previous MoveNext, I don't want to execute the new just added task again,
                //so I can't update the coroutines count, it must stay the previous one/
                int index = 0;
                bool mustExit;

                var coroutines = _coroutines.ToArrayFast(out _);

                do
                {
                    if (_info.CanProcessThis(ref index) == false) break;

                    bool result;

                    if (_flushingOperation.stopping) coroutines[index].Stop();

#if ENABLE_PLATFORM_PROFILER
                    using (platformProfiler.Sample(coroutines[index].name))
#endif
#if TASKS_PROFILER_ENABLED
                        result =
                            Profiler.TaskProfiler.MonitorUpdateDuration(ref coroutines[index], _info.runnerName);
#else
                        result = coroutines[index].MoveNext();
#endif
                    //MoveNext may now cause tasks to run immediately and therefore increase the array size
                    //this side effect is due to the fact that I don't have a stack for each task anymore
                    //like I used to do in Svelto tasks 1.5 and therefore running new enumerators would
                    //mean to add new coroutines. However I do not want to iterate over the new coroutines
                    //during this iteration, so I won't modify coroutinesCount avoid this complexity disabling run
                    //immediate
                    //coroutines = _coroutines.ToArrayFast(out _);

                    int previousIndex = index;

                    if (result == false)
                    {
                        _coroutines.UnorderedRemoveAt(index);

                        coroutinesCount--;
                    }
                    else
                        index++;

                    mustExit = (coroutinesCount == 0 || 
                                _info.CanMoveNext(ref index, ref coroutines[previousIndex], coroutinesCount) ==
                                false ||
                                index >= coroutinesCount);
                } while (!mustExit);

                return true;
            }
            
            readonly ThreadSafeQueue<T> _newTaskRoutines;
            readonly FasterList<T>      _coroutines;
            readonly FlushingOperation  _flushingOperation;
            
            TFlowModifier _info;
        }
        
        public class FlushingOperation
        {
            public bool paused         => Volatile.Read(ref _paused);
            public bool stopping       => Volatile.Read(ref _stopped);
            public bool kill           => Volatile.Read(ref _killed);
            public bool acceptsNewTasks => paused == false && stopping == false && kill == false;  
            
            public void Stop()
            {
                Volatile.Write(ref _stopped, true);
                Volatile.Write(ref _paused, false);
            }

            public void Kill()
            {
                Volatile.Write(ref _stopped, true);
                Volatile.Write(ref _killed, true);
                Volatile.Write(ref _paused, false);
            }

            public void Pause()
            {
                DBC.Tasks.Check.Require(kill == false, "cannot pause a runner that is killed");

                Volatile.Write(ref _paused, true);
            }

            public void Resume()
            {
                DBC.Tasks.Check.Require(stopping == false, "cannot resume a runner that is stopping");
                DBC.Tasks.Check.Require(kill == false, "cannot resume a runner that is killed");
                
                Volatile.Write(ref _paused, false);
            }

            internal void Unstop()
            {
                Volatile.Write(ref _stopped, false);
            }
            
            bool        _paused;
            bool        _stopped;
            bool        _killed;
        }
    }
}