using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Parallelism;
using UnityEngine.TestTools;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsWithMultiThreadRunners
    {
        [SetUp]
        public void Setup()
        {
            _iterable1 = new Enumerator(10000);
        }
        
        [UnityTest]
        public IEnumerator TestMultithreadQuick()
        {
            yield return null;

            using (var runner = new MultiThreadRunner("TestMultithreadQuick", false))
            {
                var task = _iterable1.RunOnScheduler(runner);

                while (task.MoveNext()) ;

                Assert.That(_iterable1.AllRight == true);

                //do it again to test if starting another task works

                _iterable1.Reset();

                task = _iterable1.RunOnScheduler(runner);

                while (task.MoveNext()) ;

                Assert.That(_iterable1.AllRight == true);
            }
        }

        [UnityTest]
        public IEnumerator TestMultithreadIntervaled()
        {
            yield return null;

            using (var runner = new MultiThreadRunner("TestMultithreadIntervaled", 1))
            {
                var iterable1 = new Enumerator(2000);
                var taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
                taskRoutine.SetEnumerator(iterable1);

                DateTime now = DateTime.Now;
                taskRoutine.Start().Complete();
                var seconds = (DateTime.Now - now).TotalSeconds;

                //2000 iteration * 1ms = 2 seconds
                Assert.That((int) seconds, Is.EqualTo(2));
                Assert.IsTrue(iterable1.AllRight);
            }
        }
        
        [UnityTest]
        public IEnumerator TestMultiThreadParallelTaskCompletes()
        {
            yield return null;

            using (var test = new MultiThreadedParallelTaskCollection())
            {
                bool done = false;
                test.onComplete += () => done = true;
                Token token = new Token();

                test.Add(new WaitEnumerator(token));
                test.Add(new WaitEnumerator(token));
                test.Add(new WaitEnumerator(token));
                test.Add(new WaitEnumerator(token));

                test.RunOnScheduler(new MultiThreadRunner("test", true));
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
                var routine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);

                routine.SetEnumerator(YieldMultiThreadedParallelTaskCollection());

                var continuator = routine.Start();

                while (continuator.MoveNext() == true) yield return null;
            }
        }
        
        [UnityTest]
        [Timeout(1000)]
        public IEnumerator TestNaiveEnumeratorsOnMultithreadedRunners()
        {
            yield return null;

            using (var runner = new MultiThreadRunner("TestMultithread"))
            {
                _iterable1.Reset();

                var continuator = _iterable1.RunOnScheduler(runner);

                while (continuator.MoveNext()) yield return null;

                Assert.That(_iterable1.AllRight == true);
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

        IEnumerator crazyEnumerator(ValueObject result, IRunner<IEnumerator> runner)
        {
            yield return SimpleEnumeratorFast(result).RunOnScheduler(runner);
            yield return SimpleEnumeratorFast(result).RunOnScheduler(runner);
            yield return SimpleEnumeratorFast(result).RunOnScheduler(runner);
            yield return SimpleEnumeratorFast(result).RunOnScheduler(runner);
            yield return SimpleEnumeratorFast(result).RunOnScheduler(runner);
        }
        
        IEnumerator SimpleEnumeratorFast(ValueObject result)
        {
            yield return null;

            Interlocked.Increment(ref result.counter);
        }
        
        Enumerator _iterable1;
    }
}