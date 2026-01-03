//Note the Queue works exactly like a Ring, the only difference is that Queue can grow
//In fact I would have used Queue instead, but I need the Span support

using System;

namespace Svelto.DataStructures
{
    public class RingBuffer<T>
    {
        public RingBuffer(int capacity)
        {
            capacity = NextPowerOfTwo(capacity);
            _entries = new T[capacity];
        }

        public int capacity => _entries.Length;
        public int count => (int) (_producerCursor - _consumerCursor);
        
        public RingBufferEnumerator GetEnumerator()
        {
            return new RingBufferEnumerator(this);   
        }
        
        public ReadOnlySpan<T> AsReadOnlySpan()
        {
            return new ArraySegment<T>(_entries, 0, count);
        }
        
        public void CopyTo(Span<T> destination)
        {
            if (_producerCursor == _consumerCursor)
                return;
            
            if (_producerCursor > _consumerCursor) //no wrap
            {
                _entries.AsSpan((int)_consumerCursor, count).CopyTo(destination);
            }
            else
            {
                var start = (int)_consumerCursor;
                int end = (int)_producerCursor;
                var firstPart = _entries.AsSpan(start);
                var secondPart = _entries.AsSpan(0, end);
                firstPart.CopyTo(destination);
                secondPart.CopyTo(destination.Slice(firstPart.Length));
            }
        }
        
        public void CopyTo(ref SpanList<T> pingTimes)
        {
            if (_producerCursor == _consumerCursor)
                return;
            
            if (_producerCursor > _consumerCursor) //no wrap
            {
                pingTimes.AddRange(_entries.AsSpan((int)_consumerCursor, count));
            }
            else
            {
                var start = (int)_consumerCursor;
                int end = (int)_producerCursor;
                var firstPart = _entries.AsSpan(start);
                var secondPart = _entries.AsSpan(0, end);
                
                pingTimes.AddRange(firstPart);
                pingTimes.AddRange(secondPart);
            }
        }
        
        public void Reset()
        {
            _consumerCursor = _producerCursor = 0;
        }

        public ref T Dequeue()
        {
            if (_consumerCursor == _producerCursor)
                throw new Exception("Queue is empty");
            
            var consumerCursor = _consumerCursor;
            
            _consumerCursor = (uint) ((_consumerCursor + 1) & _modMask);
            
            return ref _entries[consumerCursor];
        }

        public void Enqueue(in T item)
        {
            _entries[_producerCursor] = item;
            
            _producerCursor = (uint) ((_producerCursor + 1) & _modMask);
        }

        //todo: probably better to move to prime and fasts mod
        static int NextPowerOfTwo(int x)
        {
            var result = 2;
            while (result < x)
            {
                result <<= 1;
            }

            return result;
        }
        
        int  _modMask => _entries.Length - 1;
        
        readonly T[]        _entries;
        
        uint _consumerCursor = 0;
        uint _producerCursor = 0;
        
        public struct RingBufferEnumerator
        {
            readonly RingBuffer<T> _ringBuffer;
            uint _index;
            uint _end;

            public RingBufferEnumerator(RingBuffer<T> ringBuffer)
            {
                _ringBuffer = ringBuffer;
                _index = _ringBuffer._consumerCursor;
                _end = _ringBuffer._producerCursor;
            }

            public ref T Current => ref _ringBuffer._entries[_index];

            public bool MoveNext()
            {
                if (_index >= _end)
                {
                    return false;
                }

                _index++;
                return true;
            }
        }
    }
}