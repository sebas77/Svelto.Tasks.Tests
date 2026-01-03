using Svelto.Tasks;
using Svelto.Tasks.Lean;

using Svelto.Tasks.Parallelism;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsTaskCollections
    {
        struct TestJob : ISveltoJob
        {
            public int[] results;

            public void Update(int index)
            {
                System.Threading.Interlocked.Increment(ref results[index]);
            }
        }

        [Test]
        public void MultiThreadedParallelJobCollection_RunsJobsInParallel()
        {
            // What we are testing:
            // MultiThreadedParallelJobCollection distributes job iterations across multiple threads.

            var job = new TestJob { results = new int[1024] };
            using (var collection = new MultiThreadedParallelJobCollection<TestJob>("test", 4, false))
            {
                collection.Add(ref job, 1024);

                // Job collection is an IEnumerator<TaskContract>, it can be run like any other task
                // We use Complete() extension to run it synchronously for the test
                collection.Complete(2000);
            }

            for (int i = 0; i < 1024; i++)
                Assert.That(job.results[i], Is.EqualTo(1), $"Index {i} was not updated correctly");
        }

        [Test]
        public void SerialTaskCollection_ExecutesTasksInOrder_AndIsReusable()
        {
            // What we are testing:
            // SerialTaskCollection runs tasks one after another and can be reused after Reset().

            var serial = new SerialTaskCollection("serial");

            var log = new List<int>();

            IEnumerator<TaskContract> Task(int id)
            {
                log.Add(id);
                yield break;
            }

            serial.Add(Task(1));
            serial.Add(Task(2));
            serial.Add(Task(3));

            using (var runner = new SyncRunner("sync"))
            {
                serial.RunOn(runner);
                runner.ForceComplete(1000);
            }

            Assert.That(log.Count, Is.EqualTo(3));
            Assert.That(log[0], Is.EqualTo(1));
            Assert.That(log[1], Is.EqualTo(2));
            Assert.That(log[2], Is.EqualTo(3));

            serial.Reset();
            log.Clear();

            serial.Add(Task(4));
            serial.Add(Task(5));

            using (var runner = new SyncRunner("sync2"))
            {
                serial.RunOn(runner);
                runner.ForceComplete(1000);
            }

            Assert.That(log.Count, Is.EqualTo(2));
            Assert.That(log[0], Is.EqualTo(4));
            Assert.That(log[1], Is.EqualTo(5));
        }

        [Test]
        public void ParallelTaskCollection_CompletesAllTasks()
        {
            // What we are testing:
            // ParallelTaskCollection progresses tasks until all complete.

            var parallel = new ParallelTaskCollection("parallel", 4);

            var a = 0;
            var b = 0;

            IEnumerator<TaskContract> TaskA()
            {
                a++;
                yield return TaskContract.Yield.It;
                a++;
            }

            IEnumerator<TaskContract> TaskB()
            {
                b++;
                yield return TaskContract.Yield.It;
                b++;
            }

            parallel.Add(TaskA());
            parallel.Add(TaskB());

            using (var runner = new SteppableRunner("step"))
            {
                parallel.RunOn(runner);

                var safety = 0;
                while (runner.hasTasks && safety++ < 256)
                    runner.Step();
            }

            Assert.That(a, Is.EqualTo(2));
            Assert.That(b, Is.EqualTo(2));
        }

        [Test]
        public void TaskCollection_AddWhileRunning_Throws()
        {
            // What we are testing:
            // TaskCollection enforces that Add() can't be called while the collection is running.

            var serial = new SerialTaskCollection("serial");

            IEnumerator<TaskContract> YieldOnce()
            {
                yield return TaskContract.Yield.It;
            }

            serial.Add(YieldOnce());

            Assert.Throws<Exception>(() =>
            {
                // One MoveNext puts the collection in running state.
                serial.MoveNext();
                serial.Add(YieldOnce());
            });
        }

        [Test]
        public void TaskCollection_Clear_RemovesAllTasks()
        {
            // What we are testing:
            // Clear() empties the task collection.

            var serial = new SerialTaskCollection("serial");

            IEnumerator<TaskContract> Empty()
            {
                yield break;
            }

            serial.Add(Empty());
            serial.Add(Empty());

            serial.Clear();

            Assert.That(serial.Current, Is.EqualTo(default(TaskContract)));
        }
    }
}
