﻿using System.Collections;

    class Enumerator : IEnumerator
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

        public object Current { get; private set; }

        readonly int totalIterations;
        public   int iterations;
    }

    class Token
    {
        public int count;
    }

class ValueRef
{
    public bool isDone;
}
