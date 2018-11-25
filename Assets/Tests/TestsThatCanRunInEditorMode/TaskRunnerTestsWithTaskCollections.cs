using System;
using System.Collections;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using UnityEngine.TestTools;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsWithTaskCollections
    {
        [SetUp]
        public void Setup ()
        {
            _serialTasks1   = new SerialTaskCollection();
            _serialTasks2 = new SerialTaskCollection();
            _parallelTasks1 = new ParallelTaskCollection();
            _parallelTasks2 = new ParallelTaskCollection();

            _iterable1             = new Enumerable(10000);
            _iterable2             = new Enumerable(10000);
            _iterable3             = new Enumerable(2000);
            _iterableWithException = new Enumerable(-5);
            
            _taskRunner = TaskRunner.Instance;
            _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine()
                                             .SetScheduler(new SyncRunner()); //the taskroutine will stall the thread because it runs on the SyncScheduler
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
        public IEnumerator TestEnumerablesAreExecutedInSerialAndOnCompleteIsCalled()
        {
            yield return null;

            bool isDone = false;
            _serialTasks1.onComplete += () => isDone = true;

            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterable2.GetEnumerator());

            _taskRunner.RunOnScheduler(new SyncRunner(), _serialTasks1);

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
        
        [UnityTest]
        public IEnumerator TestEnumerableAreExecutedInParallel()
        {
            yield return null;

            _parallelTasks1.onComplete += () => { Assert.That(_iterable1.AllRight && _iterable2.AllRight); };

            _parallelTasks1.Add(_iterable1.GetEnumerator());
            _parallelTasks1.Add(_iterable2.GetEnumerator());

            _taskRunner.RunOnScheduler(new SyncRunner(), _parallelTasks1);
        }
        
        [UnityTest]
        public IEnumerator TestEnumerablesAreExecutedInSerialWithReusableTask()
        {
            yield return null;

            _reusableTaskRoutine.SetEnumerator(TestSerialTwice()).Start();
        }
        
        [UnityTest]
        public IEnumerator TestParallelTasks1IsExecutedBeforeParallelTask2()
        {
            yield return null;

            TaskRunner.Instance.RunOnScheduler(new SyncRunner(), SerialContinuation());
        }
        
        [UnityTest]
        public IEnumerator TestSerializedTasksAreExecutedInSerial()
        {
            yield return null;

            Enumerable _task1 = new Enumerable(2);
            Enumerable _task2 = new Enumerable(3);

            _serialTasks1.onComplete += () => Assert.That(_task1.AllRight && _task2.AllRight, Is.True);

            _serialTasks1.Add(_task1.GetEnumerator());
            _serialTasks1.Add(_task2.GetEnumerator());

            _taskRunner.RunOnScheduler(new SyncRunner(), _serialTasks1);
        }

        [UnityTest]
        public IEnumerator TestTasksAreExecutedInParallel()
        {
            yield return null;

            Enumerable _task1 = new Enumerable(2);
            Enumerable _task2 = new Enumerable(3);

            _parallelTasks1.onComplete += () => Assert.That(_task1.AllRight && _task2.AllRight, Is.True);

            _parallelTasks1.Add(_task1.GetEnumerator());
            _parallelTasks1.Add(_task2.GetEnumerator());

            _taskRunner.RunOnScheduler(new SyncRunner(), _parallelTasks1);
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

            _parallelTasks1.Add(_iterable2.GetEnumerator());
            _parallelTasks1.Add(_iterable1.GetEnumerator());

            yield return _parallelTasks1;

            Assert.That(_parallelTasks1.isRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator TestParallelTasksAreExecutedInSerial()
        {
            yield return null;

            bool parallelTasks1Done = false;
            bool parallelTasks2Done = false;

            _parallelTasks1.Add(_iterable1.GetEnumerator());
            _parallelTasks1.Add(_iterable2.GetEnumerator());
            _parallelTasks1.onComplete += () =>
                                          {
                                              Assert.That(parallelTasks2Done, Is.False);
                                              parallelTasks1Done = true;
                                          };

            _parallelTasks2.Add(_iterable1.GetEnumerator());
            _parallelTasks2.Add(_iterable2.GetEnumerator());
            _parallelTasks2.onComplete += () =>
                                          {
                                              Assert.That(parallelTasks1Done, Is.True);
                                              parallelTasks2Done = true;
                                          };

            _serialTasks1.Add(_parallelTasks1);
            _serialTasks1.Add(_parallelTasks2);
            _serialTasks1.onComplete += () => { Assert.That(parallelTasks1Done && parallelTasks2Done); };

            _taskRunner.RunOnScheduler(new SyncRunner(), _serialTasks1);
        }
        
        [UnityTest]
        public IEnumerator TestSerialTasksAreExecutedInParallel()
        {
            yield return null;

            int test1 = 0;
            int test2 = 0;

            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterable2.GetEnumerator());
            _serialTasks1.onComplete += () =>
                                        {
                                            test1++;
                                            test2++;
                                        };

            _serialTasks2.Add(_iterable1.GetEnumerator());
            _serialTasks2.Add(_iterable2.GetEnumerator());
            _serialTasks2.onComplete += () => { test2++; };

            _parallelTasks1.Add(_serialTasks1);
            _parallelTasks1.Add(_serialTasks2);
            _parallelTasks1.onComplete += () => Assert.That((test1 == 1) && (test2 == 2), Is.True);

            _taskRunner.RunOnScheduler(new SyncRunner(), _parallelTasks1);
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
        public IEnumerator TestPromisesExceptionHandler()
        {
            yield return null;

            bool allDone = false;

            //this must never happen
            _serialTasks1.onComplete += () =>
                                        {
                                            allDone = true;
                                            Assert.That(false);
                                        };

            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterableWithException.GetEnumerator()); //will throw an exception

            bool hasBeenCalled = false;
            try
            {
                _reusableTaskRoutine.SetEnumerator(_serialTasks1).SetScheduler(new SyncRunner()).Start
                    (e =>
                     {
                         Assert.That(allDone, Is.False);
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

            bool allDone  = false;
            bool testDone = false;

            _serialTasks1.onComplete += () =>
                                        {
                                            allDone = true;
                                            Assert.That(false);
                                        };

            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterable2.GetEnumerator());

            //this time we will make the task run on another thread
            _reusableTaskRoutine.SetScheduler(new MultiThreadRunner("TestPromisesCancellation"))
                                .SetEnumerator(_serialTasks1).Start
                                     (null, () =>
                                            {
                                                testDone = true;
                                                Assert.That(allDone, Is.False);
                                            });
            _reusableTaskRoutine.Stop();

            while (testDone == false) ;
        }

        
        IEnumerator TestSerialTwice()
        {
            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterable2.GetEnumerator());

            yield return _serialTasks1;

            Assert.That(_iterable1.AllRight && _iterable2.AllRight && (_iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime),
                        Is.True);

            _iterable1.Reset();
            _iterable2.Reset();
            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterable2.GetEnumerator());

            yield return _serialTasks1;

            Assert.That(_iterable1.AllRight && _iterable2.AllRight && (_iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime),
                        Is.True);
        }
        
        IEnumerator BreakIt()
        {
            yield return Break.It;
        }

        
        TaskRunner   _taskRunner;
        ITaskRoutine _reusableTaskRoutine;
 
        SerialTaskCollection                                   _serialTasks1;
        SerialTaskCollection _serialTasks2;


        ParallelTaskCollection                                 _parallelTasks1;
        ParallelTaskCollection _parallelTasks2;

        Enumerable _iterable1;
        Enumerable _iterable2;
        Enumerable _iterableWithException;
        Enumerable _iterable3;
    }
}