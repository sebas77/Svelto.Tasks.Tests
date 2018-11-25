using System;
using System.Collections;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Unity;
using Svelto.Tasks.Unity.Internal;
using UnityEngine.TestTools;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsSpecialRunners
    {
        [UnityTest]
        public IEnumerator TestStaggeredMonoRunner()
        {
            yield return null;

            var staggeredMonoRunner = new StaggeredMonoRunner("staggered", 4);

            ValueObject val = new ValueObject();
            for (int i = 0; i < 16; i++)
                new SimpleEnumeratorClassRef(val).RunOnScheduler(staggeredMonoRunner);

            var runnerBehaviour = staggeredMonoRunner._go.GetComponent<RunnerBehaviourUpdate>();

            runnerBehaviour.Update();

            Assert.That(staggeredMonoRunner.numberOfRunningTasks, Is.EqualTo(12));
            runnerBehaviour.Update();

            Assert.That(staggeredMonoRunner.numberOfRunningTasks, Is.EqualTo(8));
            runnerBehaviour.Update();

            Assert.That(staggeredMonoRunner.numberOfRunningTasks, Is.EqualTo(4));
            runnerBehaviour.Update();

            Assert.That(staggeredMonoRunner.numberOfRunningTasks, Is.EqualTo(0));

            Assert.That(val.counter, Is.EqualTo(16));
        }

        [UnityTest]
        public IEnumerator TestTimeBoundMonoRunner()
        {
            var frames = 0;

            var timeBoundMonoRunner = new TimeBoundMonoRunner("timebound", 200);

            ValueObject val = new ValueObject();
            for (int i = 0; i < 32; i++)
                new SimpleEnumeratorClassRefTime(val).RunOnScheduler(timeBoundMonoRunner);

            var runnerBehaviour = timeBoundMonoRunner._go.GetComponent<RunnerBehaviourUpdate>();

            frames++;
            runnerBehaviour.Update(); //first iteration of the runner so that the tasks are filled

            while (timeBoundMonoRunner.numberOfRunningTasks > 0)
            {
                frames++;
                runnerBehaviour.Update();
                yield return null;
            }

            Assert.That(frames, Is.EqualTo(16));
            Assert.That(val.counter, Is.EqualTo(32));
        }

        [UnityTest]
        public IEnumerator TestTimeSlicedMonoRunner()
        {
            var frames = 0;

            var timeSlicedMonoRunner = new TimeSlicedMonoRunner("timesliced", 200);

            ValueObject val        = new ValueObject();
            var         yieldBreak = TimeSlicedYield(val);
            yieldBreak.RunOnScheduler(timeSlicedMonoRunner);
            var yieldBreak1 = TimeSlicedYield(val);
            yieldBreak1.RunOnScheduler(timeSlicedMonoRunner);
            var yieldBreak2 = TimeSlicedYield(val);
            yieldBreak2.RunOnScheduler(timeSlicedMonoRunner);
            var yieldBreak3 = TimeSlicedYield(val);
            yieldBreak3.RunOnScheduler(timeSlicedMonoRunner);

            var runnerBehaviour = timeSlicedMonoRunner._go.GetComponent<RunnerBehaviourUpdate>();

            frames++;
            runnerBehaviour.Update(); //first iteration of the runner so that the tasks are filled

            while (timeSlicedMonoRunner.numberOfRunningTasks > 0)
            {
                frames++;
                val.counter++;
                runnerBehaviour.Update();
                yield return null;
            }

            Assert.That(frames, Is.EqualTo(1));
            Assert.That(yieldBreak.Current, Is.EqualTo(100));
            Assert.That(yieldBreak1.Current, Is.EqualTo(100));
            Assert.That(yieldBreak2.Current, Is.EqualTo(100));
            Assert.That(yieldBreak3.Current, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator TestTimeSlicedMonoRunnerWithBreak()
        {
            var frames = 0;

            var timeSlicedMonoRunner = new TimeSlicedMonoRunner("timesliced", 200);

            ValueObject val        = new ValueObject();
            var         yieldBreak = TimeSlicedYieldBreak(val);
            yieldBreak.RunOnScheduler(timeSlicedMonoRunner);
            var yieldBreak1 = TimeSlicedYieldBreak(val);
            yieldBreak1.RunOnScheduler(timeSlicedMonoRunner);
            var yieldBreak2 = TimeSlicedYieldBreak(val);
            yieldBreak2.RunOnScheduler(timeSlicedMonoRunner);
            var yieldBreak3 = TimeSlicedYieldBreak(val);
            yieldBreak3.RunOnScheduler(timeSlicedMonoRunner);

            var runnerBehaviour = timeSlicedMonoRunner._go.GetComponent<RunnerBehaviourUpdate>();

            frames++;
            runnerBehaviour.Update(); //first iteration of the runner so that the tasks are filled

            while (timeSlicedMonoRunner.numberOfRunningTasks > 0)
            {
                frames++;
                val.counter++;
                runnerBehaviour.Update();
                yield return null;
            }

            Assert.That(frames, Is.EqualTo(100));
            Assert.That(yieldBreak.Current, Is.EqualTo(100));
            Assert.That(yieldBreak1.Current, Is.EqualTo(100));
            Assert.That(yieldBreak2.Current, Is.EqualTo(100));
            Assert.That(yieldBreak3.Current, Is.EqualTo(100));
        }
        
        IEnumerator TimeSlicedYieldBreak(ValueObject val)
        {
            int i = 0;
            while (++i < 100)
            {
                int j = 0;
                while (++j < 100)
                {
                    var frame = val.counter;
                    yield return null;
                    if (frame != val.counter) throw new Exception("must always finish before next iteration");
                }

                yield return Break.RunnerExecutionAndResumeNextIteration;
            }

            yield return i;
        }

        IEnumerator TimeSlicedYield(ValueObject val)
        {
            int i = 0;
            while (++i < 100)
            {
                int j = 0;
                while (++j < 100)
                {
                    var frame = val.counter;
                    yield return null;
                    if (frame != val.counter) throw new Exception("must always finish before next iteration");
                }
            }

            yield return i;
        }
    }
}