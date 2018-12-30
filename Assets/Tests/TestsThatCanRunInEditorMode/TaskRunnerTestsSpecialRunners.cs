#if later
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Svelto.Tasks.Unity;
using UnityEngine.TestTools;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsSpecialRunners
    {
        /// <summary>
        /// StaggeredMonoRunner runs not more than maxTasksPerIteration tasks in one single iteration.
        /// Several tasks must run on this runner to make sense. TaskCollections are considered
        /// single tasks, so they count as single task each.
        /// </summary>
        [UnityTest]
        public IEnumerator TestStaggeredMonoRunner()
        {
            yield return null;

            //careful, runners can be garbage collected if they are not referenced somewhere and the
            //framework does not keep a reference
            using (var staggeredMonoRunner = new StaggeredMonoRunner("staggered", 4))
            {
                ValueObject val = new ValueObject();
                for (int i = 0; i < 16; i++)
                    new SimpleEnumeratorClassRef(val).Run(staggeredMonoRunner);

                Assert.That(staggeredMonoRunner.numberOfQueuedTasks, Is.EqualTo(16));
                staggeredMonoRunner.Step();

                Assert.That(staggeredMonoRunner.numberOfRunningTasks, Is.EqualTo(12));
                staggeredMonoRunner.Step();

                Assert.That(staggeredMonoRunner.numberOfRunningTasks, Is.EqualTo(8));
                staggeredMonoRunner.Step();

                Assert.That(staggeredMonoRunner.numberOfRunningTasks, Is.EqualTo(4));
                staggeredMonoRunner.Step();

                Assert.That(staggeredMonoRunner.numberOfRunningTasks, Is.EqualTo(0));

                Assert.That(val.counter, Is.EqualTo(16));
            }
        }

        [UnityTest]
        public IEnumerator TestTimeBoundMonoRunner()
        {
            yield return null;
            
            var frames = 0;

            using (var timeBoundMonoRunner = new TimeBoundMonoRunner("timebound", 200))
            {
                ValueObject val = new ValueObject();
                for (int i = 0; i < 32; i++)
                    new SimpleEnumeratorClassRefTime(val).Run(timeBoundMonoRunner);

                frames++;
                timeBoundMonoRunner.Step();

                while (timeBoundMonoRunner.numberOfRunningTasks > 0)
                {
                    frames++;
                    timeBoundMonoRunner.Step();
                    yield return null;
                }

                Assert.That(frames, Is.InRange(15,16)); //time based tests are not great
                Assert.That(val.counter, Is.EqualTo(32));
            }
        }

        [UnityTest]
        public IEnumerator TestTimeSlicedMonoRunner()
        {
            yield return null;
            
            var frames = 0;

            using (var timeSlicedMonoRunner = new TimeSlicedMonoRunner("timesliced", 2000))
            {
                ValueObject val        = new ValueObject();
                var         yieldBreak = TimeSlicedYield(val);
                yieldBreak.Run(timeSlicedMonoRunner);
                var yieldBreak1 = TimeSlicedYield(val);
                yieldBreak1.Run(timeSlicedMonoRunner);
                var yieldBreak2 = TimeSlicedYield(val);
                yieldBreak2.Run(timeSlicedMonoRunner);
                var yieldBreak3 = TimeSlicedYield(val);
                yieldBreak3.Run(timeSlicedMonoRunner);

                DateTime then = DateTime.Now;

                frames++;
                timeSlicedMonoRunner.Step(); //first iteration of the runner so that the tasks are filled

                while (timeSlicedMonoRunner.numberOfRunningTasks > 0)
                {
                    frames++;
                    val.counter++;
                    timeSlicedMonoRunner.Step();
                    yield return null;
                }

                Assert.That((DateTime.Now - then).TotalMilliseconds < 1900);
                Assert.That(frames, Is.EqualTo(1));
                Assert.That((int)yieldBreak.Current, Is.EqualTo(100));
                Assert.That((int)yieldBreak1.Current, Is.EqualTo(100));
                Assert.That((int)yieldBreak2.Current, Is.EqualTo(100));
                Assert.That((int)yieldBreak3.Current, Is.EqualTo(100));
            }
        }
        
        [UnityTest]
        public IEnumerator TestTimeSlicedMonoRunnerSliced()
        {
            yield return null;
            
            var frames = 0;

            using (var timeSlicedMonoRunner = new TimeSlicedMonoRunner("timesliced2", 2))
            {
                ValueObject val        = new ValueObject();
                var         yieldBreak = TimeSlicedYieldNormal(val);
                yieldBreak.Run(timeSlicedMonoRunner);
                var yieldBreak1 = TimeSlicedYieldNormal(val);
                yieldBreak1.Run(timeSlicedMonoRunner);
                var yieldBreak2 = TimeSlicedYieldNormal(val);
                yieldBreak2.Run(timeSlicedMonoRunner);
                var yieldBreak3 = TimeSlicedYieldNormal(val);
                yieldBreak3.Run(timeSlicedMonoRunner);

                frames++;
                timeSlicedMonoRunner.Step(); //first iteration of the runner so that the tasks are filled

                while (timeSlicedMonoRunner.numberOfRunningTasks > 0)
                {
                    frames++;
                    val.counter++;
                    timeSlicedMonoRunner.Step();
                    yield return null;
                }

                Assert.That(frames, Is.GreaterThan(1));
                Assert.That((int)yieldBreak.Current, Is.EqualTo(100));
                Assert.That((int)yieldBreak1.Current, Is.EqualTo(100));
                Assert.That((int)yieldBreak2.Current, Is.EqualTo(100));
                Assert.That((int)yieldBreak3.Current, Is.EqualTo(100));
            }
        }

        IEnumerator<TaskContract?> TimeSlicedYield(ValueObject val)
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
        
        IEnumerator<TaskContract?> TimeSlicedYieldNormal(ValueObject val)
        {
            int i = 0;
            while (++i < 100)
            {
                int j = 0;
                while (++j < 100)
                {
                    yield return null;
                }
            }

            yield return i;
        }
    }
}

#endif