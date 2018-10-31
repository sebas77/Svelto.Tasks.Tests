using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Unity;
using UnityEngine;

public class TestsThatCanRunOnlyInPlayMode
{
    [SetUp]
    public void Setup()
    {
        _iterable1 = new Enumerator(1000);
    }
    // A UnityTest behaves like a coroutine in PlayMode
    // and allows you to yield null to skip a frame in EditMode
    [UnityTest]
	public IEnumerator TestHandlingInstructionsToUnity() {
        // Use the Assert class to test conditions.
        // yield to skip a frame
        var task = UnityHandle().Run();

        while (task.MoveNext())
            yield return null;
    }

    IEnumerator UnityHandle()
    {
        DateTime now = DateTime.Now;

        yield return new WaitForSeconds(2);

        var seconds = (DateTime.Now - now).Seconds;

        Assert.That(seconds == 2);
    }

    [UnityTest]
    public IEnumerator TestCoroutineRunnerYieldOneFrame()
    {
        var enumerator = Continuation();
        var continuation = enumerator.Run();

        while (continuation.MoveNext() == true) yield return null;
        
        Assert.That(enumerator.Current, Is.EqualTo(100));
    }

    IEnumerator Continuation()
    {
        var result = false;
        var frame = Time.frameCount;
        int i = 0;
        while (++i < 100)
        {
            yield return null;

            if (frame == Time.frameCount)
                yield break;
            
            frame = Time.frameCount;
        }

        yield return i;
    }

    [UnityTest]
    public IEnumerator TestMultithreadIntervaled()
    {
        using (var runner = new MultiThreadRunner("intervalTest", 1))
        {
            DateTime now = DateTime.Now;

            var task = _iterable1.RunOnScheduler(runner);

            while (task.MoveNext())
                yield return null;

            var seconds = (DateTime.Now - now).Seconds;

            //10000 iteration * 1ms = 10 seconds

            Assert.That(_iterable1.AllRight == true && seconds == 1);
        }
    }
    
    [UnityTest, Timeout(1000)]
    public IEnumerator TestTightMultithread()
    {
        var iterable2 = new Enumerator(100);
        
        using (var runner = new MultiThreadRunner("tighttest"))
        {
            var taskroutine = TaskRunner.Instance.AllocateNewTaskRoutine().SetScheduler(runner).SetEnumerator(iterable2);
            yield return taskroutine.Start();

            Assert.That(true);
        }
    }
    
    [UnityTest]
    public IEnumerator TaskWithEnumeratorMustStopAndRestart()
    {
        var task = TaskRunner.Instance.AllocateNewTaskRoutine().SetScheduler(StandardSchedulers.updateScheduler).SetEnumerator
            (_iterable1);
            
        bool done = false;

        task.Start(onStop: () => {
            done = true;
        });

        while (task.isRunning == true)
        {
            yield return null;
            task.Stop();
        }
            
        Assert.That(done == true);
        Assert.IsFalse(_iterable1.AllRight);

        task.Start();

        while (task.isRunning == true) yield return null;
        
        //it's one because the Start run immediatly the code until the first yield 
        Assert.IsTrue(_iterable1.AllRight);
    }
    
    [UnityTest]
    public IEnumerator TestUnityWait()
    {
        ITaskRoutine taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine();
        taskRoutine.SetEnumeratorProvider(new WaitForSecondsU().GetEnumerator).SetScheduler(new UpdateMonoRunner("test"));
        taskRoutine.Start();
        DateTime then = DateTime.Now;
        while (taskRoutine.isRunning == true) yield return null;

        var totalSeconds = (DateTime.Now - then).TotalSeconds;
        Assert.That(totalSeconds, Is.InRange(1.0, 1.1));
    }
    
    [UnityTest]
    public IEnumerator TestUnityWaitInParallel()
    {
        ITaskRoutine taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine();
        ParallelTaskCollection pt = new ParallelTaskCollection();
        pt.Add(new WaitForSecondsU().GetEnumerator());
        pt.Add(new WaitForSecondsU().GetEnumerator());
        taskRoutine.SetEnumerator(pt).SetScheduler(new UpdateMonoRunner("test"));
        taskRoutine.Start();
        DateTime then = DateTime.Now;
        while (taskRoutine.isRunning == true) yield return null;

        var totalSeconds = (DateTime.Now - then).TotalSeconds;
        Assert.That(totalSeconds, Is.InRange(1.0, 1.1));
    }
    
    public class WaitForSecondsU : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new WaitForSecondsRealtime(1);
        }
    }

        
    [UnityTest]
    public IEnumerator TaskWithEnumeratorProviderMustStopAndRestart()
    {
        var task = TaskRunner.Instance.AllocateNewTaskRoutine().SetScheduler(StandardSchedulers.coroutineScheduler).
            SetEnumeratorProvider(() => SubEnumerator(1));
            
        bool done = false;

        task.Start(onStop: () => {
            done = true;
        });

        int iteration = 0;
            
        while (iteration++ < 3)
            yield return null;
            
        Assert.That(done == false);
            
        task.Stop();

        int iteration2 = 0;
            
        while (iteration2++ < 3)
            yield return null;
            
        Assert.That(done == true);

        task.Start();
        
        Assert.That(_hasReset == true);
    }
    
    [UnityTest]
    public IEnumerator TaskWithCompilerEnumeratorProviderCannotRestart()
    {
        var task = TaskRunner.Instance.AllocateNewTaskRoutine().SetScheduler(StandardSchedulers.coroutineScheduler).
            SetEnumerator(SubEnumerator(1));
            
        bool done = false;

        task.Start(onStop: () => {
            done = true;
        });

        int iteration = 0;
            
        while (iteration++ < 3)
            yield return null;
            
        Assert.That(done == false);
            
        task.Stop();

        int iteration2 = 0;
            
        while (iteration2++ < 3)
            yield return null;
            
        Assert.That(done == true); 

        Assert.Catch<Exception>(() => task.Start());
    }
    
    [UnityTest]
    public IEnumerator TaskWithCompilerEnumeratorProviderCanRestartIfEnumeratorIsSetAgain()
    {
        var task = TaskRunner.Instance.AllocateNewTaskRoutine().SetScheduler(StandardSchedulers.coroutineScheduler).
            SetEnumerator(SubEnumerator(1));
            
        bool done = false;

        task.Start(onStop: () => {
            done = true;
        });

        int iteration = 0;
            
        while (iteration++ < 3)
            yield return null;
            
        Assert.That(done == false);
            
        task.Stop();

        int iteration2 = 0;
            
        while (iteration2++ < 3)
            yield return null;
        
        Assert.That(done == true);

        task.SetEnumerator(SubEnumerator(1));

        task.Start();

        Assert.That(_hasReset == true);
    }

    class ValueRef
    {
        public bool isDone;
    }
    
    [UnityTest]
    public IEnumerator TaskWithWaitForSecondsMustRestartImmediately()
    {
        var valueRef = new ValueRef();
        var task = TaskRunner.Instance.AllocateNewTaskRoutine().SetScheduler(StandardSchedulers.coroutineScheduler).
            SetEnumeratorProvider(() => UnityWaitEnumerator(valueRef));
            
        bool stopped = false;
        
        DateTime time = DateTime.Now;
        
        task.Start(onStop: () => {
            stopped = true;
        });
        
        while (task.isRunning == true)
        {
            yield return null; //stop is called inside the runner
            task.Stop();
            yield return null; //stop is called inside the runner
        }
        var totalSeconds = (DateTime.Now - time).TotalSeconds;
        Assert.Less(totalSeconds, 0.2);
        Assert.That(stopped == true);
        Assert.That(valueRef.isDone == false);
        
        stopped = false;
        time = DateTime.Now;
        
        yield return task.Start(onStop: () => {
            stopped = true;
        });

        totalSeconds = (DateTime.Now - time).TotalSeconds;
        Assert.Greater(totalSeconds, 1.9);
        Assert.Less(totalSeconds, 2.1);
        Assert.That(valueRef.isDone == true);
        Assert.That(stopped == false);
    }
    
/*    [UnityTest]
    public IEnumerator TaskWithWaitForSecondsMustRestartImmediatlyInParallelToo()
    {
        ParallelTaskCollection tasks = new ParallelTaskCollection();
        var valueRef = new ValueRef();
        var valueRef2 = new ValueRef();
        tasks.Add(SubEnumerator(1));
        tasks.Add(UnityWaitEnumerator(valueRef));
        tasks.Add(UnityWaitEnumerator(valueRef2));
        tasks.Add(SubEnumerator(1));
            
        var task = TaskRunner.Instance.AllocateNewTaskRoutine().SetScheduler(StandardSchedulers.coroutineScheduler).
            SetEnumerator(tasks);
            
        bool stopped = false;

        task.Start(onStop: () => {
            stopped = true;
        });
        
        DateTime time = DateTime.Now;
        
        while (task.isRunning == true)
        {
            yield return null; 
            task.Stop();
            yield return null; //stop is called inside the runner
        }
        
        var totalSeconds = (DateTime.Now - time).TotalSeconds;
       // Assert.Less(totalSeconds, 0.2);
        Assert.That(stopped == true);
        Assert.That(valueRef.isDone == false);
        Assert.That(valueRef2.isDone == false);
        
        tasks = new ParallelTaskCollection();
        tasks.Add(UnityWaitEnumerator(new ValueRef()));
        task.SetEnumerator(tasks);
        
        stopped = false;
        time = DateTime.Now;
        
        yield return task.Start(onStop: () => {
            stopped = true;
        });
        
        totalSeconds = (DateTime.Now - time).TotalSeconds;
        Assert.Greater(totalSeconds, 1.9);
        Assert.Less(totalSeconds, 2.1);
        Assert.That(stopped == false);
        Assert.That(valueRef.isDone == true);
        Assert.That(valueRef2.isDone == true);
    }*/
    
    class Token
    {
        public int count;
    }

    [UnityTest] 
    public IEnumerator TestStopMultiThreadParallelTask()
    {
        var test = new MultiThreadedParallelTaskCollection(4, false);

        Token token = new Token();
        bool done = false;
        test.onComplete += () => done = true;
        test.Add(new WaitEnumerator(token));
        test.Add(new WaitEnumerator(token));
        test.Add(new WaitEnumerator(token));
        test.Add(new WaitEnumerator(token));

        test.Run();

        yield return new WaitForSeconds(0.5f); 
        
        test.Stop();
        
        Assert.That(done, Is.False);
        Assert.AreEqual(token.count, 0);
    }
    
    [UnityTest]  
    public IEnumerator TestMultiThreadParallelTaskReset()
    {
        var test = new MultiThreadedParallelTaskCollection(4, false);
        
        Token token = new Token();

        int done = 0;
        test.onComplete += () => done++;
        test.Add(new WaitEnumerator(token));
        test.Add(new WaitEnumerator(token));
        test.Add(new WaitEnumerator(token));
        test.Add(new WaitEnumerator(token));
        
        test.Run();
        
        yield return new WaitForSeconds(0.5f);

        token.count = 3;
        
        test.Stop();
        
        test.Complete();
        
        Assert.That(done == 1);
        Assert.AreEqual(4, token.count);
    }

    class WaitEnumerator:IEnumerator
    {
        Token _token;

        public WaitEnumerator(Token token)
        {
            _token  = token;
            _future = DateTime.UtcNow.AddSeconds(2);
        }
        
        public void Reset()
        {
            _future      = DateTime.UtcNow.AddSeconds(2);
            _token.count = 0;
        }

        public object Current { get { return null; } }

        DateTime _future;

        public bool MoveNext()
        {
            if (_future <= DateTime.UtcNow)
            {
                Interlocked.Increment(ref _token.count);
        
                return false;
            }

            return true;
        }
    }
    
    IEnumerator UnityWaitEnumerator(ValueRef valueRef)
    {
        yield return new WaitForSeconds(2);

        valueRef.isDone = true;
    }

    IEnumerator SubEnumerator(int i)
    {
        _hasReset = true;
        var count = i + 10;
        while (++i < count)
        {
            yield return null; //enable asynchronous execution
            _hasReset = false;
        }

        yield return i; //careful it will be boxed;
    }
    
    Enumerator _iterable1;
    bool _hasReset;

    class Enumerator : IEnumerator
    {
        public bool AllRight
        {
            get
            {
                return iterations == totalIterations;
            }
        }

        public Enumerator(int niterations)
        {
            iterations = 0;
            totalIterations = niterations;
        }

        public bool MoveNext()
        {
            if (iterations < totalIterations)
            {
                iterations++;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            iterations = 0;
        }

        public object Current { get; private set; }

        readonly int totalIterations;
        public int iterations;
    }
}
