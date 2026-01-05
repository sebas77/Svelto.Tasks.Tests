using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class RunnerLifecycleTests
    {
        [Test]
        public void SteppableRunner_StopPreventsNewTasksFromStartingUntilSteppedAndUnstopped()
        {
            // What we are testing:
            // Stop() puts the runner in stopping state, blocking new tasks from being accepted immediately.
            // The runner can be stepped to flush tasks and then reused.

            using (var runner = new SteppableRunner("SteppableRunner_Stop"))
            {
                var counter = 0;

                IEnumerator<TaskContract> OneStepIncrement()
                {
                    counter++;
                    yield break;
                }
                
                runner.Stop();

                OneStepIncrement().RunOn(runner);

                Assert.That(runner.hasTasks, Is.True);
                
                runner.Step();

                Assert.That(counter, Is.EqualTo(0));
                
                OneStepIncrement().RunOn(runner);

                // After flushing, runner should allow new tasks.
                runner.Step();
                runner.Step();

                Assert.That(counter, Is.EqualTo(2));
            }
        }

        [Test]
        public void SteppableRunner_FlushClearsTasksAndAllowsReuse()
        {
            // What we are testing:
            // Flush() should stop and reset the runner's internal task state so it can be reused.

            using (var runner = new SteppableRunner("SteppableRunner_Flush"))
            {
                var counter = 0;

                IEnumerator<TaskContract> LongTask()
                {
                    counter++;
                    yield return TaskContract.Yield.It;
                    counter++;
                }

                LongTask().RunOn(runner);
                runner.Step();

                Assert.That(counter, Is.EqualTo(1));
                Assert.That(runner.hasTasks, Is.True);

                runner.Flush();

                Assert.That(runner.hasTasks, Is.False);

                LongTask().RunOn(runner);
                runner.Step();
                runner.Step();

                Assert.That(counter, Is.EqualTo(3));
            }
        }

        [Test]
        public void MultiThreadRunner_StopThenDispose_DoesNotDeadlock()
        {
            // What we are testing:
            // MultiThreadRunner can be stopped and disposed without deadlocking even with running tasks.

            using (var runner = new MultiThreadRunner("MultiThreadRunner_StopDispose"))
            {
                var counter = 0;

                IEnumerator<TaskContract> SpinYield(int iterations)
                {
                    var i = 0;
                    while (i++ < iterations)
                    {
                        counter++;
                        yield return TaskContract.Yield.It;
                    }
                }

                SpinYield(64).RunOn(runner);
                
                // Let it run a bit
                Thread.Sleep(10);
                
                runner.Stop();
                // Dispose is called by using block
            }
        }

        [Test]
        public void SteppableRunner_Stop_AllowsReuseAfterFlush_AndQueuedTasksDuringStopRunAfterward()
        {
            using (var runner = new SteppableRunner("SteppableRunner_StopReuse"))
            {
                var firstCounter  = 0;
                var secondCounter = 0;

                IEnumerator<TaskContract> TwoStepIncrementFirst()
                {
                    firstCounter++;
                    yield return TaskContract.Yield.It;
                    firstCounter++;
                }

                IEnumerator<TaskContract> TwoStepIncrementSecond()
                {
                    secondCounter++;
                    yield return TaskContract.Yield.It;
                    secondCounter++;
                }

                TwoStepIncrementFirst().RunOn(runner);

                runner.Step();
                Assert.That(firstCounter, Is.EqualTo(1));

                runner.Stop();

                TwoStepIncrementSecond().RunOn(runner);

                for (var i = 0; i < 32 && secondCounter < 2; i++)
                    runner.Step();

                Assert.That(secondCounter, Is.EqualTo(2));
            }
        }

        [Test]
        public void SteppableRunner_Kill_StopsAndPreventsReuse()
        {
            using (var runner = new SteppableRunner("SteppableRunner_Kill"))
            {
                var counter = 0;

                IEnumerator<TaskContract> Increment()
                {
                    counter++;
                    yield return TaskContract.Yield.It;
                    counter++;
                }

                Increment().RunOn(runner);
                runner.Step();
                Assert.That(counter, Is.EqualTo(1));

                // Kill the runner
                // This is internal in FlushingOperation but exposed via Dispose() or internal methods.
                // SteppableRunner.Dispose() calls Kill().
                // However, we want to test Kill specifically if possible, or Dispose behavior.
                // SteppableRunner doesn't expose Kill() publicly, only Dispose().
                // But SveltoTaskRunner.FlushingOperation has a Kill method.
                // Let's use Dispose() which calls Kill().
                
                runner.Dispose();

                // Try to run a new task
                Assert.Throws<DBC.Tasks.PreconditionException>(() =>
                {
                    Increment().RunOn(runner);
                });
            }
        }
    }
}
