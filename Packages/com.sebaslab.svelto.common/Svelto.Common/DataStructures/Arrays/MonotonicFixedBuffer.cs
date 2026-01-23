using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Svelto.Utilities;

namespace Svelto.DataStructures
{
// MonotonicWindowBuffer<T>
// ---------------------------------
// SPSC (single producer / single consumer) fixed-capacity sliding window indexed by a monotonically
// increasing int "logical index". The producer may publish indices out-of-order (e.g. set 8 before 5).
//
// IMPORTANT: T must be safe to publish/read across threads with no additional synchronization.
// In practice that means T should be immutable after Set(), or otherwise thread-safe for concurrent
// producer write + consumer read.
//
// Queue semantics are preserved by the consumer consuming strictly in-order: TryPeek/TryDequeue only
// operate on the current head index. Therefore if there is a "hole" (e.g. head=5 not published yet),
// the consumer will stall even if later indices (6,7,8,...) are already present.
//
// Operations are O(1) and non-blocking; readiness is tracked per slot via a PublishedIndex marker.
// Publication order is "write Value, then Volatile.Write(PublishedIndex)" so the consumer won’t
// observe an index as present before its value is visible (release/acquire pattern). [web:184][web:98]

    public sealed class MonotonicWindowBuffer<T>
    {
        // Combined buffer for cache locality. published marker: slot contains logical index i if published == i + 1.
        // (i + 1 so default 0 means "not published")
        readonly (T value, uint published)[] _buffer;

        readonly uint _capacity;
        readonly uint _mask;
        readonly uint _expectedCount;

        // Consumer-owned: next logical index to dequeue/peek.
        // -1 means "head not set".
        int _head;

        // Highest published index so far. -1 means nothing published yet.
        int _highestPublished;

        public MonotonicWindowBuffer(uint expectedCount)
        {
            if (expectedCount == 0)
                throw new ArgumentOutOfRangeException(nameof(expectedCount));

            _expectedCount = expectedCount;

            _capacity = Utils.NextPowerOfTwo(expectedCount);
            _mask = _capacity - 1;

            int len = checked((int)_capacity);
            _buffer = new (T, uint)[len];
            _head = -1;
            _highestPublished = -1;
        }

        // Returns the span from head to highest published index (inclusive), or 0 if nothing ready.
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int head = Volatile.Read(ref _head);
                int highest = Volatile.Read(ref _highestPublished);
                
                if (head == -1 || highest == -1 || highest < head)
                    return 0;
                
                return highest - head + 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Set(int index, in T value)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            int head = Volatile.Read(ref _head);

            // If head isn't set yet, this is an unsafe set: no window checks can be performed.
            if (head != -1)
            {
                // Do nothing if retired or outside window
                if (index < head)
                    return false;

                if ((uint)(index - head) >= _expectedCount)
                    throw new MonotonicWindowBufferOverflowException($"Index out of window range, expected count: {_expectedCount}, index: {index}, head: {head}");
            }

            int slot = (int)(((uint)index) & _mask);

            ref var valueTuple = ref _buffer[slot];
            valueTuple.value = value;

            uint expectedMarker = (uint)index + 1;

            Volatile.Write(ref valueTuple.published, expectedMarker);

            // Update highest published index (SPSC safe - we are the only writer).
            if (index > _highestPublished)
            {
                Volatile.Write(ref _highestPublished, index);
            }
  
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //summary
        /// Consumer-only. Try get value at index if (and only if) published. Does not retire.
        /// Get is always an unsafe operation: no window checks are performed.
        /// 
        public bool TryGet(int index, out T value)
        {
            int slot = (int)(((uint)index) & _mask);

            ref var valueTuple = ref _buffer[slot];
            if (Volatile.Read(ref valueTuple.published) != (uint)index + 1)
            {
                value = default;
                return false;
            }

            value = valueTuple.value;
            return true;
        }

        /// <summary>
        /// Consumer-only. Peek current head if (and only if) head has been published. Does not retire.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out T value)
        {
            int head = Volatile.Read(ref _head);
            if (head == -1)
                throw new InvalidOperationException("Head not set.");

            uint headU = (uint)head;
            int slot = (int)(headU & _mask);

            ref var valueTuple = ref _buffer[slot];
            if (Volatile.Read(ref valueTuple.published) != headU + 1)
            {
                value = default;
                return false;
            }

            value = valueTuple.value;
            return true;
        }

        /// <summary>
        /// Consumer-only. Dequeue (retire) current head if published.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T value)
        {
            int head = Volatile.Read(ref _head);
            if (head == -1)
                throw new InvalidOperationException("Head not set.");

            uint headU = (uint)head;
            int slot = (int)(headU & _mask);

            ref var valueTuple = ref _buffer[slot];
            if (Volatile.Read(ref valueTuple.published) != headU + 1)
            {
                value = default!;
                return false;
            }

            value = valueTuple.value;
            
            // Retire head (release).
            Volatile.Write(ref _head, head + 1);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetHead(int newHead)
        {
            if (newHead < 0)
                throw new ArgumentOutOfRangeException(nameof(newHead));

            // Consumer-only. New head must not be retired => must be >= current head.
            int currentHead = Volatile.Read(ref _head);
            if (currentHead != -1 && newHead < currentHead)
                throw new InvalidOperationException("New head is retired (cannot move head backwards).");

            Volatile.Write(ref _head, newHead);
        }
    }

    public class MonotonicWindowBufferOverflowException : Exception
    {
        public MonotonicWindowBufferOverflowException(string message) : base(message)
        {
        }
    }
}
