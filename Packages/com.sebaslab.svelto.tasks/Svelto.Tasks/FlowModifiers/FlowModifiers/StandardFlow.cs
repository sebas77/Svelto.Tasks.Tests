using Svelto.DataStructures;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.FlowModifiers
{
    public struct StandardFlow:IFlowModifier
    {
        public bool CanMoveNext<T>(ref TombstoneHandle nextIndex, int coroutinesCount, bool hasCoroutineCompleted) where T:ISveltoTask
        {
            return true;
        }

        public bool CanProcessThis(ref TombstoneHandle index)
        {
            return true;
        }

        public void Reset()
        {}
    }
}