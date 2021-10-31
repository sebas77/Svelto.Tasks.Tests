using Svelto.Tasks.Internal;

namespace Svelto.Tasks.FlowModifiers
{
    public struct SerialFlow : IFlowModifier
    {
        public bool CanMoveNext<T>(ref int nextIndex, ref T currentResult, int coroutinesCount, bool hasCoroutineComplete) where T:ISveltoTask
        {
            if (hasCoroutineComplete == false)
                nextIndex--; //stay on the current task until it's done
   
            return true;
        }
   
        public bool CanProcessThis(ref int index)
        {
            return true;
        }
   
        public void Reset()
        {
        }
   
        public string runnerName { get; set; }
   }
}