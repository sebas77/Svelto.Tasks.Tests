using System;
using System.Collections.Generic;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
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
            // The task runner has an inner faster list that starts initialized with a private const value of 3.
            // 32 task continuations should be enough for the foreseeable future to execute a list resize.
            const int requiredTasks = 32;

            // Each task has a number, first it will continue with a task with number + 1.
            // Then it will wait for the sub task to finish, assert that the x value is number + 1 and finally set x to be number.
            // This means that when the 32th task (which has no continue) starts will find x with value 0 set it to 32
            // and then all the parent task will decrement the value by 1.
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

            Continuation task = Task(1).RunOn(_taskRunner);
            
            DateTime timeout = DateTime.Now.AddSeconds(1);
            while (task.isRunning && DateTime.Now < timeout)
            {
                _taskRunner.Step();
            }
            
            if (task.isRunning)
            {
                Assert.Fail("The task did not complete in time");
            }
        }

        SteppableRunner _taskRunner;
    }
}