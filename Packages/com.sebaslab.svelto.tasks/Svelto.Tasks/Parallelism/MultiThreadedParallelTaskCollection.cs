using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.ExtraLean;
using Svelto.Utilities;
using MultiThreadRunner = Svelto.Tasks.ExtraLean.MultiThreadRunner;

namespace Svelto.Tasks.Parallelism
{
    public class MultiThreadedParallelTaskCollectionException : Exception
    {
        public MultiThreadedParallelTaskCollectionException(
            string canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection) : base(
            canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection)
        {
        }
    }
}

namespace Svelto.Tasks.Parallelism.Lean
{
    public class MultiThreadedParallelTaskCollection<TTask> : IEnumerator<TaskContract>
        where TTask : IEnumerator<TaskContract>
    {
        public event Action onComplete;

        public TaskContract Current
        {
            get { return TaskContract.Yield.It; }
        }

        object IEnumerator.Current
        {
            get => throw new NotImplementedException();
        }

        public bool isRunning { private set; get; }

        /// <summary>
        ///  
        /// </summary>
        /// <param name="numberOfThreads"></param>
        /// <param name="tightTasks">
        /// if several cache friendly and optimized tasks run in parallel, using tightTasks may improve parallelism
        /// as gives the chance to other threads to run.
        /// </param>
        public MultiThreadedParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks)
        {
            _decrementRunningThread = DecrementRunningThread;
            DBC.Tasks.Check.Require(numberOfThreads > 0, "doesn't make much sense to use this with 0 threads");

            _name = name;

            InitializeThreadsAndData(numberOfThreads, tightTasks);
        }
        
        public MultiThreadedParallelTaskCollection(string name, bool tightTasks):this(name, (uint)Math.Max(1, Environment.ProcessorCount - 2), tightTasks)
        { }

        /// <summary>
        /// Add can be called by another thread, so if the collection is already running
        /// I can't allow adding more tasks.
        /// </summary>
        /// <param name="enumerator"></param>
        /// <exception cref="MultiThreadedParallelTaskCollectionException"></exception>
        public void Add(TTask enumerator)
        {
            if (isRunning == true)
                throw new MultiThreadedParallelTaskCollectionException(
                    "can't add tasks on a started MultiThreadedParallelTaskCollection");

            _parallelTasks.Add(enumerator);
            _numberOfTasksAdded++;
        }

        public bool MoveNext()
        {
            //isDisposed can be set by the GC finalizer thread
            if (Volatile.Read(ref _isDisposed)) return false;

            if (RunMultiThreadParallelTasks()) return true;

            if (onComplete != null)
                onComplete();

            isRunning = false;

            return false;
        }

        public void Reset()
        {
            Stop(0);

            _numberOfTasksAdded = 0;
            _parallelTasks.Clear();
            isRunning = false;
        }

        public void Stop()
        {
            Stop(0);
        }

        public void Stop(int msTimeout)
        {
            if (isRunning == false)
                return;

            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Stop();

            if (msTimeout > 0)
            {
                var then = DateTime.UtcNow.AddMilliseconds(msTimeout);

                //wait until each runner has finished flushing its running tasks
                for (int i = 0; i < _runners.Length; i++)
                    while (_runners[i].isStopping == true)
                    {
                        if (DateTime.UtcNow > then)
                            throw new SveltoTaskException("MultiThreadedParallelTaskCollection.Stop() timed out, increase time out or check if it got stuck");

                        ThreadUtility.TakeItEasy();
                    }
            }
            else
            {
                //wait until each runner has finished flushing its running tasks
                for (int i = 0; i < _runners.Length; i++)
                    while (_runners[i].isStopping == true)
                        ThreadUtility.TakeItEasy();
            }

            isRunning = false;
        }

        public void Dispose()
        {
            if (Volatile.Read(ref _isDisposed) == true) return;

            Volatile.Write(ref _isDisposed, true);
            _disposingThreads = _runners.Length;
 
            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Kill(_decrementRunningThread);

            while (Volatile.Read(ref _disposingThreads) > 0)
                ThreadUtility.TakeItEasy();

            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Dispose();

            _runners            = null;
            onComplete          = null;
            _numberOfTasksAdded = 0;
            isRunning           = false;

            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            return _name;
        }

        ~MultiThreadedParallelTaskCollection()
        {
            Console.LogWarning(
                $"MultiThreadedParallelTaskCollection {_name} wasn't disposed of correctly. You forgot to call Dispose()");
            
            Dispose();
        }

        void InitializeThreadsAndData(uint numberOfThreads, bool tightTasks)
        {
            _runners = new MultiThreadRunner[numberOfThreads];

            //prepare a single multithread runner for each group of fiber like task collections
            //number of threads can be less than the number of tasks to run
            for (int i = 0; i < numberOfThreads; i++)
                _runners[i] = new MultiThreadRunner(
                    "MultiThreadedParallelRunner ".FastConcat(_name, " #").FastConcat(i), false, tightTasks);
        }

        bool RunMultiThreadParallelTasks()
        {
            if (_isDisposed == true)
                throw new MultiThreadedParallelTaskCollectionException(
                    "can't run a MultiThreadedParallelTaskCollection once killed");

            if (isRunning == false)
            {
                if (_parallelTasks.Count == 0) return false;

                isRunning = true;
                Volatile.Write(ref _counter, _parallelTasks.Count);

                //start them
                for (int i = 0; i < _parallelTasks.Count; i++)
                {
                    var runner = _runners[i % _runners.Length];
                    IEnumerator<TaskContract> wrapper = Wrap(_parallelTasks[i]);
                    wrapper.RunOn( runner);
                }
            }

            //wait for completion, I am not using signaling as this Collection could be yielded by a main thread runner
            return Volatile.Read(ref _counter) > 0;
        }

        IEnumerator<TaskContract> Wrap(TTask task)
        {
            try
            {
                while (task.MoveNext())
                    yield return task.Current;
            }
            finally
            {
                DecrementConcurrentOperationsCounter();
            }
        }

        void DecrementRunningThread()
        {
            Interlocked.Decrement(ref _disposingThreads);
        }

        void DecrementConcurrentOperationsCounter()
        {
            Interlocked.Decrement(ref _counter);
        }

        protected MultiThreadRunner[] _runners;
        readonly List<TTask>  _parallelTasks = new List<TTask>();

        int  _numberOfTasksAdded;
        int  _counter;
        int  _disposingThreads;
        bool _isDisposed;

        readonly string _name;
        readonly Action _decrementRunningThread;
    }
}

namespace Svelto.Tasks.Parallelism.ExtraLean
{
    public class MultiThreadedParallelTaskCollection<TTask> : IEnumerator, IDisposable
        where TTask : IEnumerator
    {
        public event Action onComplete;

        public object Current
        {
            get => throw new NotImplementedException();
        }

        public bool isRunning { private set; get; }

        /// <summary>
        ///  
        /// </summary>
        /// <param name="numberOfThreads"></param>
        /// <param name="tightTasks">
        /// if several cache friendly and optimized tasks run in parallel, using tightTasks may improve parallelism
        /// as gives the chance to other threads to run.
        /// </param>
        public MultiThreadedParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks)
        {
            _decrementRunningThread = DecrementRunningThread;
            DBC.Tasks.Check.Require(numberOfThreads > 0, "doesn't make much sense to use this with 0 threads");

            _name = name;

            InitializeThreadsAndData(numberOfThreads, tightTasks);
        }
        
        public MultiThreadedParallelTaskCollection(string name, bool tightTasks):this(name, (uint)Math.Max(1, Environment.ProcessorCount - 2), tightTasks)
        { }

        /// <summary>
        /// Add can be called by another thread, so if the collection is already running
        /// I can't allow adding more tasks.
        /// </summary>
        /// <param name="enumerator"></param>
        /// <exception cref="MultiThreadedParallelTaskCollectionException"></exception>
        public void Add(TTask enumerator)
        {
            if (isRunning == true)
                throw new MultiThreadedParallelTaskCollectionException(
                    "can't add tasks on a started MultiThreadedParallelTaskCollection");

            _parallelTasks.Add(enumerator);
            _numberOfTasksAdded++;
        }

        public bool MoveNext()
        {
            //isDisposed can be set by the GC finalizer thread
            if (Volatile.Read(ref _isDisposed)) return false;

            if (RunMultiThreadParallelTasks()) return true;

            if (onComplete != null)
                onComplete();

            isRunning = false;

            return false;
        }

        public void Reset()
        {
            Stop(0);

            _numberOfTasksAdded = 0;
            _parallelTasks.Clear();
            isRunning = false;
        }

        public void Stop()
        {
            Stop(0);
        }

        public void Stop(int msTimeout)
        {
            if (isRunning == false)
                return;

            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Stop();

            if (msTimeout > 0)
            {
                var then = DateTime.UtcNow.AddMilliseconds(msTimeout);

                //wait until each runner has finished flushing its running tasks
                for (int i = 0; i < _runners.Length; i++)
                    while (_runners[i].isStopping == true)
                    {
                        if (DateTime.UtcNow > then)
                            throw new SveltoTaskException("MultiThreadedParallelTaskCollection.Stop() timed out, increase time out or check if it got stuck");

                        ThreadUtility.TakeItEasy();
                    }
            }
            else
            {
                //wait until each runner has finished flushing its running tasks
                for (int i = 0; i < _runners.Length; i++)
                    while (_runners[i].isStopping == true)
                        ThreadUtility.TakeItEasy();
            }

            isRunning = false;
        }

        public void Dispose()
        {
            if (Volatile.Read(ref _isDisposed) == true) return;

            Volatile.Write(ref _isDisposed, true);
            _disposingThreads = _runners.Length;
 
            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Kill(_decrementRunningThread);

            while (Volatile.Read(ref _disposingThreads) > 0)
                ThreadUtility.TakeItEasy();

            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Dispose();

            _runners            = null;
            onComplete          = null;
            _numberOfTasksAdded = 0;
            isRunning           = false;

            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            return _name;
        }

        ~MultiThreadedParallelTaskCollection()
        {
            Console.LogWarning(
                $"MultiThreadedParallelTaskCollection {_name} wasn't disposed of correctly. You forgot to call Dispose()");
            
            Dispose();
        }

        void InitializeThreadsAndData(uint numberOfThreads, bool tightTasks)
        {
            _runners       = new MultiThreadRunner[numberOfThreads];

            //prepare a single multithread runner for each group of fiber like task collections
            //number of threads can be less than the number of tasks to run
            for (int i = 0; i < numberOfThreads; i++)
                _runners[i] = new MultiThreadRunner(
                    "MultiThreadedParallelRunner ".FastConcat(_name, " #").FastConcat(i), false, tightTasks);
        }

        bool RunMultiThreadParallelTasks()
        {
            if (_isDisposed == true)
                throw new MultiThreadedParallelTaskCollectionException(
                    "can't run a MultiThreadedParallelTaskCollection once killed");

            if (isRunning == false)
            {
                if (_parallelTasks.Count == 0) return false;

                isRunning = true;
                Volatile.Write(ref _counter, _parallelTasks.Count);

                //start them
                for (int i = 0; i < _parallelTasks.Count; i++)
                {
                    MultiThreadRunner runner = _runners[i % _runners.Length];
                    IEnumerator wrapper = Wrap(_parallelTasks[i]);
                    wrapper.RunOn( runner);
                }
            }

            //wait for completion, I am not using signaling as this Collection could be yielded by a main thread runner
            return Volatile.Read(ref _counter) > 0;
        }

        IEnumerator Wrap(TTask task)
        {
            try
            {
                while (task.MoveNext())
                    yield return task.Current;
            }
            finally
            {
                DecrementConcurrentOperationsCounter();
            }
        }

        void DecrementRunningThread()
        {
            Interlocked.Decrement(ref _disposingThreads);
        }

        void DecrementConcurrentOperationsCounter()
        {
            Interlocked.Decrement(ref _counter);
        }

        protected MultiThreadRunner[] _runners;
        readonly List<TTask>  _parallelTasks = new List<TTask>();

        int  _numberOfTasksAdded;
        int  _counter;
        int  _disposingThreads;
        bool _isDisposed;

        readonly string _name;
        readonly Action _decrementRunningThread;
    }
}
