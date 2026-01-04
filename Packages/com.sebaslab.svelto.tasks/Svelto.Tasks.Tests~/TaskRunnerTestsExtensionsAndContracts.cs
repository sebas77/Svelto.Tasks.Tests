using System.Collections;
using Svelto.Tasks.ExtraLean;
using Svelto.Tasks.Lean;

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
            
//            The sequence of events is:
//            Step 1: Parent runs, adds 1, yields Forget(). Child is scheduled (added to new tasks queue). Parent pauses.
//                    Step 2: Parent resumes, adds 4, finishes. Child (now in running list) starts, adds 2, yields.
//                    Step 3: Child resumes, adds 3, finishes.
//                    The test now asserts:
//            After Step 1: order is [1]
//                    After Step 2: order is [1, 4, 2]
//                    After Step 3: order is [1, 4, 2, 3]

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

                Parent().RunOn(runner);

                runner.Step();

                // Parent ran until yield. Child is queued but not run yet.
                Assert.That(order.Count, Is.EqualTo(1));
                Assert.That(order[0], Is.EqualTo(1));

                // Parent resumes and finishes. Child starts.
                runner.Step();
                Assert.That(order, Is.EqualTo(new[] { 1, 4, 2 }));

                // Child continues.
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

                Exception caughtException = null;

                void OnException(Exception ex, string msg)
                {
                    caughtException = ex;
                }

                Console.onException += OnException;

                try
                {
                    runner.Step();
                }
                finally
                {
                    Console.onException -= OnException;
                }

                // In some Unity setups there can be multiple Svelto assemblies loaded,
                // causing type identity mismatches for Assert.Throws<SveltoTaskException>.
                // Assert on the thrown exception by runtime type name instead.
                Assert.That(caughtException, Is.Not.Null);
                Assert.That(caughtException.GetType().Name, Is.EqualTo("SveltoTaskException"));
            }
        }
    }
}
