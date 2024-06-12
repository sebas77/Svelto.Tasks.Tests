using System;
using System.Collections.Generic;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks
{
    public interface IRunner : IDisposable
    {
    }

    public interface ISteppableRunner : IRunner
    {
        bool Step();
        bool hasTasks { get; }
    }

    public interface IRunner<T> : IRunner where T : ISveltoTask
    {
        void StartTask(in T task);
        void SpawnContinuingTask(in T task);
    }
}