#if later
using Svelto.Tasks.Chain;

namespace Test
{
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
}
#endif