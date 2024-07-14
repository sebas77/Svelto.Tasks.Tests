using System;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks
{
    //ISveltoTask is not an enumerator just to avoid ambiguity and understand responsibilities in the other classes
    public interface ISveltoTask
    {
        StepState Step();

        void Stop();
        
        bool isCompleted { get; }
        
        string name { get; }
    }
    
    [Flags]
    public enum StepState
    {
        Running,
        Completed,
        Waiting,
        Faulted
    }
}

