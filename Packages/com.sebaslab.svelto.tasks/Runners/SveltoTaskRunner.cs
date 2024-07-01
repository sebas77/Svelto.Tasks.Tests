using System;
using System.Collections.Concurrent;
using System.Threading;
using Svelto.Common;
using Svelto.DataStructures;


namespace Svelto.Tasks.Internal
{
    public static class SveltoTaskRunner<TTask> where TTask : ISveltoTask
    {
        internal class Process<TFlowModifier> : IProcessSveltoTasks<TTask> where TFlowModifier : IFlowModifier
        {
            public override string ToString()
            {
                return _runnerName;
            }

            public Process(FlushingOperation flushingOperation, TFlowModifier info, uint size, string runnerName)
            {
                _newTaskRoutines   = new ConcurrentQueue<TTask>();
                _runningCoroutines = new FasterList<TTask>(size);
                _spawnedCoroutines = new FasterList<TTask>(size);
                //_waitingCoroutines = new FasterList<TTask>(size);
                _flushingOperation = flushingOperation;
                _info              = info;
                _runnerName        = $"{typeof(TFlowModifier).FullName} - {runnerName} runner";
            }

            public bool MoveNext<PlatformProfiler>(in PlatformProfiler platformProfiler)
                where PlatformProfiler : IPlatformProfiler
            {
                DBC.Tasks.Check.Require(_flushingOperation.paused == false || _flushingOperation.kill == false
                  , $"cannot be found in pause state if killing has been initiated {_runnerName}");
                DBC.Tasks.Check.Require(_flushingOperation.kill == false || _flushingOperation.stopping == true
                  , $"if a runner is killed, must be stopped {_runnerName}");

                if (_flushingOperation.flush)
                {
                    _newTaskRoutines.Clear();
                }

                //a stopped runner can restart and the design allows to queue new tasks in the stopped state
                //although they won't be processed. In this sense it's similar to paused. For this reason
                //_newTaskRoutines cannot be cleared in paused and stopped state.
                //This is done before the stopping check because all the tasks queued before stop will be stopped
                if (_newTaskRoutines.Count > 0 && _flushingOperation.acceptsNewTasks == true)
                {
                    var count = _runningCoroutines.count;
                    _runningCoroutines.EnsureCountIsAtLeast((uint)(count + _newTaskRoutines.Count));
                    _newTaskRoutines.CopyTo(_runningCoroutines.ToArrayFast(out _), count);
                    _newTaskRoutines.Clear();
                }

                //the difference between stop and pause is that pause freeze the tasks states, while stop flush
                //them until there is nothing to run. Ever looping tasks are forced to be stopped and therefore
                //can terminate naturally
                if (_flushingOperation.stopping == true)
                {
                    //remember: it is not possible to clear new tasks after a runner is stopped, because a runner
                    //doesn't react immediately to a stop, so new valid tasks after the stop may be queued meanwhile.
                    //A Flush should be the safe way to be sure that only the tasks in process up to the Stop()
                    //point are stopped.
                    if (_runningCoroutines.count == 0)
                    {
                        if (_flushingOperation.kill == true)
                        {
                            //ContinuationEnumeratorInternal are intercepted by the finalizers and
                            //returned to the pool.`
                            _runningCoroutines.Clear();
                            _newTaskRoutines.Clear();
                            return false;
                        }

                        //once all the coroutines are flushed the loop can return accepting new tasks
                        _flushingOperation.Unstop();
                    }
                }

                var coroutinesCount        = _runningCoroutines.count;
                var spawnedCoroutinesCount = _spawnedCoroutines.count;
                //uint waitingRoutines = 0;

                if ((spawnedCoroutinesCount + coroutinesCount == 0)
                 || (_flushingOperation.paused == true && _flushingOperation.stopping == false))
                {
                    return true;
                }

#if TASKS_PROFILER_ENABLED
                Profiler.TaskProfiler.ResetDurations(_info.runnerName);
#endif
                _info.Reset();

                bool mustExit;

                //these are the child coroutines spawned by the main coroutines
                if (spawnedCoroutinesCount > 0)
                {
                    var spawnedCoroutines = _spawnedCoroutines.ToArrayFast(out _);
                    int index             = 0;

                    do
                    {
                        StepState result;

                        ref var spawnedCoroutine = ref spawnedCoroutines[index];
                        
                        if (_flushingOperation.stopping)
                            spawnedCoroutine.Stop();

                        try
                        {
#if ENABLE_PLATFORM_PROFILER
                            using (platformProfiler.Sample(spawnedCoroutine.name))
#endif
#if TASKS_PROFILER_ENABLED
                            result =
                                Profiler.TaskProfiler.MonitorUpdateDuration(ref spawnedCoroutines[index], _info.runnerName);
#else

                            result = spawnedCoroutine.Step();
#endif
                        }
                        catch (Exception e)
                        {
                            //note, the user code cannot catch exceptions thrown by the task
                            //we could however add some extra information in the TaskContract
                            
                            //todo unit test exceptions
                            
                            Svelto.Console.LogException(e, $"catching exception for spawned task {spawnedCoroutine.name}");
                            
                            result = StepState.Completed; //todo: in future it could be faulted if makes sense
                        }
                        
                        if (result == StepState.Completed)
                        {
                            _spawnedCoroutines.UnorderedRemoveAt((uint)index);

                            spawnedCoroutinesCount--;
                        }
                        else
                            index++;

                        mustExit = (spawnedCoroutinesCount == 0 || index >= spawnedCoroutinesCount);
                    } while (!mustExit);
                }

                //these are the main coroutines
                if (coroutinesCount > 0)
                {
                    int index = 0;

                    var coroutines = _runningCoroutines.ToArrayFast(out var count);
                    
                    DBC.Tasks.Check.Assert(count == coroutinesCount, "unexpected count");

                    do 
                    {
                        if (_info.CanProcessThis(ref index) == false)
                            break;

                        StepState result;

                        ref TTask sveltoTask = ref coroutines[index];
                        
                        if (_flushingOperation.stopping)
                            sveltoTask.Stop();

                        try
                        {
#if ENABLE_PLATFORM_PROFILER
                            using (platformProfiler.Sample(sveltoTask.name))
#endif
                    
#if TASKS_PROFILER_ENABLED
                            result =
                                Profiler.TaskProfiler.MonitorUpdateDuration(ref coroutines[index], _info.runnerName);
#else
                            result = sveltoTask.Step();
#endif
                        }
                        catch (Exception e)
                        {
                            Svelto.Console.LogException(e, $"catching exception for root task {sveltoTask.name}");
                            result = StepState.Completed; //todo: in future it could be faulted if makes sense
                        }

                        int previousIndex = index;

//                        if (result == StepState.Waiting)
//                        {
//                            _waitingCoroutines.Add(sveltoTask);
//                            waitingRoutines++;
//                            _runningCoroutines.UnorderedRemoveAt((uint)index);
//
//                            coroutinesCount--;
//                        }
//                        else
                        if (result == StepState.Completed)
                        {
                            _runningCoroutines.UnorderedRemoveAt((uint)index);

                            coroutinesCount--;
                        }
                        else
                            index++;

                        mustExit = ((coroutinesCount == 0 
                                       //&& waitingRoutines == 0
                                ) 
                         || _info.CanMoveNext(ref index, ref coroutines[previousIndex], coroutinesCount, result == StepState.Completed) == false 
                         || index >= coroutinesCount);
                    } while (mustExit == false);
                }

                return true;
            }
            
            public void StartTask(in TTask task)
            {
                _newTaskRoutines.Enqueue(task);
            }

            public void EnqueueContinuingTask(in TTask task)
            {
                _spawnedCoroutines.Add(task);
            }

            readonly ConcurrentQueue<TTask> _newTaskRoutines;
            readonly FasterList<TTask>      _runningCoroutines;
            readonly FasterList<TTask>      _spawnedCoroutines;
          //  readonly FasterList<TTask>      _waitingCoroutines;
            readonly FlushingOperation  _flushingOperation;

            TFlowModifier _info;
            string _runnerName;

            public uint numberOfRunningTasks => (uint)_runningCoroutines.count + (uint)_spawnedCoroutines.count 
                   //+ (uint)_waitingCoroutines.count
                    ;
            public uint numberOfQueuedTasks => (uint)_newTaskRoutines.Count;
            public uint numberOfTasks => numberOfRunningTasks + numberOfQueuedTasks;
        }

        //todo this must copy the SveltoTaskState pattern
        public class FlushingOperation
        {
            public bool paused          => Volatile.Read(ref _paused);
            public bool stopping        => Volatile.Read(ref _stopped);
            public bool kill            => Volatile.Read(ref _killed);
            public bool flush           => Volatile.Read(ref _flush);
            public bool acceptsNewTasks => paused == false && stopping == false && kill == false;

            public void Stop(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot stop a runner that is killed {name}");

                //maybe I want both flags to be set in a thread safe way This must be bitmask
                Volatile.Write(ref _stopped, true);
                Volatile.Write(ref _paused, false);
            }

            public void StopAndFlush()
            {
                Volatile.Write(ref _flush, true);
                Volatile.Write(ref _stopped, true);
                Volatile.Write(ref _paused, false);
            }

            public void Kill(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot kill a runner that is killed {name}");

                //maybe I want both flags to be set in a thread safe way, meaning that the
                //flags must all be set at once. This must be bitmask
                Volatile.Write(ref _stopped, true);
                Volatile.Write(ref _killed, true);
                Volatile.Write(ref _paused, false);
            }

            public void Pause(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot pause a runner that is killed {name}");

                Volatile.Write(ref _paused, true);
            }

            public void Resume(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot resume a runner that is killed {name}");

                Volatile.Write(ref _paused, false);
            }

            internal void Unstop()
            {
                Volatile.Write(ref _stopped, false);
            }

            bool _paused;
            bool _stopped;
            bool _killed;
            bool _flush;
        }
    }
}