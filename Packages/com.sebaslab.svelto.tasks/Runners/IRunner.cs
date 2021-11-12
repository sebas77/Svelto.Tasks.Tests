using System;

namespace Svelto.Tasks
{
    interface ISteppableRunner: IRunner
    {
        void Step();
        bool hasTasks { get; }
    }
    
    public interface IRunner: IDisposable
    {
        bool isStopping { get; }
        //bool isKilled   { get; }

        void Pause();
        void Resume();
        void Stop();
        void Flush();

        uint numberOfRunningTasks { get; }
        uint numberOfQueuedTasks  { get; }
        uint numberOfProcessingTasks { get; }
    }

    public interface IRunner<T>: IRunner where T:ISveltoTask
    {
        void StartTask(in T task);
        void SpawnContinuingTask(in T task);
    }
}
