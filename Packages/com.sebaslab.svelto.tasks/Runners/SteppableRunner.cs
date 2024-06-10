using System;
using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks
{
    /// Generic Steppable Runners. They can be used to run tasks that can be stepped manually.
    namespace Lean
    {
        public class SteppableRunner : SteppableRunner<LeanSveltoTask<IEnumerator<TaskContract>>>,
                IEnumerator<TaskContract>, IGenericLeanRunner
        {
            public SteppableRunner(string name) : base(name)
            {
            }

            public bool MoveNext()
            {
                return Step();
            }

            public void Reset()
            {
            }

            public TaskContract Current => TaskContract.Yield.It;

            object IEnumerator.Current => throw new NotImplementedException();
        }
    }

    namespace ExtraLean
    {
        public class SteppableRunner : SteppableRunner<ExtraLeanSveltoTask<IEnumerator>>, IEnumerator,IGenericExtraLeanRunner
        {
            public SteppableRunner(string name) : base(name)
            {
            }

            public bool MoveNext()
            {
                return Step();
            }

            public void Reset()
            {
            }

            public object Current => TaskContract.Yield.It;
        }
    }
}