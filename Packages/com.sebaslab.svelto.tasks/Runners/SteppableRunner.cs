using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks
{
    namespace Lean
    {
        public class SteppableRunner : SteppableRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
        {
            public SteppableRunner(string name) : base(name)
            { }
        }
    }

    namespace ExtraLean
    {
        public class SteppableRunner : SteppableRunner<ExtraLeanSveltoTask<IEnumerator>>
        {
            public SteppableRunner(string name) : base(name)
            { }
        }
    }
    
    public class SteppableRunner<T> : BaseRunner<T> where T : ISveltoTask
    {
        public SteppableRunner(string name) : base(name)
        { }
    }
}