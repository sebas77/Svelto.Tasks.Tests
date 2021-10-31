using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks
{
    namespace Lean
    {
        public class SteppableRunner : BaseRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
        {
            public SteppableRunner(string name) : base(name)
            { }
        }
    }

    namespace ExtraLean
    {
        public class SteppableRunner : BaseRunner<ExtraLeanSveltoTask<IEnumerator>>
        {
            public SteppableRunner(string name) : base(name)
            { }
        }
    }
}