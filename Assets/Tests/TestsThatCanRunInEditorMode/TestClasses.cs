using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;

namespace Test
{
    class ValueObject
    {
        public int counter;
    }

    public class TimeoutEnumerator : IEnumerator, IDisposable
    {
        readonly DateTime _then;

        public TimeoutEnumerator()
        {
            _then = DateTime.Now;
        }

        public bool disposed { get; private set; }

        public void Dispose()
        {
            disposed = true;
        }

        public bool MoveNext()
        {
            var timePassed = (float) (DateTime.Now - _then).TotalSeconds;

            if (timePassed > 1)
                return false;

            return true;
        }

        public void Reset()
        {
        }

        public object Current { get; private set; }
    }

    class Enumerator : IEnumerator<TaskContract>
    {
        readonly int totalIterations;
        public   int iterations;

        public Enumerator(int niterations)
        {
            iterations      = 0;
            totalIterations = niterations;
        }

        public long endOfExecutionTime { get; private set; }

        public bool AllRight => iterations == totalIterations;

        public bool MoveNext()
        {
            if (totalIterations < 0) throw new Exception("can't handle this");

            if (iterations < totalIterations)
            {
                ++iterations;

                return true;
            }

            endOfExecutionTime = DateTime.Now.Ticks;

            return false;
        }

        public void Reset() { iterations = 0; }

        public TaskContract Current => Yield.It;
        object IEnumerator.Current  => throw new NotSupportedException();

        public void Dispose() { throw new NotImplementedException(); }
    }

    class SimpleEnumeratorClassRefTime : IEnumerator
    {
        readonly ValueObject _val;

        public SimpleEnumeratorClassRefTime(ValueObject val)
        {
            _val = val;
        }

        public bool MoveNext()
        {
            Thread.Sleep(100);
            _val.counter++;
            return false;
        }

        public void Reset()
        {
        }

        public object Current { get; }
    }

    class SimpleEnumeratorClassRef : IEnumerator
    {
        readonly ValueObject _val;

        public SimpleEnumeratorClassRef(ValueObject val)
        {
            _val = val;
        }

        public bool MoveNext()
        {
            _val.counter++;
            return false;
        }

        public void Reset()
        {
        }

        public object Current { get; }
    }

    class Token
    {
        public int count;
    }

    class WaitEnumerator : IEnumerator
    {
        DateTime       _future;
        readonly int   _time;
        readonly Token _token;

        public WaitEnumerator(Token token, int time = 2)
        {
            _token  = token;
            _future = DateTime.UtcNow.AddSeconds(time);
            _time   = time;
        }

        public WaitEnumerator(int time)
        {
            _future = DateTime.UtcNow.AddSeconds(time);
            _time   = time;
        }

        public void Reset()
        {
            _future = DateTime.UtcNow.AddSeconds(_time);
            if (_token != null)
                _token.count = 0;
        }

        public object Current => null;

        public bool MoveNext()
        {
            if (_future <= DateTime.UtcNow)
            {
                if (_token != null)
                    Interlocked.Increment(ref _token.count);

                return false;
            }

            return true;
        }
    }

    public class SlowTask : IEnumerator
    {
        readonly DateTime _then;

        public SlowTask()
        {
            _then = DateTime.Now.AddSeconds(1);
        }

        public object Current { get; private set; }

        public bool MoveNext()
        {
            if (DateTime.Now < _then)
                return true;
            return false;
        }

        public void Reset()
        {
        }
    }

    public struct SlowTaskStruct : IEnumerator
    {
        readonly DateTime _then;

        public object Current => null;

        public SlowTaskStruct(int seconds) : this()
        {
            _then = DateTime.Now.AddSeconds(seconds);
        }

        public bool MoveNext()
        {
            if (DateTime.Now < _then)
                return true;
            return false;
        }

        public void Reset()
        {
        }
    }
}