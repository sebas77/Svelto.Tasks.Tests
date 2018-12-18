using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Unity;
using UnityEngine;

class Enumerator : IEnumerator<TaskContract?>
    {
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

        TaskContract? IEnumerator<TaskContract?>.Current => null;

        public object Current { get; private set; }

        readonly int totalIterations;
        public   int iterations;
        public void Dispose()
        {}
    }

/// <summary>
/// This is just for testing purpose, you should never
/// yield YieldInstrucitons as they are inefficient and they work only with the CoroutineMonoRunner
/// </summary>
public class WaitForSecondsUnity : IEnumerable<TaskContract?>
{
    public IEnumerator<TaskContract?> GetEnumerator()
    {
        yield return new YieldInstructionEnumerator(new WaitForSeconds(1)).Continue();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

    class Token
    {
        public int count;
    }

class ValueRef
{
    public bool isDone;
}
