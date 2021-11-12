using Svelto.Tasks.Internal;

namespace Svelto.Tasks.FlowModifiers
{
    /// <summary>
    /// SerialFlow guarantees that the task running run in serial, but the order of execution is not guaranteed
    /// (they won't run in the order they are added in the runner).
    /// </summary>
    public struct SerialFlow : IFlowModifier
    {
        public bool CanMoveNext<T>(ref int nextIndex, ref T currentResult, int coroutinesCount, bool hasCoroutineComplete) where T:ISveltoTask
        {
            if (hasCoroutineComplete == false)
                nextIndex--; //stay on the current task until it's done
   
            return false;
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