using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System;
using Svelto.Tasks;

public class TestsThatCanRunOnlyInPlayMode
{
    [SetUp]
    public void Setup()
    {
        iterable1 = new Enumerable(1000);
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

        yield return new UnityEngine.WaitForSeconds(2);

        var seconds = (DateTime.Now - now).Seconds;

        Assert.That(seconds == 2);
    }

    [UnityTest]
    public IEnumerator TestMultithreadIntervaled()
    {
        using (var runner = new MultiThreadRunner("intervalTest", 1))
        {
            DateTime now = DateTime.Now;

            var task = iterable1.ThreadSafeRunOnSchedule(runner);

            while (task.MoveNext())
                yield return null;

            var seconds = (DateTime.Now - now).Seconds;

            //10000 iteration * 1ms = 10 seconds

            Assert.That(iterable1.AllRight == true && seconds == 1);
        }
    }
    
    [UnityTest]
    [Timeout(1000)]
    public IEnumerator TestTightMultithread()
    {
        var iterable2 = new Enumerable(100);
        
        using (var runner = new MultiThreadRunner("tighttest"))
        {
            var taskroutine = iterable1.PrepareTaskRoutineOnSchedule(runner);
            
            yield return taskroutine.Start();

            taskroutine.SetEnumerator(iterable2);
            yield return taskroutine.Start();

            Assert.That(true);
        }
    }

    Enumerable iterable1;

    class Enumerable : IEnumerator
    {
        public bool AllRight
        {
            get
            {
                return iterations == totalIterations;
            }
        }

        public Enumerable(int niterations)
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
        int iterations;
    }
}
