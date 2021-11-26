using System.Collections.Generic;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsLeanContinuation
    {
        [SetUp]
        public void Setup()
        {
            _taskRunner = new SteppableRunner("LeanSveltoStepRunner");
        }

        [Test]
        public void TestThatLeanTasksWaitForContinuesWhenRunnerListsResize()
        {
            const int requiredTasks = 32;
            int x = 0;

            IEnumerator<TaskContract> Task(int number)
            {
                if (number < requiredTasks)
                {
                    yield return Task(number + 1).Continue();
                    Assert.AreEqual(number + 1, x, $"Task {number} did not wait its continuation task");
                }
                else
                {
                    Assert.AreEqual(0, x);
                }

                x = number;
            }

            var task = Task(1).RunOn(_taskRunner);
            while (task.isRunning)
            {
                _taskRunner.Step();
            }
        }

        SteppableRunner _taskRunner;
    }
}