using System;
using NUnit.Framework;
using Svelto.DataStructures.Experimental;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class TombstoneListTests
    {
        [Test]
        public void Add_RemoveAt_ReusesFreedSlots()
        {
            var list = new TombstoneList<int>(4);

            uint a = list.Add(1);
            uint b = list.Add(2);
            uint c = list.Add(3);

            list.RemoveAt(b);

            uint d = list.Add(4);

            Assert.That(d, Is.EqualTo(b));
            Assert.That(list.count, Is.EqualTo(3u));
            Assert.That(list.UnsafeGet(a), Is.EqualTo(1));
            Assert.That(list.UnsafeGet(d), Is.EqualTo(4));
            Assert.That(list.UnsafeGet(c), Is.EqualTo(3));
        }

        [Test]
        public void Clear_ResetsFreelist_AndEnumerationReturnsNoItems()
        {
            var list = new TombstoneList<int>(8);

            list.Add(10);
            list.Add(20);
            list.Add(30);

            list.Clear();

            int enumerated = 0;
            foreach (ref int item in list)
                enumerated++;

            Assert.That(enumerated, Is.EqualTo(0));
            Assert.That(list.count, Is.EqualTo(0u));

            // after clear, the first add must start from index 0 again
            uint index = list.Add(99);
            Assert.That(index, Is.EqualTo(0u));
            Assert.That(list.UnsafeGet(index), Is.EqualTo(99));
        }

        [Test]
        public void Enumerator_Skips_Tombstones()
        {
            var list = new TombstoneList<int>(6);

            uint i0 = list.Add(1);
            uint i1 = list.Add(2);
            uint i2 = list.Add(3);

            list.RemoveAt(i1);

            int sum = 0;
            int count = 0;

            foreach (ref int item in list)
            {
                sum += item;
                count++;
            }

            Assert.That(count, Is.EqualTo(2));
            Assert.That(sum, Is.EqualTo(1 + 3));
            Assert.That(list.count, Is.EqualTo(2u));
            Assert.That(list.UnsafeGet(i0), Is.EqualTo(1));
            Assert.That(list.UnsafeGet(i2), Is.EqualTo(3));
        }

        [Test]
        public void RemoveAt_Twice_Throws()
        {
            var list = new TombstoneList<int>(2);

            uint index = list.Add(123);
            list.RemoveAt(index);

            Assert.That(() => list.RemoveAt(index), Throws.Exception);
        }
    }
}
