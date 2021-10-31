using System;
using Svelto.Tasks;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Lean;
using Svelto.Utilities;

namespace Svelto.Tasks.ExtraLean
{
    public static class TaskRunnerExtensions
    {
        public static void RunOn<TTask, TRunner>(this TTask enumerator, TRunner runner)
            where TTask : IEnumerator where TRunner : class, IRunner<ExtraLeanSveltoTask<TTask>>
        {
            new ExtraLeanSveltoTask<TTask>().Run(runner, ref enumerator);
        }
        
        public static void RunOn<TRunner>(this IEnumerator enumerator, TRunner runner)
            where TRunner : class, IRunner<ExtraLeanSveltoTask<IEnumerator>>
        {
            new ExtraLeanSveltoTask<IEnumerator>().Run(runner, ref enumerator);
        }
    }
}

namespace Svelto.Tasks.Lean
{
    public static class TaskRunnerExtensions
    {
        public static Continuation RunOn<TTask, TRunner>(this TTask enumerator, TRunner runner)
            where TTask : struct, IEnumerator<TaskContract> where TRunner : class, IRunner<LeanSveltoTask<TTask>>
        {
            return new LeanSveltoTask<TTask>().Run(runner, ref enumerator);
        }
        
        public static Continuation RunOn<TRunner>(this IEnumerator<TaskContract> enumerator, TRunner runner)
            where TRunner : class, IRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
        {
            return new LeanSveltoTask<IEnumerator<TaskContract>>().Run(runner, ref enumerator);
        }
    }
}

public static class TaskRunnerExtensions
{
    public static TaskContract Continue(this IEnumerator<TaskContract> task) 
    {
        return new TaskContract(task);
    }
    
    public static TaskContract Continue<T>(this T enumerator) where T:class, IEnumerator 
    {
        return new TaskContract(enumerator);
    }
    
    public static void Complete(this IEnumerator<TaskContract> task, int _timeout = 0) 
    {
        var syncRunnerValue = LocalSyncRunners<IEnumerator<TaskContract>>.syncRunner.Value;
        task.RunOn(syncRunnerValue);
        syncRunnerValue.ForceComplete(_timeout);
    }

    public static void Complete<T>(this T enumerator, int _timeout = 0) where T:IEnumerator 
    {
        var quickIterations = 0;

        if (_timeout > 0)
        {
            var then  = DateTime.Now.AddMilliseconds(_timeout);
            var valid = true;

            while (enumerator.MoveNext() &&
                   (valid = DateTime.Now < then)) ThreadUtility.Wait(ref quickIterations);

            if (valid == false)
                throw new Exception("synchronous task timed out, increase time out or check if it got stuck");
        }
        else
        {
            if (_timeout == 0)
                while (enumerator.MoveNext())
                    ThreadUtility.Wait(ref quickIterations);
            else
                while (enumerator.MoveNext());
        }
    }
    
    public static void Complete(this Continuation enumerator, int _timeout = 1000)
    {
        var quickIterations = 0;

        if (_timeout > 0)
        {
            var then  = DateTime.Now.AddMilliseconds(_timeout);
            var valid = true;

            while (enumerator.isRunning &&
                   (valid = DateTime.Now < then)) ThreadUtility.Wait(ref quickIterations);

            if (valid == false)
                throw new Exception("synchronous task timed out, increase time out or check if it got stuck");
        }
        else
        {
            if (_timeout == 0)
                while (enumerator.isRunning)
                    ThreadUtility.Wait(ref quickIterations);
            else
                while (enumerator.isRunning);
        }
    }
}

