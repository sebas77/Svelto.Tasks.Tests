using System.Collections;
using NUnit.Framework;
using Svelto.Tasks;
using UnityEngine.TestTools;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsWithTaskChain
    {
        [SetUp]
        public void Setup()
        {
            _vo = new ValueObject();
            
            _taskChain1 = new TaskChain();
            _taskChain2 = new TaskChain();
            
            _serialTasks1   = new Svelto.Tasks.Chain.SerialTaskCollection<ValueObject>(_vo);
            _parallelTasks1 = new Svelto.Tasks.Chain.ParallelTaskCollection<ValueObject>(_vo);
            _serialTasks2   = new Svelto.Tasks.Chain.SerialTaskCollection<ValueObject>(_vo);
        }
        
        [UnityTest]
        public IEnumerator TestSerialTasks1ExecutedInParallelWithToken()
        {
            yield return null;

            _serialTasks1.Add(_taskChain1);
            _serialTasks1.Add(_taskChain1);
            _serialTasks2.Add(_taskChain2);
            _serialTasks2.Add(_taskChain2);

            _parallelTasks1.Add(_serialTasks1);
            _parallelTasks1.Add(_serialTasks2);

            _parallelTasks1.onComplete +=
                () => Assert.That(_vo.counter, Is.EqualTo(4));

            _parallelTasks1.RunOnScheduler(new SyncRunner());
        }
        
        
        TaskChain _taskChain1;
        TaskChain _taskChain2;
        
        Svelto.Tasks.Chain.SerialTaskCollection<ValueObject>   _serialTasks1;
        Svelto.Tasks.Chain.ParallelTaskCollection<ValueObject> _parallelTasks1;
        Svelto.Tasks.Chain.SerialTaskCollection<ValueObject>   _serialTasks2;
        
        ValueObject _vo;
    }
}