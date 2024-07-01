using System.Collections.Generic;
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
    
    public enum StepState
    {
        Running,
        Completed,
        Waiting 
    }
}

