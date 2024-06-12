using Svelto.Common;

namespace Svelto.Tasks.Internal
{
    public interface IProcessSveltoTasks<TTask> where TTask : ISveltoTask
    {
        bool MoveNext<PlatformProfiler>(in PlatformProfiler platformProfiler)
            where PlatformProfiler : IPlatformProfiler;

        uint numberOfRunningTasks { get; }
        uint numberOfQueuedTasks { get; }
        uint numberOfTasks { get; }

        void StartTask(in TTask task);
        void EnqueueContinuingTask(in TTask task);
    }
}