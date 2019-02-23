#if later
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
            yield return Yield.It;

            using (var runner = new MultiThreadRunner("TestMultithreadQuick", false))
            {
                var task = _iterable1.RunOn(runner);

                while ((task).isRunning) ;

                Assert.That(_iterable1.AllRight == true);

                //do it again to test if starting another task works

                _iterable1.Reset();

                task = _iterable1.RunOn(runner);

                while ((task).isRunning) ;

                Assert.That(_iterable1.AllRight == true);
            }
        }

        [UnityTest]
        public IEnumerator TestMultithreadIntervaled()
        {
            yield return Yield.It;

            using (var runner = new MultiThreadRunner("TestMultithreadIntervaled", 1))
            {
                var iterable1 = new Enumerator(2000);
                var taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
                taskRoutine.SetEnumerator(iterable1);

                DateTime now = DateTime.Now;
                (taskRoutine.Start()).Complete();
                var seconds = (DateTime.Now - now).TotalSeconds;

                //2000 iteration * 1ms = 2 seconds
                Assert.That((int) seconds, Is.EqualTo(2));
                Assert.IsTrue(iterable1.AllRight);
            }
        }
        
        [UnityTest]
        public IEnumerator TestMultiThreadParallelTaskCompletes()
        {
            yield return Yield.It;

            var test = new MultiThreadedParallelTaskCollection("test", 4, false);
            
                bool done = false;
                test.onComplete += () => done = true;
                Token token = new Token();

                test.Add(new WaitEnumerator(token));
                test.Add(new WaitEnumerator(token));
                test.Add(new WaitEnumerator(token));
                test.Add(new WaitEnumerator(token));

                var multiThreadRunner = new MultiThreadRunner("test", true);
                test.RunOn(multiThreadRunner);
                DateTime now = DateTime.Now;
                yield return Yield.It;

                while (test.isRunning)
                    yield return Yield.It;

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

                    while (continuationWrapper.isRunning == true)
                        yield return Yield.It;
                }
            }
            runner.Dispose();

            Assert.That(result.counter, Is.EqualTo(100));
        }
        
        [UnityTest]
        public IEnumerator ParallelMultiThread()
        {
            yield return Yield.It;

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
            yield return Yield.It;

            var runner = new MultiThreadRunner("MT");
            {
                var routine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);

                routine.SetEnumerator(YieldMultiThreadedParallelTaskCollection());

                var continuator = routine.Start();

                while ((continuator).isRunning == true) yield return Yield.It;
            }
            runner.Dispose();
        }
        
        [UnityTest]
        [Timeout(1000)]
        public IEnumerator<TaskContract> TestNaiveEnumeratorsOnMultithreadedRunners()
        {
            yield return Yield.It;

            var runner = new MultiThreadRunner("TestMultithread");
            
                _iterable1.Reset();

                var continuator = _iterable1.RunOn(runner);

                while ((continuator).isRunning) yield return Yield.It;

                Assert.That(_iterable1.AllRight == true);
            
                runner.Dispose();
                
        }

        [UnityTest]
        public IEnumerator YieldMultiThreadedParallelTaskCollection()
        {
            yield return Yield.It;

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

         IEnumerator<TaskContract> crazyEnumerator(ValueObject result, IRunner<IEnumerator> runner)
        {
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
        }
        
         IEnumerator<TaskContract> SimpleEnumeratorFast(ValueObject result)
        {
            yield return Yield.It;

            Interlocked.Increment(ref result.counter);
        }
        
        Enumerator _iterable1;
    }
}
#endif