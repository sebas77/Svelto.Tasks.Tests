using System.Collections;

namespace Svelto.Tasks.Parallelism.Internal
{
    public struct ParallelRunEnumerator<T> : IEnumerator where T:struct, ISveltoJob
    {
        public ParallelRunEnumerator(in T job, int startIndex, int numberOfIterations):this()
        {
            _startIndex = startIndex;
            _numberOfIterations = numberOfIterations;
            _job = job;
        }

        public bool MoveNext()
        {
            _endIndex = _startIndex + _numberOfIterations;

            Loop();

            return false;
        }

        void Loop()
        {
            for (_index = _startIndex; _index < _endIndex; _index++)
                _job.Update(_index);
        }

        public void Reset()
        {}

        public object Current => null;

        readonly int _startIndex;
        readonly int _numberOfIterations;
        readonly T _job;
        
        int _index;
        int _endIndex;
    }
}