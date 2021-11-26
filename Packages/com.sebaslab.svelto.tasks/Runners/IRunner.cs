using System;

namespace Svelto.Tasks
{
    public interface IRunner: IDisposable
    {
        bool isStopping { get; }
        //bool isKilled   { get; }

        void Pause();
        void Resume();
        void Stop();
        void Flush();

        uint numberOfRunningTasks    { get; }
        uint numberOfQueuedTasks     { get; }
        uint numberOfProcessingTasks { get; }
    }
    
    interface ISteppableRunner: IRunner
    {
        void Step();
        bool hasTasks { get; }
    }
    
    public interface IRunner<T>: IRunner where T:ISveltoTask
    {
        void  StartTask(in T task);
        ref T SpawnContinuingTask(T task);
    }
}
