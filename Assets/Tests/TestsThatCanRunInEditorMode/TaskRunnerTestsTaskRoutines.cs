using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using UnityEngine.TestTools;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsTaskRoutines
    {
        [SetUp]
        public void Setup()
        {
            _iterable1 = new Enumerable(10000);
            _iterable3 = new Enumerable(2000);
            
            _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine()
                                             .SetScheduler(new SyncRunner()); //the taskroutine will stall the thread because it runs on the SyncScheduler
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

                while (continuator.MoveNext()) ;

                Assert.That(_iterable1.AllRight == true);

                _iterable1.Reset();

                _reusableTaskRoutine.SetEnumerator(_iterable1.GetEnumerator());

                continuator = _reusableTaskRoutine.Start();

                while (continuator.MoveNext()) ;

                Assert.That(_iterable1.AllRight == true);
            }
        }
        
        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineStartStart()
        {
            yield return null;

            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStartStart"))
            {
                ValueObject result = new ValueObject();

                var taskRoutine = _reusableTaskRoutine
                                 .SetScheduler(runner).SetEnumeratorProvider(() => SimpleEnumerator(result));
                taskRoutine.Start();
                yield return null;
                var continuator = taskRoutine.Start();

                while (continuator.MoveNext()) yield return null;

                Assert.That(result.counter, Is.EqualTo(1));
            }
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
        public IEnumerator TestSimpleTaskRoutineStopStartWithProvider()
        {
            yield return null;

            ValueObject result = new ValueObject();

            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider"))
            {
                var continuator = _reusableTaskRoutine.SetScheduler(runner)
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
        public IEnumerator TestSimpleTaskRoutineStopStart()
        {
            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStart"))
            {
                ValueObject result = new ValueObject();

                var taskRoutine = _reusableTaskRoutine.SetScheduler(runner).SetEnumerator(SimpleEnumerator(result));

                taskRoutine.Start();
                taskRoutine.Stop();
                taskRoutine.SetEnumerator(SimpleEnumerator(result));

                var continuator = taskRoutine.Start();

                while (continuator.MoveNext()) yield return null;

                Assert.That(result.counter == 1);
            }
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

                Assert.That((int) enumerator.Current, Is.EqualTo(1));
            }
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
        
        IEnumerator SimpleEnumerator(ValueObject result)
        {
            yield return new WaitForSecondsEnumerator(1);

            Interlocked.Increment(ref result.counter);
        }
        
        IEnumerator SimpleEnumeratorLong(ValueObject result)
        {
            yield return new WaitForSecondsEnumerator(10);
        }

        ITaskRoutine _reusableTaskRoutine;

        Enumerable _iterable1;
        Enumerable _iterable3;
    }
}