namespace Svelto.Tasks
{
#if TO_IMPLEMENT_PROPERLY
    public interface ITaskProgress
    {
        float		progress { get; }
    }
#endif
    public interface IServiceTask
    {
        bool isDone { get; }
        
        IEnumerator<TaskContract> Execute();	
    }

    public interface IServiceTaskExceptionHandler
    {
        Exception   throwException { get; }
    }
}

