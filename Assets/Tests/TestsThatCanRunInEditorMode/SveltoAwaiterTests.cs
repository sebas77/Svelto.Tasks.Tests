using System.Collections;
using NUnit.Framework;
using Svelto.Tasks.Lean;
using UnityEngine.TestTools;
using System.Threading.Tasks;
using UnityEngine;

namespace Test
{
    [TestFixture]
    public class TaskRunnerAwaiterTests
    {
        class testClass
        {
            public bool continued = false;
        }
        [UnityTest]
        public IEnumerator TestThatSveltoAwaiterContinuationDoesNotRunWhenRunnerStops()
        {
            // arrange
            var runner = new SteppableRunner("SveltoAwaiterTest");
            
            var continued = new testClass();

            // obtain the custom awaiter that posts continuation into the runner
            var task = SomeAsyncOperation(continued, runner);
            
            while (continued.continued == false)
                runner.Step();
            
            // give some frames/time to potentially run (it shouldn't)
            yield return new WaitForSeconds(0.2f);

            Assert.False(task.IsCompleted, "Continuation should not run when the runner is stopped");
        }

        async Task SomeAsyncOperation(testClass continued, SteppableRunner runner)
        {
            await Task.Delay(10).GetAwaiter(runner);
            
            continued.continued = true;
            runner.Stop();
            await Task.Delay(10).GetAwaiter(runner);
        }
    }
}
