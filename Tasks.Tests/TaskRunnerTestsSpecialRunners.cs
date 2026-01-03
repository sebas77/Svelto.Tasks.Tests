using Svelto.Tasks;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Lean;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsFlowModifiers
    {
        [Test]
        public void TimeBoundFlow_BoundsWorkPerStep_OnSteppableRunner()
        {
            // What we are testing:
            // TimeBoundFlow should prevent a single Step() from running past the configured time budget.
            // We assert qualitative behavior: it should take multiple steps to finish a workload.

            using (var runner = new SteppableRunner("TimeBoundFlow_Steppable"))
            {
                runner.UseFlowModifier(new TimeBoundFlow(20f));

                var counter = 0;

                IEnumerator<TaskContract> Work()
                {
                    var i = 0;
                    while (i++ < 512)
                    {
                        counter++;
                        yield return TaskContract.Yield.It;
                    }
                }

                Work().RunOn(runner);

                runner.Step();

                Assert.That(counter, Is.GreaterThan(0));
                Assert.That(counter, Is.LessThan(512));

                var safety = 0;
                while (runner.hasTasks && safety++ < 8192)
                    runner.Step();

                Assert.That(counter, Is.EqualTo(512));
            }
        }

        [Test]
        public void TimeBoundFlow_BoundsWorkPerIteration_OnMultiThreadRunner()
        {
            // What we are testing:
            // TimeBoundFlow compiles/works on MultiThreadRunner and completes within a reasonable time.

            using (var runner = new MultiThreadRunner("TimeBoundFlow_MT"))
            {
                runner.UseFlowModifier(new TimeBoundFlow(20f));

                var counter = 0;

                IEnumerator<TaskContract> Work()
                {
                    var i = 0;
                    while (i++ < 512)
                    {
                        counter++;
                        yield return TaskContract.Yield.It;
                    }
                }

                Work().RunOn(runner);

                var then = DateTime.UtcNow.AddSeconds(2);
                while (runner.hasTasks && DateTime.UtcNow < then)
                {
                }

                Assert.That(counter, Is.EqualTo(512));
            }
        }
    }
}
