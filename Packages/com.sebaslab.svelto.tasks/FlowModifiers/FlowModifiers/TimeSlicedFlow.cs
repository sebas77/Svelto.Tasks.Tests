using System.Diagnostics;
using Svelto.Tasks.Internal;
using Svelto.Utilities;

namespace Svelto.Tasks.FlowModifiers
{
    public struct TimeSlicedFlow : IFlowModifier
    {
        public TimeSlicedFlow(uint maxMilliseconds)
        {
            _maxMs = maxMilliseconds;
            _stopWatch = new Stopwatch();
        }

        public bool CanMoveNext<T>(ref int nextIndex, int coroutineCount, bool hasCoroutineCompleted) where T:ISveltoTask
        {
            //never stops until maxMilliseconds is elapsed or Break.AndResumeNextIteration is returned
            if (_stopWatch.ElapsedMilliseconds > _maxMs)
            {
                _stopWatch.Reset();
                _stopWatch.Start();

                return false;
            }

            if (nextIndex >= coroutineCount)
                nextIndex = 0; //restart iteration and continue

            return true;
        }


        public bool CanProcessThis(ref int index)
        {
            return true;
        }

        public void Reset()
        {
            _stopWatch.Reset();
            _stopWatch.Start();
        }

        readonly Stopwatch _stopWatch;
        readonly uint      _maxMs;
    }
}