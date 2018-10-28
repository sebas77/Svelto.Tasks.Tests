#if !NETFX_CORE

using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Experimental;
using Svelto.Tasks.Unity;
using Svelto.Tasks.Unity.Internal;
using UnityEngine;
using UnityEngine.TestTools;

//Note: RunSync is used only for testing purposes
//Real scenarios should use Run or RunManaged
namespace Test
{
    [TestFixture]
    public class TaskRunnerTests
    {
        [SetUp]
        public void Setup ()
        {
            _vo = new ValueObject();

            _serialTasks1 = new SerialTaskCollection<ValueObject>(_vo);
            _parallelTasks1 = new ParallelTaskCollection();
            _serialTasks2 = new SerialTaskCollection<ValueObject>(_vo);
            _parallelTasks2 = new ParallelTaskCollection();

            _task1 = new Task();
            _task2 = new Task();

            _taskChain1 = new TaskChain();
            _taskChain2 = new TaskChain();
            
            _iterable1 = new Enumerable(10000);
            _iterable2 = new Enumerable(10000);
            _iterable3 = new Enumerable(2000);
            _iterableWithException = new Enumerable(-5);
            
            _taskRunner = TaskRunner.Instance;
            _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine()
                .SetScheduler(new SyncRunner()); //the taskroutine will stall the thread because it runs on the SyncScheduler
        }
        
        [UnityTest]
        public IEnumerator TestCrazyMultiThread()
        {
            ValueObject result = new ValueObject();

            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider"))
            {
                int i = 0;
                while (i++ < 20)
                {
                    var continuationWrapper = crazyEnumerator(result, runner).RunOnScheduler(new SyncRunner());
                    
                    while (continuationWrapper.MoveNext() == true)
                        yield return null;
                }
            }

            Assert.That(result.counter, Is.EqualTo(100));
        }

        IEnumerator crazyEnumerator(ValueObject result, IRunner runner)
        {
            yield return SimpleEnumeratorFast(result).RunOnScheduler(runner);
            yield return SimpleEnumeratorFast(result).RunOnScheduler(runner);
            yield return SimpleEnumeratorFast(result).RunOnScheduler(runner);
            yield return SimpleEnumeratorFast(result).RunOnScheduler(runner);
            yield return SimpleEnumeratorFast(result).RunOnScheduler(runner);
        }   

        [UnityTest]
        public IEnumerator TestEnumerablesAreExecutedInSerialAndOnCompleteIsCalled()
        {
            yield return null;
            
            bool isDone = false;
            _serialTasks1.onComplete += () => isDone = true;

            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterable2.GetEnumerator());
            
            _taskRunner.RunOnScheduler(new SyncRunner(),_serialTasks1);

            Assert.IsTrue(_iterable1.AllRight && _iterable2.AllRight &&
                          _iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime && isDone);
        }

        [UnityTest]
        public IEnumerator TestSerialBreakIt()
        {
            yield return null;
            
            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(BreakIt());
            _serialTasks1.Add(_iterable2.GetEnumerator());

            _taskRunner.RunOnScheduler(new SyncRunner(), _serialTasks1);

            Assert.That(_iterable1.AllRight == true && _iterable2.AllRight == false);
        }

        IEnumerator BreakIt()
        {
            yield return Break.It;
        }

        [UnityTest]
        public IEnumerator TestParallelBreakIt()
        {
            yield return null;
            
            _parallelTasks1.Add(_iterable1.GetEnumerator());
            _parallelTasks1.Add(BreakIt());
            _parallelTasks1.Add(_iterable2.GetEnumerator());

            _taskRunner.RunOnScheduler(new SyncRunner(), _parallelTasks1);

            Assert.That(_iterable1.AllRight == false && _iterable1.iterations == 1 && 
                _iterable2.AllRight == false && _iterable2.iterations == 0);
        }

        [UnityTest]
        public IEnumerator TestBreakIt()
        {
            yield return null;
            
            SeveralTasks().RunOnScheduler(new SyncRunner());

            Assert.That(_iterable1.AllRight == true && _iterable2.AllRight == false);
        }

        class SimpleEnumeratorClass : IEnumerator
        {
            public bool MoveNext()
            {
                Thread.Sleep(100);
                return false;
            }

            public void Reset()
            {
                
            }

            public object Current { get; }
        }

        [UnityTest]
        public IEnumerator TestStaggeredMonoRunner()
        {
            var frames = 0;
            
            var staggeredMonoRunner = new StaggeredMonoRunner("staggered", 4);
            
            for (int i = 0; i < 32; i++)
                new SimpleEnumeratorClass().RunOnScheduler(staggeredMonoRunner);

            var runnerBehaviour = staggeredMonoRunner._go.GetComponent<RunnerBehaviourUpdate>();
            
            frames++;
            runnerBehaviour.Update();
            
            while (staggeredMonoRunner.numberOfRunningTasks > 0)
            {
                frames++;
                runnerBehaviour.Update();
                yield return null;
            }

            Assert.That(frames, Is.EqualTo(8));
        }
        
        [UnityTest]
        public IEnumerator TestTimeBoundMonoRunner()
        {
            var frames = 0;
            
            var timeBoundMonoRunner = new TimeBoundMonoRunner("timebound", 200);
            
            for (int i = 0; i < 32; i++)
                new SimpleEnumeratorClass().RunOnScheduler(timeBoundMonoRunner);

            var runnerBehaviour = timeBoundMonoRunner._go.GetComponent<RunnerBehaviourUpdate>();
            
            frames++;
            runnerBehaviour.Update();
            
            while (timeBoundMonoRunner.numberOfRunningTasks > 0)
            {
                frames++;
                runnerBehaviour.Update();
                yield return null;
            }

            Assert.That(frames, Is.EqualTo(16));
        }
        
        [UnityTest]
        public IEnumerator TestTimeSlicedMonoRunner()
        {
            var frames = 0;
            
            var timeSlicedMonoRunner = new TimeSlicedMonoRunner("timesliced", 2000);
            
            for (int i = 0; i < 100; i++)
                new SimpleEnumeratorClass().RunOnScheduler(timeSlicedMonoRunner);

            var runnerBehaviour = timeSlicedMonoRunner._go.GetComponent<RunnerBehaviourUpdate>();
            
            frames++;
            runnerBehaviour.Update();
            
            while (timeSlicedMonoRunner.numberOfRunningTasks > 0)
            {
                frames++;
                runnerBehaviour.Update();
                yield return null;
            }

            Assert.That(frames, Is.EqualTo(5));
        }

        IEnumerator SeveralTasks()
        {
            yield return _iterable1.GetEnumerator();

            yield return Break.It;

            yield return _iterable2.GetEnumerator();
        }

        [UnityTest]
        public IEnumerator TestEnumerablesAreExecutedInSerialWithReusableTask()
        {
            yield return null;
            
            _reusableTaskRoutine.SetEnumerator(TestSerialTwice()).Start();
        }

        IEnumerator TestSerialTwice()
        {
            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterable2.GetEnumerator());

            yield return _serialTasks1;

            Assert.That(_iterable1.AllRight && _iterable2.AllRight && (_iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime), Is.True);

            _iterable1.Reset(); _iterable2.Reset();
            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterable2.GetEnumerator());

            yield return _serialTasks1;

            Assert.That(_iterable1.AllRight && _iterable2.AllRight && (_iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime), Is.True);
        }

        [UnityTest]
        public IEnumerator TestEnumerableAreExecutedInParallel()
        {
            yield return null;
            
            _parallelTasks1.onComplete += () => { Assert.That(_iterable1.AllRight && _iterable2.AllRight); };

            _parallelTasks1.Add(_iterable1.GetEnumerator());
            _parallelTasks1.Add(_iterable2.GetEnumerator());

            _taskRunner.RunOnScheduler(new SyncRunner(),_parallelTasks1);
        }

        [UnityTest]
        public IEnumerator TestPromisesExceptionHandler()
        {
            yield return null;
            
            bool allDone = false;

            //this must never happen
            _serialTasks1.onComplete += () =>
                                        {
                                            allDone = true; Assert.That(false);
                                        };
            
            _serialTasks1.Add (_iterable1.GetEnumerator());
            _serialTasks1.Add (_iterableWithException.GetEnumerator()); //will throw an exception

            bool hasBeenCalled = false;
            try
            {
                _reusableTaskRoutine.SetEnumerator(_serialTasks1).SetScheduler(new SyncRunner()).Start
                    (e => { Assert.That(allDone, Is.False);
                         hasBeenCalled = true;
                     }
                    ); //will catch the exception
            }
            catch
            {
                Assert.That(hasBeenCalled == true);
            }
        }

        [UnityTest]
        public IEnumerator TestPromisesCancellation()
        {
            yield return null;
            
            bool allDone = false;
            bool testDone = false;

            _serialTasks1.onComplete += () => { allDone = true; Assert.That(false); };

            _serialTasks1.Add (_iterable1.GetEnumerator());
            _serialTasks1.Add (_iterable2.GetEnumerator());
            
            //this time we will make the task run on another thread
            _reusableTaskRoutine.SetScheduler(new MultiThreadRunner("TestPromisesCancellation")).
                SetEnumerator(_serialTasks1).Start
                (null, () => { testDone = true; Assert.That(allDone, Is.False); });
            _reusableTaskRoutine.Stop();

            while (testDone == false);
        }

        // Test ITask implementations

        [UnityTest]
        public IEnumerator TestSingleITaskExecution()
        {
            yield return null;
            
            _task1.Execute();
            
            while (_task1.isDone == false);

            Assert.That(_task1.isDone);
        }

        [UnityTest]
        public IEnumerator TestSingleTaskExecutionCallsOnComplete()
        {
            yield return null;
            
            _task1.OnComplete(() => Assert.That(_task1.isDone, Is.True) );
            
            _task1.Execute();

            while (_task1.isDone == false);
        }

        //test parallel and serial tasks

        [UnityTest]
        public IEnumerator TestSerializedTasksAreExecutedInSerial()
        {
            yield return null;
            
            _serialTasks1.onComplete += () => Assert.That(_task1.isDone && _task2.isDone, Is.True); 
            
            _serialTasks1.Add (_task1);
            _serialTasks1.Add (_task2);
            
            _taskRunner.RunOnScheduler(new SyncRunner(),_serialTasks1);
        }

        [UnityTest]
        public IEnumerator TestTask1IsExecutedBeforeTask2()
        {
            yield return null;
            
            bool test1Done = false;
            
            _task1.OnComplete(() => { test1Done = true; });
            _task2.OnComplete(() => { Assert.That (test1Done); });
            
            _serialTasks1.Add (_task1);
            _serialTasks1.Add (_task2);
            
            _taskRunner.RunOnScheduler(new SyncRunner(), _serialTasks1);
        }

        [UnityTest]
        public IEnumerator TestTasksAreExecutedInParallel()
        {
            yield return null;
            
            _parallelTasks1.onComplete += () => Assert.That(_task1.isDone && _task2.isDone, Is.True); 
                
            _parallelTasks1.Add (new TaskWrapper(_task1));
            _parallelTasks1.Add (new TaskWrapper(_task2));
            
            _taskRunner.RunOnScheduler(new SyncRunner(), _parallelTasks1);
        }

        //test parallel/serial tasks combinations

        [UnityTest]
        public IEnumerator TestParallelTasks1IsExecutedBeforeParallelTask2 ()
        {
            yield return null;
            
            TaskRunner.Instance.RunOnScheduler(new SyncRunner(), SerialContinuation());
        }

        [UnityTest]
        public IEnumerator ParallelMultiThread()
        {
            yield return null;
            
            var parallelMultiThread = new MultiThreadedParallelTaskCollection();

            parallelMultiThread.Add(new SlowTask());
            parallelMultiThread.Add(new SlowTask());
            
            var sw = System.Diagnostics.Stopwatch.StartNew();

            parallelMultiThread.Complete();

            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.AtLeast(900));
            Assert.That(sw.ElapsedMilliseconds, Is.AtMost(1100));

        }
        
        [UnityTest]
        public IEnumerator MultiThreadedParallelTaskCollectionRunningOnAnotherThread()
        {
            yield return null;
            
            using (var runner = new MultiThreadRunner("MT"))
            {
                ITaskRoutine routine = TaskRunner.Instance.AllocateNewTaskRoutine();

                routine.SetEnumerator(YieldMultiThreadedParallelTaskCollection()).SetScheduler(runner);

                var continuator = routine.Start();

                while (continuator.MoveNext() == true) yield return null;
            }
        }
        
        [UnityTest]
        public IEnumerator YieldMultiThreadedParallelTaskCollection()
        {
            yield return null;
            
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var parallelMultiThread = new MultiThreadedParallelTaskCollection();

            parallelMultiThread.Add(new SlowTask());
            parallelMultiThread.Add(new SlowTask());

            yield return parallelMultiThread;

            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.AtLeast(900));
            Assert.That(sw.ElapsedMilliseconds, Is.AtMost(1100));
        }

        public class SlowTask : IEnumerator
        {
            DateTime _then;
            public object Current { get; private set; }

            public SlowTask()
            {
                _then = DateTime.Now.AddSeconds(1);
            }

            public bool MoveNext()
            {
                if (DateTime.Now < _then)
                    return true;
                return false;
            }

            public void Reset()
            {
            }
        }

        [UnityTest]
        public IEnumerator TestExtension()
        {
            yield return null;
            
            SerialContinuation().RunOnScheduler(new SyncRunner());
        }

        IEnumerator SerialContinuation()
        {
            bool parallelTasks1Done = false;
            bool parallelTasks2Done = false;

            _parallelTasks1.Add(new TaskWrapper(_task1));
            _parallelTasks1.Add(_iterable1.GetEnumerator());

            yield return _parallelTasks1;
            
            Assert.That(parallelTasks2Done, Is.False); parallelTasks1Done = true;

            _parallelTasks1.Add(new TaskWrapper(_task2));
            _parallelTasks1.Add(_iterable2.GetEnumerator());

            yield return _parallelTasks1;

            Assert.That(parallelTasks1Done, Is.True); parallelTasks2Done = true;
            Assert.That(parallelTasks1Done && parallelTasks2Done);
        }

        [UnityTest]
        public IEnumerator TestParallelTasksAreExecutedInSerial()
        {
            yield return null;
            
            bool parallelTasks1Done = false;
            bool parallelTasks2Done = false;

            _parallelTasks1.Add(new TaskWrapper(_task1));
            _parallelTasks1.Add(_iterable1.GetEnumerator());
            _parallelTasks1.onComplete += () => { Assert.That(parallelTasks2Done, Is.False); parallelTasks1Done = true; };

            _parallelTasks2.Add(new TaskWrapper(_task2));
            _parallelTasks2.Add(_iterable2.GetEnumerator());
            _parallelTasks2.onComplete += () => { Assert.That(parallelTasks1Done, Is.True); parallelTasks2Done = true; };

            _serialTasks1.Add(_parallelTasks1);
            _serialTasks1.Add(_parallelTasks2);
            _serialTasks1.onComplete += () => { Assert.That(parallelTasks1Done && parallelTasks2Done); };
            
            _taskRunner.RunOnScheduler(new SyncRunner(), _serialTasks1);
        }

        //test passage of token between tasks 

        [UnityTest]
        public IEnumerator TestSerialTasks1ExecutedInParallelWithToken ()
        {
            yield return null;
            
            _serialTasks1.Add(_taskChain1);
            _serialTasks1.Add(_taskChain1);
            _serialTasks2.Add(_taskChain2);
            _serialTasks2.Add(_taskChain2);

            _parallelTasks1.Add(_serialTasks1);
            _parallelTasks1.Add(_serialTasks2);

            _parallelTasks1.onComplete += 
                () => Assert.That(_vo.counter, Is.EqualTo(4));

            _taskRunner.RunOnScheduler(new SyncRunner(), _parallelTasks1);
        }

        [UnityTest]
        public IEnumerator TestSerialTasksAreExecutedInParallel ()
        {
            yield return null;
            
            int test1 = 0;
            int test2 = 0;

            _serialTasks1.Add (_iterable1.GetEnumerator());
            _serialTasks1.Add (_iterable2.GetEnumerator());
            _serialTasks1.onComplete += () => { test1++; test2++; }; 

            _serialTasks2.Add (_task1);
            _serialTasks2.Add (_task2);
            _serialTasks2.onComplete += () => { test2++; };

            _parallelTasks1.Add (_serialTasks1);
            _parallelTasks1.Add (_serialTasks2);
            _parallelTasks1.onComplete += () => Assert.That((test1 == 1) && (test2 == 2), Is.True);

            _taskRunner.RunOnScheduler(new SyncRunner(), _parallelTasks1);
        }
        
        [UnityTest]
        public IEnumerator TestStopStartTaskRoutine()
        {
            yield return null;
            
            using (var runner = new MultiThreadRunner("TestStopStartTaskRoutine"))
            {
                _reusableTaskRoutine.SetScheduler(runner);
                _reusableTaskRoutine.SetEnumerator(TestWithThrow());
                _reusableTaskRoutine.Start();
                _reusableTaskRoutine
                    .Stop(); //although it's running in another thread, thanks to the waiting, it should be able to stop in time

                _reusableTaskRoutine.SetScheduler(new SyncRunner());
                var enumerator = TestWithoutThrow();
                var continuator = _reusableTaskRoutine.SetEnumerator(enumerator)
                                    .Start(); //test routine can be reused with another enumerator
                
                while (continuator.MoveNext()) yield return null;
                
                Assert.That((int)enumerator.Current, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineStopStart()
        {
            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStart"))
            {
                ValueObject result = new ValueObject();

                var taskRoutine = _reusableTaskRoutine.SetScheduler(runner).SetEnumerator(SimpleEnumerator(result));
                
                taskRoutine.Start();
                _reusableTaskRoutine.Stop();
                taskRoutine = _reusableTaskRoutine.SetEnumerator(SimpleEnumerator(result));
                
                var continuator = taskRoutine.Start();
                
                while (continuator.MoveNext()) yield return null;

                Assert.That(result.counter == 1);
            }
        }
        
        [UnityTest]
        public IEnumerator TestMultiThreadParallelTaskCompletes()
        {
            yield return null;
            
            var test = new MultiThreadedParallelTaskCollection();

            bool done = false;
            test.onComplete += () => done = true;
            Token token = new Token();
        
            test.Add(new WaitEnumerator(token));
            test.Add(new WaitEnumerator(token));
            test.Add(new WaitEnumerator(token));
            test.Add(new WaitEnumerator(token));

            test.RunOnScheduler(StandardSchedulers.multiThreadScheduler);
            DateTime now = DateTime.Now;
            yield return null;
            
            while (test.isRunning) 
                yield return null;

            var totalSeconds = (DateTime.Now - now).TotalSeconds;
            
            Assert.Greater(totalSeconds, 1.9);
            Assert.Less(totalSeconds, 2.1);
            Assert.That(done, Is.True);
            Assert.AreEqual(4, token.count);
        }
        
        class Token
        {
            public int count;
        }
        
        class WaitEnumerator:IEnumerator
        {
            Token _token;

            public WaitEnumerator(Token token)
            {
                _token   = token;
                _future = DateTime.UtcNow.AddSeconds(2);
            }
        
            public void Reset()
            {
                _future      = DateTime.UtcNow.AddSeconds(2);
                _token.count = 0;
            }

            public object Current { get { return null; } }

            DateTime _future;

            public bool MoveNext()
            {
                Thread.Sleep(1);
                if (_future <= DateTime.UtcNow)
                {
                    Interlocked.Increment(ref _token.count);
        
                    return false;
                }

                return true;
            }
        }
        
        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineStartStart()
        {
            yield return null;
            
            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStartStart"))
            {
                ValueObject result = new ValueObject();

                var taskRoutine = _reusableTaskRoutine.SetScheduler(runner).SetEnumeratorProvider(() => SimpleEnumerator(result));
                taskRoutine.Start();
                var continuator = taskRoutine.Start();
                
                while (continuator.MoveNext()) yield return null;

                Assert.That(result.counter == 1);
            }
        }

        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineStopStartWithProvider()
        {
            yield return null;
            
            ValueObject result = new ValueObject();

            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider"))
            {
                var continuator =_reusableTaskRoutine.SetScheduler(runner)
                                    .SetEnumerator(SimpleEnumeratorLong(result)).Start();
                
                Assert.That(continuator.completed == false, "can't be completed");
                _reusableTaskRoutine.Stop();
                
                Thread.Sleep(500); //let's be sure it's completed
                
                Assert.That(continuator.completed == true, "must be completed");
                
                continuator = 
                    _reusableTaskRoutine.SetEnumeratorProvider(() => SimpleEnumerator(result))
                                        .Start();
                
                while (continuator.MoveNext()) yield return null;
            }

            Assert.That(result.counter, Is.EqualTo(1));
        }
        
        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineRestartsWithProvider()
        {
            yield return null;
            
            ValueObject result = new ValueObject();

            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider"))
            {
                var continuator = _reusableTaskRoutine.SetScheduler(runner)
                                    .SetEnumerator(SimpleEnumeratorLong(result)).Start();
                Assert.That(continuator.completed == false, "can't be completed");
                continuator = 
                    _reusableTaskRoutine.SetEnumeratorProvider(() => SimpleEnumerator(result)).Start();

                while (continuator.MoveNext()) yield return null;
            }

            Assert.That(result.counter, Is.EqualTo(1));
        }
        
        [UnityTest]
        public IEnumerator TestParallelTimeOut()
        {
            yield return null;
            
            _parallelTasks1.Add(new TimeoutEnumerator());
            _parallelTasks1.Add(new TimeoutEnumerator());
            _parallelTasks1.Add(new TimeoutEnumerator());
            _parallelTasks1.Add(new TimeoutEnumerator());
            _parallelTasks1.Add(new TimeoutEnumerator());
            _parallelTasks1.Add(new TimeoutEnumerator());
            _parallelTasks1.Add(new TimeoutEnumerator());
            _parallelTasks1.Add(new TimeoutEnumerator());
            _parallelTasks1.Add(new TimeoutEnumerator());

            DateTime then = DateTime.Now;
            _taskRunner.RunOnScheduler(new SyncRunner(), _parallelTasks1);

            var totalSeconds = (DateTime.Now - then).TotalSeconds;
            Assert.That(totalSeconds, Is.InRange(1.0, 1.1));
        }
        
        [UnityTest]
        public IEnumerator TestParallelWait()
        {
            yield return null;
            
            _parallelTasks1.Add(new WaitForSecondsEnumerator(1));
            _parallelTasks1.Add(new WaitForSecondsEnumerator(1));
            _parallelTasks1.Add(new WaitForSecondsEnumerator(1));
            _parallelTasks1.Add(new WaitForSecondsEnumerator(1));
            _parallelTasks1.Add(new WaitForSecondsEnumerator(1));
            _parallelTasks1.Add(new WaitForSecondsEnumerator(1));
            _parallelTasks1.Add(new WaitForSecondsEnumerator(1));
            _parallelTasks1.Add(new WaitForSecondsEnumerator(1));
            _parallelTasks1.Add(new WaitForSecondsEnumerator(1));

            DateTime then = DateTime.Now;
            _taskRunner.RunOnScheduler(new SyncRunner(), _parallelTasks1);

            var totalSeconds = (DateTime.Now - then).TotalSeconds;
            Assert.That(totalSeconds, Is.InRange(1.0, 1.1));
        }
        
        [UnityTest]
        public IEnumerator TestParallelUnityWait()
        {
            yield return null;
            
            _parallelTasks1.Add(new WaitForSecondsU().GetEnumerator());
            _parallelTasks1.Add(new WaitForSecondsU().GetEnumerator());
            _parallelTasks1.Add(new WaitForSecondsU().GetEnumerator());
            _parallelTasks1.Add(new WaitForSecondsU().GetEnumerator());
            _parallelTasks1.Add(new WaitForSecondsU().GetEnumerator());
            _parallelTasks1.Add(new WaitForSecondsU().GetEnumerator());
            _parallelTasks1.Add(new WaitForSecondsU().GetEnumerator());
            _parallelTasks1.Add(new WaitForSecondsU().GetEnumerator());
            _parallelTasks1.Add(new WaitForSecondsU().GetEnumerator());

            DateTime then = DateTime.Now;
            _taskRunner.RunOnScheduler(new SyncRunner(), _parallelTasks1);

            var totalSeconds = (DateTime.Now - then).TotalSeconds;
            Assert.That(totalSeconds, Is.InRange(1.0, 1.1));
        }
        
        IEnumerator SimpleEnumeratorLong(ValueObject result)
        {
            yield return new WaitForSecondsEnumerator(10);
        }

        IEnumerator SimpleEnumerator(ValueObject result)
        {
            yield return new WaitForSecondsEnumerator(1);

            Interlocked.Increment(ref result.counter);
        }
        
        IEnumerator SimpleEnumeratorFast(ValueObject result)
        {
            yield return null;

            Interlocked.Increment(ref result.counter);
        }

        IEnumerator TestWithThrow()
        {
            yield return new WaitForSecondsEnumerator(0.1f);

            throw new Exception();
        }

        IEnumerator TestWithoutThrow()
        {
            yield return 1;
        }

        public class TimeoutEnumerator : IEnumerator
        {
            public TimeoutEnumerator()
            {
                _then = DateTime.Now;
            }

            public bool MoveNext()
            {
                var timePassed = (float) (DateTime.Now - _then).TotalSeconds;

                if (timePassed > 1)
                    return false;

                return true;
            }

            public void Reset()
            {}

            public object Current { get; private set; }

            DateTime _then;
        }

        [UnityTest]
        public IEnumerator TestComplexCoroutine()
        {
            yield return null;
            
            _taskRunner.RunOnScheduler(new SyncRunner(),
                ComplexEnumerator((i) => Assert.That(i == 100, Is.True)));
        }

        [UnityTest]
        public IEnumerator TestMultithreadWithPooledTasks()
        {
            yield return null;
            
            using (var runner = new MultiThreadRunner("TestMultithread"))
            {
                _iterable1.Reset();

                var continuator = _iterable1.GetEnumerator().RunOnScheduler(runner);
                
                while (continuator.MoveNext());

                Assert.That(_iterable1.AllRight == true);

                _iterable1.Reset();

                continuator = _iterable1.GetEnumerator().RunOnScheduler(runner);

                while (continuator.MoveNext());

                Assert.That(_iterable1.AllRight == true);
            }
        }
        
        [UnityTest]
        public IEnumerator TestMultithreadWitTaskRoutines()
        {
            yield return null;
            
            using (var runner = new MultiThreadRunner("TestMultithread"))
            {
                _iterable1.Reset();

                _reusableTaskRoutine.SetEnumerator(_iterable1.GetEnumerator());
                
                var continuator = _reusableTaskRoutine.SetScheduler(runner).Start();
                
                while (continuator.MoveNext());

                Assert.That(_iterable1.AllRight == true);

                _iterable1.Reset();
                
                _reusableTaskRoutine.SetEnumerator(_iterable1.GetEnumerator());
                
                continuator = _reusableTaskRoutine.Start();

                while (continuator.MoveNext());

                Assert.That(_iterable1.AllRight == true);
            }
        }

        [UnityTest]
        public IEnumerator TestMultithreadQuick()
        {
            yield return null;
            
            using (var runner = new MultiThreadRunner("TestMultithreadQuick", false))
            {
                var task = _iterable1.GetEnumerator().RunOnScheduler(runner);

                while (task.MoveNext());

                Assert.That(_iterable1.AllRight == true);

                //do it again to test if starting another task works

                _iterable1.Reset();

                task = _iterable1.GetEnumerator().RunOnScheduler(runner);

                while (task.MoveNext());

                Assert.That(_iterable1.AllRight == true);
            }
        }

        [UnityTest]
        public IEnumerator TestMultithreadIntervaled()
        {
            yield return null;
            
            using (var runner = new MultiThreadRunner("TestMultithreadIntervaled", 1))
            {
                ITaskRoutine taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine();
                taskRoutine.SetEnumerator(_iterable3.GetEnumerator()).SetScheduler(runner);
                
                DateTime now = DateTime.Now;

                taskRoutine.Start().Complete();

                var seconds = (DateTime.Now - now).TotalSeconds;

                //2000 iteration * 1ms = 2 seconds

                Assert.That((int)seconds, Is.EqualTo(2));
                Assert.IsTrue(_iterable3.AllRight);
            }
        }

        IEnumerator ComplexEnumerator(Action<int> callback)    
        {
            int i = 0;
            int j = 0;
            while (j < 10)
            {
                j++;

                var enumerator = SubEnumerator(i);
                yield return enumerator;  //it will be executed on the same frame
                i = (int)enumerator.Current; //carefull it will be unboxed
            }

            callback(i);
        }

        IEnumerator SubEnumerator(int i)
        {
            int count = i + 10;
            while (++i < count)
                yield return null; //enable asynchronous execution

            yield return i; //careful it will be boxed;
        }

        TaskRunner _taskRunner;
        ITaskRoutine _reusableTaskRoutine;

        SerialTaskCollection<ValueObject> _serialTasks1;
        SerialTaskCollection<ValueObject> _serialTasks2;
        ParallelTaskCollection _parallelTasks1;
        ParallelTaskCollection _parallelTasks2;

        Task _task1;
        Task _task2;

        Enumerable _iterable1;
        Enumerable _iterable2;
        Enumerable _iterableWithException;
        
        TaskChain _taskChain1;
        TaskChain _taskChain2;
        ValueObject _vo;
        Enumerable _iterable3;


        class Task : ITask
        {
            //ITask Implementation
            public bool  isDone { get; private set; }

            public Task()
            {
                isDone = false;
            }

            //ITask Implementation
            public void Execute() 
            {
                _delayTimer = new System.Timers.Timer
                {
                    Interval = 1000,
                    Enabled = true
                };
                _delayTimer.Elapsed += _delayTimer_Elapsed;
                _delayTimer.Start();
            }

            public void  OnComplete(Action action)
            {
                _onComplete += action;
            }

            void _delayTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
            {
                isDone = true;
                if (_onComplete != null)
                    _onComplete();

                _delayTimer.Stop();
                _delayTimer = null;
            }

            System.Timers.Timer _delayTimer;
            Action _onComplete;
        }

        class TaskChain: ITaskChain<ValueObject>
        {
            public bool  isDone { get; private set; }
            
            public TaskChain()
            {
                isDone = false;
            }

            public void Execute(ValueObject token)
            {
                Interlocked.Increment(ref token.counter);

                isDone = true;
            }
        }

        class ValueObject
        {
            public int counter;
        }

        class Enumerable : IEnumerable
        {
            public long endOfExecutionTime {get; private set;}

            public bool AllRight { get 
            {
                return iterations == totalIterations; 
            }}

            public Enumerable(int niterations)
            {
                iterations = 0; 
                totalIterations = niterations;
            }

            public void Reset()
            {
                iterations = 0;
            }

            public IEnumerator GetEnumerator()
            {
                if (totalIterations < 0)
                    throw new Exception("can't handle this");

                while (iterations < totalIterations)
                {
                    iterations++;

                    yield return null;
                }
                
                endOfExecutionTime = DateTime.Now.Ticks;
            }

            readonly int totalIterations;
            public int iterations;
        }
    }

    public class WaitForSecondsU : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new WaitForSecondsRealtime(1);
        }
    }
}
#endif