using NUnit.Framework;
using Svelto.Tasks.Parallelism;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class MultiThreadedParallelTaskCollectionTests
    {
        [Test]
        public void MultiThreadedParallelTaskCollection_RunsTasksInParallel()
        {
            // What we are testing:
            // MultiThreadedParallelTaskCollection runs tasks on multiple threads.
            
            using (var collection = new Parallelism.ExtraLean.MultiThreadedParallelTaskCollection<WaitEnumerator>("test_parallel", 4, false))
            {
                bool done = false;
                collection.onComplete += () => done = true;
                Token token = new Token();

                // Add 4 tasks that wait 1 second each.
                // If run sequentially, it would take 4 seconds.
                // If run in parallel (4 threads), it should take ~1 second.
                collection.Add(new WaitEnumerator(token, 1));
                collection.Add(new WaitEnumerator(token, 1));
                collection.Add(new WaitEnumerator(token, 1));
                collection.Add(new WaitEnumerator(token, 1));

                DateTime now = DateTime.Now;

                collection.Complete(2000); // Timeout 2s

                var totalSeconds = (DateTime.Now - now).TotalSeconds;

                Assert.That(totalSeconds, Is.GreaterThan(0.9));
                Assert.That(totalSeconds, Is.LessThan(1.5)); // Should be close to 1s, definitely less than 4s
                Assert.That(done, Is.True);
                Assert.That(token.count, Is.EqualTo(4));
            }
        }

        [Test]
        public void MultiThreadedParallelTaskCollection_Reset_ClearsTasksAndAllowsReuse()
        {
            // What we are testing:
            // Reset() clears the collection and allows adding new tasks.
            
            using (var collection = new Parallelism.ExtraLean.MultiThreadedParallelTaskCollection<WaitEnumerator>("test_reset", 4, false))
            {
                Token token = new Token();
                collection.Add(new WaitEnumerator(token, 0)); // Instant task

                collection.Complete(1000);
                Assert.That(token.count, Is.EqualTo(1));

                collection.Reset();

                // Collection should be empty now (conceptually), or at least ready for new tasks.
                // MultiThreadedParallelTaskCollection.Reset() clears the internal list.
                
                Token token2 = new Token();
                collection.Add(new WaitEnumerator(token2, 0));
                
                collection.Complete(1000);
                Assert.That(token2.count, Is.EqualTo(1));
                // token1 should not be incremented again because the old task was removed
                Assert.That(token.count, Is.EqualTo(1)); 
            }
        }

        [Test]
        public void MultiThreadedParallelTaskCollection_AddWhileRunning_Throws()
        {
            using (var collection = new Parallelism.ExtraLean.MultiThreadedParallelTaskCollection<WaitEnumerator>("test_add_running", 4, false))
            {
                collection.Add(new WaitEnumerator(1));

                // Start running manually to control state
                collection.MoveNext(); 

                Assert.That(collection.isRunning, Is.True);

                Assert.Throws<MultiThreadedParallelTaskCollectionException>(() =>
                {
                    collection.Add(new WaitEnumerator(1));
                });
                
                // Finish execution
                collection.Complete(2000);
            }
        }
        
        [Test]
        public void MultiThreadedParallelTaskCollection_Stop_StopsExecution()
        {
             using (var collection = new Parallelism.ExtraLean.MultiThreadedParallelTaskCollection<WaitEnumerator>("test_stop", 4, false))
            {
                // Add long running tasks
                collection.Add(new WaitEnumerator(5)); 
                collection.Add(new WaitEnumerator(5));

                // Start
                collection.MoveNext();
                Assert.That(collection.isRunning, Is.True);

                collection.Stop();

                Assert.That(collection.isRunning, Is.False);
                
                // Verify we can dispose without issues
            }
        }

        [Test]
        public void MultiThreadedParallelTaskCollection_ExtraLean_RunsTasksInParallel()
        {
            // What we are testing:
            // MultiThreadedParallelTaskCollection (ExtraLean) runs tasks on multiple threads.
            
            using (var collection = new Svelto.Tasks.Parallelism.ExtraLean.MultiThreadedParallelTaskCollection<WaitEnumeratorExtraLean>("test_parallel_extralean", 4, false))
            {
                bool done = false;
                collection.onComplete += () => done = true;
                Token token = new Token();

                collection.Add(new WaitEnumeratorExtraLean(token, 1));
                collection.Add(new WaitEnumeratorExtraLean(token, 1));
                collection.Add(new WaitEnumeratorExtraLean(token, 1));
                collection.Add(new WaitEnumeratorExtraLean(token, 1));

                DateTime now = DateTime.Now;

                collection.Complete(2000); // Timeout 2s

                var totalSeconds = (DateTime.Now - now).TotalSeconds;

                Assert.That(totalSeconds, Is.GreaterThan(0.9));
                Assert.That(totalSeconds, Is.LessThan(1.5));
                Assert.That(done, Is.True);
                Assert.That(token.count, Is.EqualTo(4));
            }
        }

        [Test]
        public void MultiThreadedParallelTaskCollection_Stop_DoesNotClearAndAllowsReuse()
        {
            using (var collection = new Parallelism.ExtraLean.MultiThreadedParallelTaskCollection<WaitEnumerator>("test_stop_reuse", 2, false))
            {
                var token = new Token();
                collection.Add(new WaitEnumerator(token, 0));

                collection.MoveNext();
                Assert.That(collection.isRunning, Is.True);

                collection.Stop();
                Assert.That(collection.isRunning, Is.False);

                token.count = 0;

                collection.Complete(1000);

                Assert.That(token.count, Is.EqualTo(1));
            }
        }
    }
}
