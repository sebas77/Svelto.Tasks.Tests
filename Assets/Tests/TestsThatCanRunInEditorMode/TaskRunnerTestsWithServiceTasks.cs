using System.Collections;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using UnityEngine.TestTools;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsWithServiceTasks
    {
        [SetUp]
        public void Setup()
        {
            _task1 = new ServiceTask();
            _task2 = new ServiceTask();
            
            _serialTasks1 = new SerialTaskCollection();
        }
        
        [UnityTest]
        public IEnumerator TestSingleITaskExecution()
        {
            yield return null;

            _task1.Execute();

            while (_task1.isDone == false) ;

            Assert.That(_task1.isDone);
        }
        
        [UnityTest]
        public IEnumerator TestSingleTaskExecutionCallsOnComplete()
        {
            yield return null;

            _task1.OnComplete(() => Assert.That(_task1.isDone, Is.True));

            _task1.Execute();

            while (_task1.isDone == false) ;
        }
        
        [UnityTest]
        public IEnumerator TestTask1IsExecutedBeforeTask2()
        {
            yield return null;

            bool test1Done = false;

            _task1.OnComplete(() => { test1Done = true; });
            _task2.OnComplete(() => { Assert.True(test1Done); });

            _serialTasks1.Add(new TaskServiceEnumerator(_task1));
            _serialTasks1.Add(new TaskServiceEnumerator(_task2));
            
            _serialTasks1.RunOnScheduler(new SyncRunner(3000));
        }

        ServiceTask          _task1;
        ServiceTask          _task2;
        SerialTaskCollection _serialTasks1;
        ITaskRoutine<IEnumerator>         _reusableTaskRoutine;
    }
}