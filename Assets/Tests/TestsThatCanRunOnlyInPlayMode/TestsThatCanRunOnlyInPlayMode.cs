﻿using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Parallelism;
using Svelto.Tasks.Unity;
using UnityEngine;

[TestFixture]
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
	public IEnumerator TestHandlingInstructionsToUnity() 
    {
        var task = UnityHandle().Run();

        while ((task as IEnumerator).MoveNext())
            yield return null;
    }

    [UnityTest]
    public IEnumerator TestCoroutineRunnerYieldOneFrame()
    {
        var enumerator = Continuation();
        var continuation = enumerator.Run();

        while ((continuation as IEnumerator).MoveNext() == true) yield return null;
        
        Assert.That(enumerator.Current, Is.EqualTo(100));
    }
    
    [UnityTest]
    public IEnumerator TestMultithreadIntervaled()
    {
        using (var runner = new MultiThreadRunner("intervalTest", 1))
        {
            DateTime now = DateTime.Now;

            var task = _iterable1.RunOnScheduler(runner);

            while ((task as IEnumerator).MoveNext())
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
            var taskroutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
                                        taskroutine.SetEnumerator(iterable2);
            yield return taskroutine.Start();

            Assert.That(true);
        }
    }
    
    [UnityTest]
    public IEnumerator TaskWithEnumeratorMustStopAndRestart()
    {
        var task = TaskRunner.Instance.AllocateNewTaskRoutine(StandardSchedulers.updateScheduler);
        task.SetEnumerator(_iterable1);
            
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
        var coroutineMonoRunner = new CoroutineMonoRunner("test1");
        ITaskRoutine<IEnumerator> taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(coroutineMonoRunner);
        taskRoutine.SetEnumeratorProvider(new WaitForSecondsUnity().GetEnumerator);

        
        taskRoutine.Start();
        DateTime then = DateTime.Now;
        while (taskRoutine.isRunning == true) yield return null;
        coroutineMonoRunner.Dispose();
        var totalSeconds = (DateTime.Now - then).TotalSeconds;
        Assert.That(totalSeconds, Is.InRange(0.9, 1.1));
    }
    
    [UnityTest]
    public IEnumerator TestUnityWaitInParallel()
    {
        var updateMonoRunner = new UpdateMonoRunner("test1");
        ITaskRoutine<IEnumerator> taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(updateMonoRunner);
        ParallelTaskCollection pt = new ParallelTaskCollection();
        pt.Add(new WaitForSecondsUnity().GetEnumerator());
        pt.Add(new WaitForSecondsUnity().GetEnumerator());
        taskRoutine.SetEnumerator(pt);
        taskRoutine.Start();
        DateTime then = DateTime.Now;
        while (taskRoutine.isRunning == true) yield return null;
        updateMonoRunner.Dispose();
        var totalSeconds = (DateTime.Now - then).TotalSeconds;
        Assert.That(totalSeconds, Is.InRange(0.9, 1.1));
    }
    
    /// <summary>
    /// This is just for testing purpose, you should never
    /// yield YieldInstrucitons as they are inefficient and they work only with the CoroutineMonoRunner
    /// </summary>
    public class WaitForSecondsUnity : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new YieldInstructionEnumerator(new WaitForSeconds(1));
        }
    }
        
    [UnityTest]
    public IEnumerator TaskWithEnumeratorProviderMustStopAndRestart()
    {
        var task = TaskRunner.Instance.AllocateNewTaskRoutine(StandardSchedulers.coroutineScheduler);
            task.
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
        var task = TaskRunner.Instance.AllocateNewTaskRoutine(StandardSchedulers.coroutineScheduler);
        task.
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
        var task = TaskRunner.Instance.AllocateNewTaskRoutine(StandardSchedulers.coroutineScheduler);
        task.
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
    
    [Test]
    public void TestCoroutineMonoRunnerStartsTheFirstIterationImmediately()
    {
        var testFirstInstruction = TestFirstInstruction();
        var runner               = new CoroutineMonoRunner("test4");
        testFirstInstruction.RunOnScheduler(runner);
        runner.Dispose();
            
        Assert.That(testFirstInstruction.Current, Is.EqualTo(1));
    }
    
    static IEnumerator TestFirstInstruction()
    {
        yield return 1;
    }


    [UnityTest]
    public IEnumerator TaskWithUnityYieldInstructionMustRestartImmediately()
    {
        var valueRef = new ValueRef();
        using (var runner = new CoroutineMonoRunner("test3"))
        {
            var task = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
                task
                                 .SetEnumeratorProvider(() => UnityWaitEnumerator(valueRef));

            bool stopped = false;

            DateTime time = DateTime.Now;

            task.Start(onStop: () => { stopped = true; });

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
            time    = DateTime.Now;

            yield return task.Start(onStop: () => { stopped = true; });

            totalSeconds = (DateTime.Now - time).TotalSeconds;
            Assert.Greater(totalSeconds, 1.9);
            Assert.Less(totalSeconds, 2.1);
            Assert.That(valueRef.isDone == true);
            Assert.That(stopped == false);
        }
    }
    
    [UnityTest] 
    public IEnumerator TestStopMultiThreadParallelTask()
    {
        var test = new MultiThreadedParallelTaskCollection("test", 4, false);

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
        test.Dispose();
        
        Assert.That(done, Is.False);
        Assert.AreEqual(token.count, 0);
    }
    
    [UnityTest]  
    public IEnumerator TestMultiThreadParallelTaskReset()
    {
        var test = new MultiThreadedParallelTaskCollection("test",4, false);
        
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
        test.Dispose();
        
        Assert.That(done == 1);
        Assert.AreEqual(4, token.count);
    }
    
    [UnityTest]
    public IEnumerator TestSimpleTaskRoutineStartStart()
    {
        yield return null;
            
        using (var runner = new UpdateMonoRunner("TestSimpleTaskRoutineStartStart"))
        {
            var taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
                                         taskRoutine.SetEnumeratorProvider(() => LongTermEnumerator());
            taskRoutine.Start();
            yield return null;
            taskRoutine.Start();
            taskRoutine.Start();
            taskRoutine.Start();
            var continuator = taskRoutine.Start();
                
            while ((continuator as IEnumerator).MoveNext()) yield return null;
            
            Assert.Pass();
        }
    }
    
    [UnityTest]
    public IEnumerator TestSimpleTaskRoutineStopStart()
    {
        yield return null;
            
        using (var runner = new UpdateMonoRunner("TestSimpleTaskRoutineStartStart"))
        {
            var taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
            taskRoutine.
                                         SetEnumeratorProvider(() => LongTermEnumerator());
            int test = 0;
            var continuator = taskRoutine.Start(onStop:() => OnStop(ref test));
            yield return null;
            taskRoutine.Stop();
            while ((continuator as IEnumerator).MoveNext()) yield return null;
            var continuator2 = taskRoutine.Start();
            Assert.That(test, Is.EqualTo(1));
            while ((continuator2 as IEnumerator).MoveNext()) yield return null;
            
            Assert.Pass();
        }
    }
    
     
    [UnityTest]
    public IEnumerator TestSimpleTaskRoutineStartStartUnity()
    {
        yield return null;
            
        using (var runner = new UpdateMonoRunner("TestSimpleTaskRoutineStartStart"))
        {
            var taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
            taskRoutine.
                                         SetEnumeratorProvider(() => LongTermEnumeratorUnity());
            taskRoutine.Start();
            taskRoutine.Start();
            taskRoutine.Start();
            taskRoutine.Start();
            var continuator = taskRoutine.Start();
                
            while ((continuator as IEnumerator).MoveNext()) yield return null;
            
            Assert.Pass();
        }
    }
    
    [UnityTest]
    public IEnumerator TestSimpleTaskRoutineStopStartUnity()
    {
        yield return null;
            
        using (var runner = new UpdateMonoRunner("TestSimpleTaskRoutineStartStart"))
        {
            var taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
            taskRoutine.
                                         SetEnumeratorProvider(() => LongTermEnumeratorUnity());
            int test        = 0;
            var continuator = taskRoutine.Start(onStop:() => OnStop(ref test));
            yield return null;
            taskRoutine.Stop();
            while ((continuator as IEnumerator).MoveNext()) yield return null;
            var continuator2 = taskRoutine.Start();
            Assert.That(test, Is.EqualTo(1));
            while ((continuator2 as IEnumerator).MoveNext()) yield return null;
            
            Assert.Pass();
        }
    }

    [UnityTest]
    public IEnumerator TestSimpleTaskRoutineStart()
    {
        yield return null;
            
        using (var runner = new CoroutineMonoRunner("TestSimpleTaskRoutineStartStart"))
        {
            ValueObject result = new ValueObject();

            var taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
            taskRoutine.
                                         SetEnumeratorProvider(() => SimpleEnumerator(result));

            var continuation = taskRoutine.Start();
                
            while ((continuation as IEnumerator).MoveNext()) yield return null;

            Assert.That(result.counter, Is.EqualTo(1));
        }
    }
    
     
    IEnumerator LongTermEnumerator()
    {
        int frame;
        int counter = 0;
        while (counter++ < 3)
        {
            frame = Time.frameCount;
            yield return null;
            if (frame == Time.frameCount)
                throw new Exception();
        }
    }
    
    IEnumerator LongTermEnumeratorUnity()
    {
        int frame;
        int counter = 0;
        while (counter++ < 3)
        {
            frame = Time.frameCount;
            yield return new YieldInstructionEnumerator(new WaitForEndOfFrame());
            if (frame == Time.frameCount)
                throw new Exception();
        }
    }
    
    IEnumerator UnityHandle()
    {
        DateTime now = DateTime.Now;

        yield return new YieldInstructionEnumerator(new WaitForSeconds(2));

        var seconds = (DateTime.Now - now).Seconds;

        Assert.That(seconds, Is.InRange(1.9, 2.1));
    }

    IEnumerator Continuation()
    {
        var frame = Time.frameCount;
        int i     = 0;
        while (++i < 100)
        {
            yield return null;

            if (frame == Time.frameCount)
                throw new Exception();
            
            frame = Time.frameCount;
        }

        yield return i;
    }
    
    IEnumerator SimpleEnumerator(ValueObject result)
    {
        var frame = Time.frameCount;
        yield return null;
        if (frame == Time.frameCount)
            throw new Exception();
        
        Interlocked.Increment(ref result.counter);
    }
    
    class ValueObject
    {
        public int counter;
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
        yield return new YieldInstructionEnumerator(new WaitForSeconds(2));

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
    
    void OnStop(ref int test)
    {
        test = 1;
    }
    
    Enumerator _iterable1;
    bool _hasReset;
}
