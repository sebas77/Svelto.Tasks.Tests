using System.Collections.Generic;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class IteratorBlockPoolTests
    {
        class PoolData
        {
            public int value;
        }

        [Test]
        public void Lean_IteratorBlockPool_RecyclesBlocks()
        {
            // What we are testing:
            // IteratorBlockPool should reuse PooledIteratorBlock instances after they are released.

            int creationCount = 0;
            IEnumerator<TaskContract> MyIterator(PoolData data)
            {
                creationCount++;
                data.value++;
                yield return TaskContract.Yield.It;
                data.value++;
            }

            var pool = new IteratorBlockPool<PoolData>(MyIterator, "TestPool");

            // Get first block
            var (data1, block1) = pool.Get();
            Assert.That(data1.value, Is.EqualTo(0));
            
            // Run it to completion
            block1.MoveNext(); // first step
            Assert.That(data1.value, Is.EqualTo(1));
            Assert.That(creationCount, Is.EqualTo(1));
            
            block1.MoveNext(); // second step (completes and releases)
            Assert.That(data1.value, Is.EqualTo(2));
            
            // block1 should have been released to pool now
            
            // Get second block
            var (data2, block2) = pool.Get();
            
            Assert.That(data2, Is.SameAs(data1));
            Assert.That(block2, Is.SameAs(block1));
            
            // Verify it actually runs again (not stuck in finished state)
            data2.value = 0;
            block2.MoveNext();
            Assert.That(data2.value, Is.EqualTo(1));
            Assert.That(creationCount, Is.EqualTo(2)); // New iterator was created
        }
        
        [Test]
        public void ExtraLean_IteratorBlockPool_RecyclesBlocks()
        {
            // What we are testing:
            // ExtraLean IteratorBlockPool should also recycle blocks.
            
            int creationCount = 0;
            System.Collections.IEnumerator MyIterator(PoolData data)
            {
                creationCount++;
                data.value++;
                yield return null;
                data.value++;
            }

            var pool = new Svelto.Tasks.ExtraLean.IteratorBlockPool<PoolData>(MyIterator, "ExtraLeanTestPool");

            var (data1, block1) = pool.Get();
            block1.MoveNext();
            block1.MoveNext(); // Completes and releases
            
            var (data2, block2) = pool.Get();
            Assert.That(data2, Is.SameAs(data1));
            Assert.That(block2, Is.SameAs(block1));
        }
    }
}

