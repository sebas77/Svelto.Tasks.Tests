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

                while ((task as IEnumerator).MoveNext()) ;

                Assert.That(_iterable1.AllRight == true);

                //do it again to test if starting another task works

                _iterable1.Reset();

                task = _iterable1.RunOnScheduler(runner);

                while ((task as IEnumerator).MoveNext()) ;

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
                (taskRoutine.Start() as IEnumerator).Complete();
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

            var test = new MultiThreadedParallelTaskCollection("test", 4, false);
            
                bool done = false;
                test.onComplete += () => done = true;
                Token token = new Token();

                test.Add(new WaitEnumerator(token));
                test.Add(new WaitEnumerator(token));
                test.Add(new WaitEnumerator(token));
                test.Add(new WaitEnumerator(token));

                var multiThreadRunner = new MultiThreadRunner("test", true);
                test.RunOnScheduler(multiThreadRunner);
                DateTime now = DateTime.Now;
                yield return null;

                while (test.isRunning)
                    yield return null;

                var totalSeconds = (DateTime.Now - now).TotalSeconds;

                Assert.Greater(totalSeconds, 1.9);
                Assert.Less(totalSeconds, 2.1);
                Assert.That(done, Is.True);
                Assert.AreEqual(4, token.count);
            
                test.Dispose();
                multiThreadRunner.Dispose();
        }
        
        [UnityTest]
        public IEnumerator TestCrazyMultiThread()
        {
            ValueObject result = new ValueObject();

            var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider");
            {
                int i = 0;
                while (i++ < 20)
                {
                    var continuationWrapper = crazyEnumerator(result, runner);

                    while (continuationWrapper.MoveNext() == true)
                        yield return null;
                }
            }
            runner.Dispose();

            Assert.That(result.counter, Is.EqualTo(100));
        }
        
        [UnityTest]
        public IEnumerator ParallelMultiThread()
        {
            yield return null;

            var parallelMultiThread = new MultiThreadedParallelTaskCollection("test", 2, true);

            parallelMultiThread.Add(new SlowTask());
            parallelMultiThread.Add(new SlowTask());

            var sw = System.Diagnostics.Stopwatch.StartNew();

            parallelMultiThread.Complete();
            parallelMultiThread.Dispose();

            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.AtLeast(900));
            Assert.That(sw.ElapsedMilliseconds, Is.AtMost(1100));
        }

        [UnityTest]
        public IEnumerator MultiThreadedParallelTaskCollectionRunningOnAnotherThread()
        {
            yield return null;

            var runner = new MultiThreadRunner("MT");
            {
                var routine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);

                routine.SetEnumerator(YieldMultiThreadedParallelTaskCollection());

                var continuator = routine.Start();

                while ((continuator as IEnumerator).MoveNext() == true) yield return null;
            }
            runner.Dispose();
        }
        
        [UnityTest]
        [Timeout(1000)]
        public IEnumerator TestNaiveEnumeratorsOnMultithreadedRunners()
        {
            yield return null;

            var runner = new MultiThreadRunner("TestMultithread");
            
                _iterable1.Reset();

                var continuator = _iterable1.RunOnScheduler(runner);

                while ((continuator as IEnumerator).MoveNext()) yield return null;

                Assert.That(_iterable1.AllRight == true);
            
                runner.Dispose();
                
        }

        [UnityTest]
        public IEnumerator YieldMultiThreadedParallelTaskCollection()
        {
            yield return null;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            var parallelMultiThread = new MultiThreadedParallelTaskCollection("test", 2, false);

            parallelMultiThread.Add(new SlowTask());
            parallelMultiThread.Add(new SlowTask());

            yield return parallelMultiThread;
            
            parallelMultiThread.Dispose();

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