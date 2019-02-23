using System;
using System.Collections;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Unity;
using Svelto.Tasks.Unity.Internal;
using UnityEngine.TestTools;

namespace Test
{
    /// <summary>
    /// It is possible to run tasks in serial and in parallel without the collections already, but the collections
    /// allows simpler pattern, like setting an onComplete and tracking the execution of the collection outside
    /// from the task itself. Their use is not common, but can be handy some times, especially when combination
    /// of serial and paralletasks can form more complex behaviours. Under the point of view of the runner,
    /// A collection is a unique task, which is another fundamental difference. 
    /// </summary>
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

            _iterable1             = new Enumerator(10000);
            _iterable2             = new Enumerator(5000);
            
            _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(new SyncRunner());
        }
        
        [UnityTest]
        public IEnumerator TestParallelBreakIt()
        {
            yield return null;

            _parallelTasks1.Add(_iterable1);
            _parallelTasks1.Add(BreakIt());
            _parallelTasks1.Add(_iterable2);

            _parallelTasks1.RunOnScheduler(new SyncRunner());

            Assert.That(_iterable1.AllRight == false && _iterable1.iterations == 1); 
            Assert.That(_iterable2.AllRight == false && _iterable2.iterations == 0);
        }
        
        [UnityTest]
        public IEnumerator TestTasksAreExecutedInSerialAndOnCompleteIsCalled()
        {
            yield return null;

            bool isDone = false;
            _serialTasks1.onComplete += () => isDone = true;

            _serialTasks1.Add(_iterable1);
            _serialTasks1.Add(_iterable2);

            _serialTasks1.RunOnScheduler(new SyncRunner());

            Assert.IsTrue(_iterable1.AllRight);
            Assert.IsTrue(_iterable2.AllRight);
            Assert.IsTrue(isDone);
            Assert.LessOrEqual(_iterable1.endOfExecutionTime, _iterable2.endOfExecutionTime);
        }
        
        [UnityTest]
        public IEnumerator TestTasksAreExecutedInParallelAndOnCompleteIsCalled()
        {
            yield return null;

            bool onCompleteIsCalled = false;

            Enumerator _task1 = new Enumerator(2);
            Enumerator _task2 = new Enumerator(3);
            Enumerator _task3 = new Enumerator(4);

            _parallelTasks1.onComplete += () =>
                                          {
                                              onCompleteIsCalled = true;
                                          };

            _parallelTasks1.Add(_task1);
            _parallelTasks1.Add(_task2);
            _parallelTasks1.Add(_task3);

            int count = 0;

            while (_parallelTasks1.MoveNext() == true)
            {
                yield return null;
                
                count++;
                if (count <= 2)
                    Assert.AreEqual(_task1.iterations, count);
                
                if (count <= 3)
                    Assert.AreEqual(_task2.iterations, count);
                
                Assert.AreEqual(_task3.iterations, count);
            }

            Assert.True(_task1.AllRight);
            Assert.True(_task2.AllRight);
            Assert.True(onCompleteIsCalled);
        }

        [UnityTest]
        public IEnumerator TestSerialBreakIt()
        {
            yield return null;

            _serialTasks1.Add(_iterable1);
            _serialTasks1.Add(BreakIt());
            _serialTasks1.Add(_iterable2);

            _serialTasks1.RunOnScheduler(new SyncRunner());

            Assert.That(_iterable1.AllRight == true && _iterable2.AllRight == false);
        }
        
        [UnityTest]
        public IEnumerator TestEnumerablesAreExecutedInSerialWithReusableTask()
        {
            yield return null;

            _reusableTaskRoutine.SetEnumerator(TestSerialTwice());
                                _reusableTaskRoutine.Start();
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

            Enumerator _task1 = new Enumerator(2);
            Enumerator _task2 = new Enumerator(3);

            _serialTasks1.onComplete += () => Assert.That(_task1.AllRight && _task2.AllRight, Is.True);

            _serialTasks1.Add(_task1);
            _serialTasks1.Add(_task2);

            _serialTasks1.RunOnScheduler(new SyncRunner());
        }

        
        [UnityTest]
        public IEnumerator TestParallelTasksAreExecutedInSerial()
        {
            yield return null;

            bool parallelTasks1Done = false;
            bool parallelTasks2Done = false;

            _parallelTasks1.Add(_iterable1);
            _parallelTasks1.Add(_iterable2);
            _parallelTasks1.onComplete += () =>
                                          {
                                              Assert.That(parallelTasks2Done, Is.False);
                                              parallelTasks1Done = true;
                                          };

            _parallelTasks2.Add(_iterable1);
            _parallelTasks2.Add(_iterable2);
            _parallelTasks2.onComplete += () =>
                                          {
                                              Assert.That(parallelTasks1Done, Is.True);
                                              parallelTasks2Done = true;
                                          };

            _serialTasks1.Add(_parallelTasks1);
            _serialTasks1.Add(_parallelTasks2);
            _serialTasks1.onComplete += () => { Assert.That(parallelTasks1Done && parallelTasks2Done); };

            _serialTasks1.RunOnScheduler(new SyncRunner());
        }
        
        [UnityTest]
        public IEnumerator TestSerialTasksAreExecutedInParallel()
        {
            yield return null;

            int test1 = 0;
            int test2 = 0;

            _serialTasks1.Add(_iterable1);
            _serialTasks1.Add(_iterable2);
            _serialTasks1.onComplete += () =>
                                        {
                                            test1++;
                                            test2++;
                                        };

            _serialTasks2.Add(_iterable1);
            _serialTasks2.Add(_iterable2);
            _serialTasks2.onComplete += () => { test2++; };

            _parallelTasks1.Add(_serialTasks1);
            _parallelTasks1.Add(_serialTasks2);
            _parallelTasks1.onComplete += () => Assert.That((test1 == 1) && (test2 == 2), Is.True);

            _parallelTasks1.RunOnScheduler(new SyncRunner());
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
            _parallelTasks1.RunOnScheduler(new SyncRunner(2000));

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
            _parallelTasks1.RunOnScheduler(new SyncRunner(4000));

            var totalSeconds = (DateTime.Now - then).TotalSeconds;
            Assert.That(totalSeconds, Is.InRange(1.0, 1.1));
        }

        [UnityTest]
        public IEnumerator TestParallelWithMixedYield()
        {
            yield return null;

            using (var runner = new UpdateMonoRunner("test2"))
            {
                var enumerator = new Enumerator(1);
                _parallelTasks1.Add(enumerator);
                var enumerator1 = new Enumerator(2);
                _parallelTasks1.Add(enumerator1);
                var enumerator2 = new Enumerator(3);
                _parallelTasks1.Add(enumerator2);
                var enumerator3 = new Enumerator(4);
                _parallelTasks1.Add(enumerator3);
                var enumerator4 = new Enumerator(5);
                _parallelTasks1.Add(enumerator4);
                var enumerator5 = new Enumerator(6);
                _parallelTasks1.Add(enumerator5);

                _parallelTasks1.RunOnScheduler(runner);

                Assert.IsFalse(enumerator.AllRight);
                Assert.IsFalse(enumerator1.AllRight);
                Assert.IsFalse(enumerator2.AllRight);
                Assert.IsFalse(enumerator3.AllRight);
                Assert.IsFalse(enumerator4.AllRight);
                Assert.IsFalse(enumerator5.AllRight);
                runner.Step();
                Assert.IsTrue(enumerator.AllRight);
                Assert.IsFalse(enumerator1.AllRight);
                Assert.IsFalse(enumerator2.AllRight);
                Assert.IsFalse(enumerator3.AllRight);
                Assert.IsFalse(enumerator4.AllRight);
                Assert.IsFalse(enumerator5.AllRight);
                runner.Step();
                Assert.IsTrue(enumerator.AllRight);
                Assert.IsTrue(enumerator1.AllRight);
                Assert.IsFalse(enumerator2.AllRight);
                Assert.IsFalse(enumerator3.AllRight);
                Assert.IsFalse(enumerator4.AllRight);
                Assert.IsFalse(enumerator5.AllRight);
                runner.Step();
                Assert.IsTrue(enumerator.AllRight);
                Assert.IsTrue(enumerator1.AllRight);
                Assert.IsTrue(enumerator2.AllRight);
                Assert.IsFalse(enumerator3.AllRight);
                Assert.IsFalse(enumerator4.AllRight);
                Assert.IsFalse(enumerator5.AllRight);
                runner.Step();
                Assert.IsTrue(enumerator.AllRight);
                Assert.IsTrue(enumerator1.AllRight);
                Assert.IsTrue(enumerator2.AllRight);
                Assert.IsTrue(enumerator3.AllRight);
                Assert.IsFalse(enumerator4.AllRight);
                Assert.IsFalse(enumerator5.AllRight);
                runner.Step();
                Assert.IsTrue(enumerator.AllRight);
                Assert.IsTrue(enumerator1.AllRight);
                Assert.IsTrue(enumerator2.AllRight);
                Assert.IsTrue(enumerator3.AllRight);
                Assert.IsTrue(enumerator4.AllRight);
                Assert.IsFalse(enumerator5.AllRight);
                runner.Step();
                Assert.IsTrue(enumerator.AllRight);
                Assert.IsTrue(enumerator1.AllRight);
                Assert.IsTrue(enumerator2.AllRight);
                Assert.IsTrue(enumerator3.AllRight);
                Assert.IsTrue(enumerator4.AllRight);
                Assert.IsTrue(enumerator5.AllRight);
            }
        }
        
        IEnumerator SerialContinuation()
        {
            _parallelTasks1.Add(_iterable2);
            _parallelTasks1.Add(_iterable1);

            yield return _parallelTasks1;

            Assert.That(_parallelTasks1.isRunning, Is.False);
        }
        
        IEnumerator TestSerialTwice()
        {
            _serialTasks1.Add(_iterable1);
            _serialTasks1.Add(_iterable2);

            yield return _serialTasks1;

            Assert.That(_iterable1.AllRight && _iterable2.AllRight && (_iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime),
                        Is.True);

            _iterable1.Reset();
            _iterable2.Reset();
            _serialTasks1.Add(_iterable1);
            _serialTasks1.Add(_iterable2);

            yield return _serialTasks1;

            Assert.That(_iterable1.AllRight && _iterable2.AllRight && (_iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime),
                        Is.True);
        }
        
        IEnumerator BreakIt()
        {
            yield return Break.AndStop;
        }

        IEnumerator Count()
        {
            yield return InnerCount(); //this won't yield a frame, only yield return null yields to the next iteration
        }

        IEnumerator InnerCount()
        {
            yield return null;
        }

        SerialTaskCollection   _serialTasks1;
        SerialTaskCollection   _serialTasks2;
        ParallelTaskCollection _parallelTasks1;
        ParallelTaskCollection _parallelTasks2;

        Enumerator _iterable1;
        Enumerator _iterable2;
        ITaskRoutine<IEnumerator> _reusableTaskRoutine;
    }
}