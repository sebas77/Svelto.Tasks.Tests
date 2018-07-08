#if !NETFX_CORE

using NUnit.Framework;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using UnityEngine;
using UnityEngine.TestTools;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTests
    {
        [SetUp]
        public void Setup ()
        {
            _vo = new ValueObject();

            _serialTasks1 = new SerialTaskCollection<TestEnumerator>();
            _parallelTasks1 = new ParallelTaskCollection<IEnumerator>();
            _serialTasks2 = new SerialTaskCollection();
            _parallelTasks2 = new ParallelTaskCollection();

            _task1 = new Task();
            _task2 = new Task();

            _asyncTaskChain1 = new AsyncTask();
            _asyncTaskChain2 = new AsyncTask();
            
            _iterable1 = new TestEnumerator(10000);
            _iterable2 = new TestEnumerator(10000);
            _iterableWithException = new TestEnumerator(-5);
            
            _taskRunner = TaskRunner.Instance;
            //the taskroutine will stall the thread because it runs on the SyncScheduler
            _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(new SyncRunner());
        }
        
        [Test]
        public void TestTaskCollectionReturningTaskCollection()
        {}
        
        [Test]
        public void TestTaskCollectionReturningItself()
        {}
        
        [Test]
        public void RestartSerialTask()
        {}
        
        [Test]
        public void RestartParallelTask()
        {}

        [Test]
        public void TestUnityInstruction()
        {}

        [Test]
        public void TestContinuationWrapper()
        {}

        [Test]
        public void TaskCollectionCanRunMultipleTimesIfResettable()
        {}
        
        [Test]
        public void TestTaskCanBeAddedToCOllectionWhileRunning()
        {}

        /// <summary>
        /// TODO: Test Coroutine Wrapper
        /// TODO: a serial task should be able to run in the same runner of its enumerator?
        /// </summary>
        /// <returns></returns>
        /*  [UnityTest]
          public IEnumerator TestStopStartTaskRoutine()
          {
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
          public IEnumerator TestSimpleTaskRoutineStopStartWithProvider()
          {
              ValueObject result = new ValueObject();
  
              using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider"))
              {
                  var continuator =_reusableTaskRoutine.SetScheduler(runner)
                                      .SetEnumerator(SimpleEnumeratorLong(result)).Start();
                  
                  Assert.That(continuator.completed == false, "can't be completed");
                  _reusableTaskRoutine.Stop();
                  
                  Thread.Sleep(500); //let's be sure it's cmompleted
                  
                  Assert.That(continuator.completed == true, "must be completed");
                  
                  continuator = 
                      _reusableTaskRoutine.SetEnumeratorProvider(() => SimpleEnumerator(result))
                                          .Start();
                  
                  while (continuator.MoveNext()) yield return null;
              }
  
              Assert.That(result.counter == 1);
          }
  */
        [Test]
        public void TestInefficientNestedEnumerator()
        {
            var routine = _taskRunner.AllocateNewTaskRoutine(new SyncRunner()).SetEnumerator(Inefficent());

            routine.Start();
        }

        IEnumerator Inefficent()
        {
            var routine = _taskRunner.AllocateNewTaskRoutine(new SyncRunner<TestEnumerator>()).SetEnumeratorRef(ref _iterable1);
            int i = 0;
            while (i++ < 10)
                yield return routine.Start();
        }
        
        [Test]
        public void TestValueEnumeratorIsExecuted()
        {
            //in order to achieve allocation 0 code
            //it's currently necessary to preallocate 
            //a TaskRoutine and relative runner.
            var routine = _taskRunner.AllocateNewTaskRoutine(new SyncRunner<TestEnumerator>()).SetEnumeratorRef(ref _iterable1);
            
            //this will be allocation 0
            var wrapper = routine.Start();

            //TODO: routine or wrapper to have the enumerator? What if the routine is completed
            Assert.That(wrapper.Current.AllRight, Is.True);
        }
        /*
        [Test]
        public void TestComplexValueEnumeratorIsExecuted()
        {
            //in order to achieve allocation 0 code
            //it's currently necessary to preallocate 
            //a TaskRoutine and relative runner.
            var routine    = TaskRunner.Instance.AllocateNewTaskRoutine<Enumerator>();
            var syncRunner = new SyncRunner<Enumerator>();
            
            //the best part is that setting new enumerator
            //even when capturing external variable
            //will be free

            for (int i = 0; i < 5; i++)
            {
                routine.SetEnumerator(new Enumerator(10)).Start();

                Assert.That(_iterable1.AllRight, Is.True);
            }
        }
        
        [Test]
        public void TestEnumerablesAreExecutedInSerial()
        {
            _serialTasks1.Add(_iterable1);
            _serialTasks1.Add(_iterable2);
            
            _serialTasks1.RunOnSchedule(new SyncRunner<SerialTaskCollection<Enumerator>>());
            
            Assert.That
                (_iterable1.AllRight && _iterable2.AllRight);
            Assert.That(_iterable1.endOfExecutionTime, Is.LessThan(_iterable2.startOfExecutionTime));
            Assert.That(_iterable2.startOfExecutionTime, Is.LessThan(_iterable2.endOfExecutionTime));
        }
        
        [Test]
        public void TestEnumerablesAreExecutedInSerialTestOnComplete()
        {
            _serialTasks1.Add(_iterable1);
            _serialTasks1.Add(_iterable2);
            
            _serialTasks1.onComplete += () =>
                                        {
                                            Assert.That
                                                (_iterable1.AllRight && _iterable2.AllRight);
                                            Assert.That(_iterable1.endOfExecutionTime, Is.LessThan(_iterable2.startOfExecutionTime));
                                            Assert.That(_iterable2.startOfExecutionTime, Is.LessThan(_iterable2.endOfExecutionTime));
                                        };
            
            _serialTasks1.RunOnSchedule(new SyncRunner());
        }

        [Test]
        public void TestYieldingNotStartingTaskRoutineThrows()
        {
            
        }

        [Test]
        public void TestSerialBreakIt()
        {
            _serialTasks2.Add(_iterable1);
            _serialTasks2.Add(BreakIt());
            _serialTasks2.Add(_iterable2);

            _taskRunner.RunOnSchedule(new SyncRunner(), _serialTasks2);

            Assert.That(_iterable1.AllRight == true && _iterable2.AllRight == false);
        }

        IEnumerator BreakIt()
        {
            yield return Break.It;
        }

        [Test]
        public void TestParallelBreakIt()
        {
            _parallelTasks1.Add(_iterable1);
            _parallelTasks1.Add(BreakIt());
            _parallelTasks1.Add(_iterable2);

            _taskRunner.RunOnSchedule(new SyncRunner(), _parallelTasks1);

            Assert.That(_iterable1.AllRight == false && _iterable1.iterations == 1 && 
                _iterable2.AllRight == false && _iterable2.iterations == 0);
        }

        [Test]
        public void TestBreakIt()
        {
             _taskRunner.RunOnSchedule(new SyncRunner(),SeveralTasks());

             Assert.That(_iterable1.AllRight == true && _iterable2.AllRight == false);
        }

        IEnumerator SeveralTasks()
        {
            yield return _iterable1;

            yield return Break.It;

            yield return _iterable2;
        }

        [Test]
        public void TestEnumerablesAreExecutedInSerialWithReusableTask()
        {
            _reusableTaskRoutine.SetEnumerator(TestSerialTwice()).Start();
        }

        IEnumerator TestSerialTwice()
        {
            _serialTasks1.Add(_iterable1);
            _serialTasks1.Add(_iterable2);

            yield return _serialTasks1;

            Assert.That(_iterable1.AllRight && _iterable2.AllRight && (_iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime), Is.True);

            _iterable1.Reset(); _iterable2.Reset();
            _serialTasks1.Add(_iterable1);
            _serialTasks1.Add(_iterable2);

            yield return _serialTasks1;

            Assert.That(_iterable1.AllRight && _iterable2.AllRight && (_iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime), Is.True);
        }

        [Test]
        public void TestCollectionCallbacksAreCalled()
        {
            
        }

        [Test] 
        public void TestEnumerableAreExecutedInParallel()
        {
            var iterable3 = new Enumerator(5000);
            
            _parallelTasks1.Add (_iterable1);
            _parallelTasks1.Add (iterable3);
            _parallelTasks1.Add (_iterable2);

            _parallelTasks1.MoveNext();

            Assert.That(_iterable1.iterations, Is.EqualTo(1));
            Assert.That(_iterable2.iterations, Is.EqualTo(1));
            Assert.That(iterable3.iterations, Is.EqualTo(1));
            
            _taskRunner.RunOnSchedule(new SyncRunner(),_parallelTasks1);
            
            Assert.That(_iterable1.AllRight && _iterable2.AllRight && iterable3.AllRight && 
                        (iterable3.endOfExecutionTime <= _iterable1.endOfExecutionTime) && (iterable3.startOfExecutionTime < _iterable2.endOfExecutionTime), Is.True);
        }

        [Test]
        public void TestPromisesExceptionHandler()
        {
            bool allDone = false;

            //this must never happen
            _serialTasks1.onComplete += () =>
                                        {
                                            allDone = true; Assert.That(false);
                                        };
            
            _serialTasks1.Add (_iterable1);
            _serialTasks1.Add (_iterableWithException); //will throw an exception

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

        [Test]
        public void TestPromisesCancellation()
        {
            bool allDone = false;
            bool testDone = false;

            _serialTasks1.onComplete += () => { allDone = true; Assert.That(false); };

            _serialTasks1.Add (_iterable1);
            _serialTasks1.Add (_iterable2);
            
            //this time we will make the task run on another thread
            _reusableTaskRoutine.SetScheduler(new MultiThreadRunner("TestPromisesCancellation")).
                SetEnumerator(_serialTasks1).Start
                (null, () => { testDone = true; Assert.That(allDone, Is.False); });
            _reusableTaskRoutine.Stop();

            while (testDone == false);
        }

        // Test ITask implementations

        [Test]
        public void TestSingleITaskExecution()
        {
            _task1.Execute();
            
            while (_task1.isDone == false);

            Assert.That(_task1.isDone);
        }

        [Test]
        public void TestSingleTaskExecutionCallsOnComplete()
        {
            _task1.OnComplete(() => Assert.That(_task1.isDone, Is.True) );
            
            _task1.Execute();

            while (_task1.isDone == false);
        }

        //test parallel and serial tasks

        [Test]
        public void TestSerializedTasksAreExecutedInSerial()
        {
            _serialTasks1.onComplete += () => Assert.That(_task1.isDone && _task2.isDone, Is.True); 
            
            _serialTasks1.Add (_task1);
            _serialTasks1.Add (_task2);
            
            _taskRunner.RunOnSchedule(new SyncRunner(),_serialTasks1);
        }

        [Test]
        public void TestTask1IsExecutedBeforeTask2()
        {
            bool test1Done = false;
            
            _task1.OnComplete(() => { test1Done = true; });
            _task2.OnComplete(() => { Assert.That (test1Done); });
            
            _serialTasks1.Add (_task1);
            _serialTasks1.Add (_task2);
            
            _taskRunner.RunOnSchedule(new SyncRunner(), _serialTasks1);
        }

        [Test]
        public void TestTasksAreExecutedInParallel()
        {
            _parallelTasks1.onComplete += () => Assert.That(_task1.isDone && _task2.isDone, Is.True); 
                
            _parallelTasks1.Add (_task1);
            _parallelTasks1.Add (_task2);
            
            _taskRunner.RunOnSchedule(new SyncRunner(), _parallelTasks1);
        }

        //test parallel/serial tasks combinations

        [Test]
        public void TestParallelTasks1IsExecutedBeforeParallelTask2 ()
        {
            TaskRunner.Instance.RunOnSchedule(new SyncRunner(), SerialContinuation());
        }

        [Test]
        public void ParallelMultiThread()
        {
            var parallelMultiThread = new MultiThreadedParallelTaskCollection();

            parallelMultiThread.Add(new SlowTask());
            parallelMultiThread.Add(new SlowTask());
            
            var sw = System.Diagnostics.Stopwatch.StartNew();

            parallelMultiThread.Complete();

            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.AtLeast(1000));
            Assert.That(sw.ElapsedMilliseconds, Is.AtMost(1100));

        }
        
        [UnityTest]
        public IEnumerator ParalelMultiThreadOnAnotherRunner()
        {
            using (var runner = new MultiThreadRunner("MT"))
            {
                var routine = TaskRunner.Instance.AllocateNewTaskRoutine();

                routine.SetEnumerator(ParallelMultiThreadWithYielding()).SetScheduler(runner);

                var continuator = routine.Start();

                while (continuator.MoveNext() == true) yield return null;
            }
        }
        
        [UnityTest]
        public IEnumerator ParallelMultiThreadWithYielding()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var parallelMultiThread = new MultiThreadedParallelTaskCollection();

            parallelMultiThread.Add(new SlowTask());
            parallelMultiThread.Add(new SlowTask());

            while (parallelMultiThread.MoveNext() == true) yield return null;

            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.AtLeast(1000));
            Assert.That(sw.ElapsedMilliseconds, Is.AtMost(1100));
        }

        public class SlowTask : IEnumerator
        {
            public object Current { get; private set; }

            public SlowTask()
            {}

            public bool MoveNext()
            {
                System.Threading.Thread.Sleep(1000);
                
                return false;
            }

            public void Reset()
            {
            }
        }

        [Test]
        public void TestExtension()
        {
            SerialContinuation().RunOnSchedule(new SyncRunner());
        }

        IEnumerator SerialContinuation()
        {
            bool parallelTasks1Done = false;
            bool parallelTasks2Done = false;

            _parallelTasks1.Add(_task1);
            _parallelTasks1.Add(_iterable1);

            yield return _parallelTasks1;
            
            Assert.That(parallelTasks2Done, Is.False); parallelTasks1Done = true;

            _parallelTasks1.Add(_task2);
            _parallelTasks1.Add(_iterable2);

            yield return _parallelTasks1;

            Assert.That(parallelTasks1Done, Is.True); parallelTasks2Done = true;
            Assert.That(parallelTasks1Done && parallelTasks2Done);
        }

        [Test]
        public void TestParallelTasksAreExecutedInSerial()
        {
            bool parallelTasks1Done = false;
            bool parallelTasks2Done = false;

            _parallelTasks1.Add(_task1);
            _parallelTasks1.Add(_iterable1);
            _parallelTasks1.onComplete += () => { Assert.That(parallelTasks2Done, Is.False); parallelTasks1Done = true; };

            _parallelTasks2.Add(_task2);
            _parallelTasks2.Add(_iterable2);
            _parallelTasks2.onComplete += () => { Assert.That(parallelTasks1Done, Is.True); parallelTasks2Done = true; };

            _serialTasks2.Add(_parallelTasks1);
            _serialTasks2.Add(_parallelTasks2);
            _serialTasks2.onComplete += () => { Assert.That(parallelTasks1Done && parallelTasks2Done); };
            
            _taskRunner.RunOnSchedule(new SyncRunner(), _serialTasks2);
        }

        //test passage of token between tasks 

        [Test]
        public void TestSerialTasks1ExecutedInParallelWithToken ()
        {
            _serialTasks1.Add(_asyncTaskChain1);
            _serialTasks1.Add(_asyncTaskChain1);
            _serialTasks2.Add(_asyncTaskChain2);
            _serialTasks2.Add(_asyncTaskChain2);

            _parallelTasks1.Add(_serialTasks1);
            _parallelTasks1.Add(_serialTasks2);

            _parallelTasks1.onComplete += 
                () => Assert.That(_vo.counter, Is.EqualTo(4));

            _taskRunner.RunOnSchedule(new SyncRunner(), _parallelTasks1);
        }

        [Test]
        public void TestSerialTasksAreExecutedInParallel ()
        {
            int test1 = 0;
            int test2 = 0;

            _serialTasks1.Add (_iterable1);
            _serialTasks1.Add (_iterable2);
            _serialTasks1.onComplete += () => { test1++; test2++; }; 

            _serialTasks2.Add (_task1);
            _serialTasks2.Add (_task2);
            _serialTasks2.onComplete += () => { test2++; };

            _parallelTasks1.Add (_serialTasks1);
            _parallelTasks1.Add (_serialTasks2);
            _parallelTasks1.onComplete += () => Assert.That((test1 == 1) && (test2 == 2), Is.True);

            _taskRunner.RunOnSchedule(new SyncRunner(), _parallelTasks1);
        }
        
        [Test]
        public void TestMultiThreadParallelTaskComplete()
        {
            var test = new MultiThreadedParallelTaskCollection(4);

            bool done = false;
            test.onComplete += () => done = true;
            Token token = new Token();
        
            test.Add(new WaitEnumerator(token));
            test.Add(new WaitEnumerator(token));
            test.Add(new WaitEnumerator(token));
            test.Add(new WaitEnumerator(token));

            test.Complete();
        
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
        public IEnumerator TestStopStartTaskRoutine()
        {
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
        public IEnumerator TestSimpleTaskRoutineStopStartWithProvider()
        {
            ValueObject result = new ValueObject();

            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider"))
            {
                var continuator =_reusableTaskRoutine.SetScheduler(runner)
                                    .SetEnumerator(SimpleEnumeratorLong(result)).Start();
                
                Assert.That(continuator.completed == false, "can't be completed");
                _reusableTaskRoutine.Stop();
                
                Thread.Sleep(500); //let's be sure it's cmompleted
                
                Assert.That(continuator.completed == true, "must be completed");
                
                continuator = 
                    _reusableTaskRoutine.SetEnumeratorProvider(() => SimpleEnumerator(result))
                                        .Start();
                
                while (continuator.MoveNext()) yield return null;
            }

            Assert.That(result.counter == 1);
        }
        
        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineReStartWithProvider()
        {
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
        
        [Test]
        public void TestParallelTimeOut()
        {
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
            _taskRunner.RunOnSchedule(new SyncRunner(), _parallelTasks1);

            var totalSeconds = (DateTime.Now - then).TotalSeconds;
            Assert.That(totalSeconds, Is.InRange(1.0, 1.1));
        }
        
        [UnityTest]
        public IEnumerator TestCrazyMultiThread()
        {
            ValueObject result = new ValueObject();

            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider"))
            {
                int i = 0;
                while (i++ < 200)
                {
                    crazyEnumerator(result, runner).RunOnSchedule(new SyncRunner());

                    yield return null;
                }
            }

            Assert.That(result.counter == 1000);
        }

        IEnumerator crazyEnumerator(ValueObject result, IRunner runner)
        {
            var taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine().SetEnumeratorProvider(() => SimpleEnumeratorFast(result)).SetScheduler(runner);

            yield return taskRoutine;
            yield return taskRoutine;
            yield return taskRoutine;
            yield return taskRoutine;
            yield return taskRoutine;
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
            yield return new WaitForSecondsEnumerator((float)result.counter / 200000.0f);

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


        [Test]
        public void TestComplexCoroutine()
        {
            _taskRunner.RunOnSchedule(new SyncRunner(),
                ComplexEnumerator((i) => Assert.That(i == 100, Is.True)));
        }

        [Test]
        public void TestMultithreadWithPooledTasks()
        {
            using (var runner = new MultiThreadRunner("TestMultithread"))
            {
                _iterable1.Reset();

                _iterable1.RunOnSchedule(runner);

                while (_iterable1.AllRight == false);

                _iterable1.Reset();

                _iterable1.RunOnSchedule(runner);

                while (_iterable1.AllRight == false);

                Assert.Pass();
            }
        }
        
        [Test]
        public void TestMultithreadWithTaskRoutines()
        {
            using (var runner = new MultiThreadRunner("TestMultithread"))
            {
                _iterable1.Reset();

                _reusableTaskRoutine.SetEnumerator(_iterable1);
                
                var continuator = _reusableTaskRoutine.SetScheduler(runner).Start();
                
                while (continuator.MoveNext());

                Assert.That(_iterable1.AllRight == true);

                _iterable1.Reset();
                
                _reusableTaskRoutine.SetEnumerator(_iterable1);
                
                continuator = _reusableTaskRoutine.Start();

                while (continuator.MoveNext());

                Assert.That(_iterable1.AllRight == true);
            }
        }

        [Test]
        public void TestMultithreadQuick()
        {
            using (var runner = new MultiThreadRunner("TestMultithread"))
            {
                _iterable1.Reset();

                _iterable1.RunOnSchedule(runner);

                while (_iterable1.AllRight == false);

                _iterable1.Reset();

                _iterable1.RunOnSchedule(runner);

                while (_iterable1.AllRight == false);

                Assert.Pass();
            }
        }

        [Test]
        public void TestMultithreadIntervaled()
        {
            using (var runner = new MultiThreadRunner("TestMultithreadIntervaled", 1))
            {
                DateTime now = DateTime.Now;

                _iterable1.RunOnSchedule(runner);

                while (_iterable1.AllRight == false);

                var seconds = (DateTime.Now - now).Seconds;

                //10000 iteration * 1ms = 10 seconds

                Assert.That(seconds == 10);
            }
        }
*/
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
        ITaskRoutine<IEnumerator> _reusableTaskRoutine;

        SerialTaskCollection<TestEnumerator> _serialTasks1;
        SerialTaskCollection _serialTasks2;
        ParallelTaskCollection<IEnumerator> _parallelTasks1;
        ParallelTaskCollection _parallelTasks2;

        Task _task1;
        Task _task2;

        TestEnumerator _iterable1;
        TestEnumerator _iterable2;

        TestEnumerator _iterableWithException;
        AsyncTask _asyncTaskChain1;
        AsyncTask _asyncTaskChain2;
        ValueObject _vo;
      

            class Task : IAsyncTask
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

            public void	OnComplete(Action action)
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

        class AsyncTask: IAsyncTask
        {
            public bool  isDone { get; private set; }
            
            public AsyncTask()
            {
                isDone = false;
            }

            public void Execute()
            {
                Interlocked.Increment(ref _value.counter);

                isDone = true;
            }

            ValueObject _value;
        }

        class ValueObject
        {
            public int counter;
        }

        struct TestEnumerator : IEnumerator
        {
            public long endOfExecutionTime {get; private set;}

            public bool AllRight { get 
            {
                return iterations == totalIterations; 
            }}

            public TestEnumerator(int niterations):this()
            {
                iterations      = 0; 
                totalIterations = niterations;
            }

            public bool MoveNext()
            {
                if (iterations == 0)
                    startOfExecutionTime = Time.frameCount;

                if (iterations < totalIterations)
                {
                    iterations++;

                    return true;
                }
                
                if (totalIterations <= 0)
                    throw new Exception("can't handle this");

                endOfExecutionTime = Time.frameCount;
                
                return false;
            }

            public void Reset()
            {
                Debug.Log("reset");
                iterations = 0;
            }

            public object Current { get; private set; }

            readonly int  totalIterations;
            public   int  iterations;
            public   long startOfExecutionTime;
        }
    }
}
#endif