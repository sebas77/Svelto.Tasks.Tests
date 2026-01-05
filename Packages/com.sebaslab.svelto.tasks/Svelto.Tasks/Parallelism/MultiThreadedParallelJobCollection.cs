using Svelto.Tasks.Parallelism.Internal;

namespace Svelto.Tasks.Parallelism;

/// <summary>
/// a ParallelTaskCollection ran by MultiThreadRunner will run the tasks in a single thread
/// MultiThreadParallelTaskCollection enables parallel tasks to run on different threads
/// </summary>
///

public class
        MultiThreadedParallelJobCollection<TJob> : ExtraLean.MultiThreadedParallelTaskCollection<ParallelRunEnumerator<TJob>>
        where TJob : struct, ISveltoJob
{
    public void Add(ref TJob job, int iterations)
    {
        if (isRunning == true)
            throw new MultiThreadedParallelTaskCollectionException(
                "can't add tasks on a started MultiThreadedParallelTaskCollection");

        var runnersLength   = _runners.Length;
        int tasksPerThread     = (int)System.MathF.Floor((float)iterations / runnersLength);
        int reminder           = iterations % runnersLength;

        for (int i = 0; i < runnersLength; i++)
            Add(new ParallelRunEnumerator<TJob>(ref job, tasksPerThread * i, tasksPerThread));

        if (reminder > 0)
            Add(new ParallelRunEnumerator<TJob>(ref job, tasksPerThread * runnersLength, reminder));
    }

    public MultiThreadedParallelJobCollection(string name, uint numberOfThreads, bool tightTasks) : base(name,
        numberOfThreads, tightTasks)
    {
    }
}