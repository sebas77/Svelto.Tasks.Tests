using System;
using System.Collections;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Chain;

namespace Test
{
    class ServiceTask : IServiceTask
    {
        public bool isDone { get; private set; }

        public ServiceTask()
        {
            isDone = false;
        }

        public void Execute()
        {
            _delayTimer = new System.Timers.Timer
            {
                Interval = 1000,
                Enabled  = true
            };
            _delayTimer.Elapsed += _delayTimer_Elapsed;
            _delayTimer.Start();
        }

        public void OnComplete(Action action)
        {
            _onComplete += action;
        }

        void _delayTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            isDone = true;
            if (_onComplete != null)
                _onComplete();

            _delayTimer.Stop();
            _delayTimer = null;
        }

        System.Timers.Timer _delayTimer;
        Action              _onComplete;
    }

    class TaskChain : ITaskChain<ValueObject>
    {
        public bool isDone { get; private set; }

        public TaskChain()
        {
            isDone = false;
        }

        public bool MoveNext()
        {
            Interlocked.Increment(ref token.counter);

            isDone = true;

            return false;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public object      Current { get; }
        public ValueObject token   { get; set; }
    }

    class ValueObject
    {
        public int counter;
    }

    public class TimeoutEnumerator : IEnumerator, IDisposable
    {
        public TimeoutEnumerator()
        {
            _then = DateTime.Now;
        }

        public bool MoveNext()
        {
            var timePassed = (float) (DateTime.Now - _then).TotalSeconds;

            if (timePassed > 1)
                return false;

            return true;
        }

        public void Reset()
        {}

        public object Current { get; private set; }

        readonly DateTime _then;

        public void Dispose()
        {
            disposed = true;
        }

        public bool disposed { get; private set; }
    }

    class Enumerator : IEnumerator
    {
        public long endOfExecutionTime { get; private set; }
        public bool AllRight
        {
            get { return iterations == totalIterations; }
        }

        public Enumerator(int niterations)
        {
            iterations      = 0;
            totalIterations = niterations;
        }
        
        public bool MoveNext()
        {
            if (totalIterations < 0)
                throw new Exception("can't handle this");

            if (iterations < totalIterations)
            {
                ++iterations;

                return true;
            }
            
            endOfExecutionTime = DateTime.Now.Ticks;

            return false;
        }

        public void Reset()
        {
            iterations = 0;
        }

        public object Current { get; }
        
        readonly int totalIterations;
        public   int iterations;
    }
    
    class SimpleEnumeratorClassRefTime : IEnumerator
    {
        ValueObject _val;

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
        ValueObject _val;

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
        {}

        public object Current { get; }
    }

    class Token
    {
        public int count;
    }

    class WaitEnumerator : IEnumerator
    {
        public WaitEnumerator(Token token, int time = 2)
        {
            _token  = token;
            _future = DateTime.UtcNow.AddSeconds(time);
            _time = time;
        }
        
        public WaitEnumerator(int time)
        {
            _future = DateTime.UtcNow.AddSeconds(time);
            _time   = time;
        }

        public void Reset()
        {
            _future      = DateTime.UtcNow.AddSeconds(_time);
            if (_token != null)
            _token.count = 0;
        }

        public object Current
        {
            get { return null; }
        }

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
        
        DateTime _future;
        int      _time;
        Token    _token;
    }

    public class SlowTask : IEnumerator
    {
        DateTime      _then;
        public object Current { get; private set; }

        public SlowTask()
        {
            _then = DateTime.Now.AddSeconds(1);
        }

        public bool MoveNext()
        {
            if (DateTime.Now < _then)
                return true;
            return false;
        }

        public void Reset()
        {}
    }
    
    public struct SlowTaskStruct : IEnumerator
    {
        readonly DateTime      _then;

        public object Current
        {
            get { return null; }
        }

        public SlowTaskStruct(int seconds):this()
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
        {}
    }
}