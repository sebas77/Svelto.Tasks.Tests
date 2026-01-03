using Svelto.Tasks;
using Svelto.Tasks.Lean;
using Svelto.Tasks.Parallelism;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsWithMultiThreadRunners
    {
        [SetUp]
        public void Setup()
        {
            _iterable1 = new LeanEnumerator(10000);
        }
        
        [Test]
        public void TestMultithreadQuick()
        {
            using (var runner = new Svelto.Tasks.Lean.MultiThreadRunner("TestMultithreadQuick", false))
            {
                var task = Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(_iterable1, runner);

                while ((task).isRunning) ;

                Assert.That(_iterable1.AllRight == true);

                //do it again to test if starting another task works

                _iterable1.Reset();

                task = Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(_iterable1, runner);

                while ((task).isRunning) ;

                Assert.That(_iterable1.AllRight == true);
            }
        }

        [Test]
        public void TestMultithreadIntervaled()
        {
            using (var runner = new Svelto.Tasks.Lean.MultiThreadRunner("TestMultithreadIntervaled", 1u))
            {
                var iterable1 = new LeanEnumerator(2000);
                
                DateTime now = DateTime.Now;
                Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(iterable1, runner).Complete();
                var seconds = (DateTime.Now - now).TotalSeconds;

                Assert.That((int) seconds, Is.EqualTo(2));
                Assert.That(iterable1.AllRight, Is.True);
            }
        }
        
        [Test]
        public void TestMultiThreadParallelTaskCompletes()
        {
            var test = new MultiThreadedParallelTaskCollection<WaitEnumerator>("test", 4, false);

            bool done = false;
            test.onComplete += () => done = true;
            Token token = new Token();

            test.Add(new WaitEnumerator(token));
            test.Add(new WaitEnumerator(token));
            test.Add(new WaitEnumerator(token));
            test.Add(new WaitEnumerator(token));

            var multiThreadRunner = new Svelto.Tasks.Lean.MultiThreadRunner("test", true);
            Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(test, multiThreadRunner);
            DateTime now = DateTime.Now;

            while (test.isRunning)
                test.Complete();

            var totalSeconds = (DateTime.Now - now).TotalSeconds;

            Assert.That(totalSeconds, Is.GreaterThan(1.9));
            Assert.That(totalSeconds, Is.LessThan(2.1));
            Assert.That(done, Is.True);
            Assert.That(token.count, Is.EqualTo(4));
        
            test.Dispose();
            multiThreadRunner.Dispose();
        }
        
        [Test]
        public void TestCrazyMultiThread()
        {
            ValueObject result = new ValueObject();

            var runner = new Svelto.Tasks.Lean.MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider");
            {
                int i = 0;
                while (i++ < 20)
                {
                    var continuator = Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(crazyEnumerator(result, runner), runner);

                    while (continuator.isRunning == true)
                        continuator.Complete();
                }
            }
            runner.Dispose();

            Assert.That(result.counter, Is.EqualTo(100));
        }
        
        [Test]
        public void ParallelMultiThread()
        {
            var parallelMultiThread = new MultiThreadedParallelTaskCollection<SlowTask>("test", 2, true);

            parallelMultiThread.Add(new SlowTask());
            parallelMultiThread.Add(new SlowTask());

            var sw = System.Diagnostics.Stopwatch.StartNew();

            parallelMultiThread.Complete();
            parallelMultiThread.Dispose();

            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.AtLeast(900));
            Assert.That(sw.ElapsedMilliseconds, Is.AtMost(1100));
        }

        [Test]
        public void MultiThreadedParallelTaskCollectionRunningOnAnotherThread()
        {
            var runner = new Svelto.Tasks.Lean.MultiThreadRunner("MT");
            {
                var iterable = MultiThreadedParallelTaskCollectionAsyncTask();
                var continuator = Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(iterable, runner);

                while ((continuator).isRunning == true) continuator.Complete();
            }
            runner.Dispose();
        }
        
        [Test]
        [Timeout(1000)]
        public void TestNaiveEnumeratorsOnMultithreadedRunners()
        {
            var runner = new Svelto.Tasks.Lean.MultiThreadRunner("TestMultithread");
            
                _iterable1.Reset();

                var continuator = Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(_iterable1, runner);

                while ((continuator).isRunning) continuator.Complete();

                Assert.That(_iterable1.AllRight == true);
            
                runner.Dispose();
        }

        [Test]
        public void YieldMultiThreadedParallelTaskCollection()
        {
            MultiThreadedParallelTaskCollectionAsyncTask().Complete();
        }

        IEnumerator<TaskContract> MultiThreadedParallelTaskCollectionAsyncTask()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var parallelMultiThread = new MultiThreadedParallelTaskCollection<SlowTask>("test", 2, false);

            parallelMultiThread.Add(new SlowTask());
            parallelMultiThread.Add(new SlowTask());

            yield return parallelMultiThread.Continue();
            
            parallelMultiThread.Dispose();

            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.AtLeast(900));
            Assert.That(sw.ElapsedMilliseconds, Is.AtMost(1100));
        }

        IEnumerator<TaskContract> crazyEnumerator(ValueObject result, IRunner<LeanSveltoTask<IEnumerator<TaskContract>>> runner)
        {
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
        }
        
        IEnumerator<TaskContract> SimpleEnumeratorFast(ValueObject result)
        {
            yield return TaskContract.Yield.It;

            Interlocked.Increment(ref result.counter);
        }
        
        [TearDown]
        public void TearDown()
        {
            _iterable1.Dispose();
        }

        LeanEnumerator _iterable1;
    }

    [TestFixture]
    public class TaskRunnerTestsMultiThreadRunner
    {
        [Test]
        public void MultiThreadRunner_CompletesTasks_WithoutDeadlock()
        {
            // What we are testing:
            // MultiThreadRunner runs Lean tasks on a background thread and completes them.

            using (var runner = new Svelto.Tasks.Lean.MultiThreadRunner("MT_Completes"))
            {
                var counter = 0;

                IEnumerator<TaskContract> Work(int iterations)
                {
                    var i = 0;
                    while (i++ < iterations)
                    {
                        counter++;
                        yield return TaskContract.Yield.It;
                    }
                }

                Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(Work(1024), runner);

                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (runner.hasTasks && DateTime.UtcNow < deadline)
                {
                }

                Assert.That(counter, Is.EqualTo(1024));
            }
        }

        [Test]
        public void MultiThreadRunner_PausePreventsProgress_UntilResume()
        {
            // What we are testing:
            // Paused runner should not progress tasks; after Resume it should complete.

            using (var runner = new Svelto.Tasks.Lean.MultiThreadRunner("MT_PauseResume"))
            {
                var counter = 0;

                IEnumerator<TaskContract> Work()
                {
                    var i = 0;
                    while (i++ < 256)
                    {
                        counter++;
                        yield return TaskContract.Yield.It;
                    }
                }

                Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(Work(), runner);

                runner.Pause();

                var snapshot = counter;
                var then = DateTime.UtcNow.AddMilliseconds(100);
                while (DateTime.UtcNow < then)
                {
                }

                Assert.That(counter, Is.EqualTo(snapshot));

                runner.Resume();

                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (runner.hasTasks && DateTime.UtcNow < deadline)
                {
                }

                Assert.That(counter, Is.EqualTo(256));
            }
        }

        [Test]
        public void MultiThreadRunner_StopFlushesRunningTasks()
        {
            // What we are testing:
            // Stop should trigger flushing behavior and allow the runner to return to idle.

            using (var runner = new Svelto.Tasks.Lean.MultiThreadRunner("MT_Stop"))
            {
                IEnumerator<TaskContract> Infinite()
                {
                    var i = 0;
                    while (i++ < 1000000)
                        yield return TaskContract.Yield.It;

                    yield break;
                }

                Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(Infinite(), runner);

                runner.Stop();

                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (runner.hasTasks && DateTime.UtcNow < deadline)
                {
                }

                Assert.That(runner.hasTasks, Is.False);
            }
        }
    }
}

