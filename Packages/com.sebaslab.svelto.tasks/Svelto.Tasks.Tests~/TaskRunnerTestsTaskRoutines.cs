using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class TaskRunnerTestsRunnerLifecycle
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

                OneStepIncrement().RunOn(runner);

                Assert.That(runner.hasTasks, Is.True);

                runner.Stop();

                // Adding a task while stopped should not execute until the runner transitions out of stopping.
                OneStepIncrement().RunOn(runner);

                runner.Step();

                Assert.That(counter, Is.EqualTo(1));

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

                runner.Stop();
            }
        }

        [Test]
        public void SteppableRunner_TimeSlicedFlow_UsesFloatMilliseconds()
        {
            // What we are testing:
            // TimeSlicedFlow accepts float milliseconds and gates progress (sanity check compile + behavior).

            using (var runner = new SteppableRunner("TimeSlicedFlow_FloatMs"))
            {
                runner.UseFlowModifier(new TimeSlicedFlow(5.5f));

                var counter = 0;

                IEnumerator<TaskContract> Work()
                {
                    // keep runner busy across multiple steps
                    var i = 0;
                    while (i++ < 256)
                    {
                        counter++;
                        yield return TaskContract.Yield.It;
                    }
                }

                Work().RunOn(runner);

                // We don't assert exact timing; just ensure it progresses and eventually completes.
                var safety = 0;
                while (runner.hasTasks && safety++ < 4096)
                    runner.Step();

                Assert.That(safety, Is.LessThan(4096));
                Assert.That(counter, Is.EqualTo(256));
            }
        }

        [Test]
        public void SteppableRunner_StaggeredFlow_CapsTasksPerIteration()
        {
            // What we are testing:
            // StaggeredFlow limits how many tasks can be processed per Step() iteration.

            using (var runner = new SteppableRunner("StaggeredFlow"))
            {
                runner.UseFlowModifier(new StaggeredFlow(2));

                var counter = 0;

                IEnumerator<TaskContract> OneTick()
                {
                    counter++;
                    yield break;
                }

                for (var i = 0; i < 8; i++)
                    OneTick().RunOn(runner);

                runner.Step();

                Assert.That(counter, Is.EqualTo(2));

                runner.Step();
                Assert.That(counter, Is.EqualTo(4));

                runner.Step();
                Assert.That(counter, Is.EqualTo(6));

                runner.Step();
                Assert.That(counter, Is.EqualTo(8));
            }
        }
    }
}
