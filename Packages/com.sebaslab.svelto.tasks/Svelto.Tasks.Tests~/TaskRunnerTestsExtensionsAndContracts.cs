using System.Collections;
using Svelto.Tasks.ExtraLean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class TaskRunnerTestsExtensionsAndContracts
    {
        [Test]
        public void Forget_RunsChildButParentDoesNotWait()
        {
            // What we are testing:
            // TaskRunnerExtensions.Forget executes the child task but the caller doesn't wait for completion.

            using (var runner = new Svelto.Tasks.Lean.SteppableRunner("Forget"))
            {
                var order = new List<int>();

                IEnumerator<TaskContract> Child()
                {
                    order.Add(2);
                    yield return TaskContract.Yield.It;
                    order.Add(3);
                }

                IEnumerator<TaskContract> Parent()
                {
                    order.Add(1);
                    yield return Child().Forget();
                    order.Add(4);
                }

                Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(Parent(), runner);

                runner.Step();

                // Parent ran without waiting for child completion.
                Assert.That(order.Count, Is.EqualTo(2));
                Assert.That(order[0], Is.EqualTo(1));
                Assert.That(order[1], Is.EqualTo(4));

                // Child still runs later.
                runner.Step();
                runner.Step();

                Assert.That(order, Is.EqualTo(new[] { 1, 4, 2, 3 }));
            }
        }

        [Test]
        public void TaskContractContinueIt_TriggersImmediateMoveNext()
        {
            // What we are testing:
            // TaskContract.Continue.It signals "immediate MoveNext" without yielding.
            // This is a pure TaskContract state test.

            TaskContract contract = TaskContract.Continue.It;
            Assert.That(contract.continueIt, Is.True);
        }

        [Test]
        public void ExtraLean_InvalidCurrent_ThrowsSveltoTaskException()
        {
            // What we are testing:
            // ExtraLean enumerators can only yield null/Yield.It/Break.It/Break.AndStop.
            // Returning an unsupported value should throw SveltoTaskException.

            using (var runner = new Svelto.Tasks.ExtraLean.SteppableRunner("ExtraLean_Invalid"))
            {
                IEnumerator Invalid()
                {
                    yield return 123;
                }

                Invalid().RunOn( runner);

                var ex = Assert.Catch(() =>
                {
                    runner.Step();
                });

                // In some Unity setups there can be multiple Svelto assemblies loaded,
                // causing type identity mismatches for Assert.Throws<SveltoTaskException>.
                // Assert on the thrown exception by runtime type name instead.
                Assert.That(ex, Is.Not.Null);
                Assert.That(ex.GetType().Name, Is.EqualTo("SveltoTaskException"));
            }
        }
    }
}
