using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Unity;
using UnityEngine.TestTools;

namespace Test
{
    /// <summary>
    /// Svelto tasks essentially should return:
    /// null (skip an iteration)
    /// another enumerator (keep on running it)
    /// a Break value. These tests test the Break return
    /// </summary>
    [TestFixture]
    public class TaskRunnerTestsBreakIt
    {
        [SetUp]
        public void Setup()
        {
            _iterable1 = new Enumerator(10000);
            _iterable2 = new Enumerator(10000);
        }

        [UnityTest]
        public IEnumerator TestThatAStandardBreakBreaksTheCurrentTaskOnly()
        {
            yield return null;

            var severalTasksParent = SeveralTasksParent();
            severalTasksParent.RunOnScheduler(new SyncRunner());

            Assert.True(_iterable1.AllRight);
            Assert.False(_iterable2.AllRight); 
            Assert.AreEqual((int)severalTasksParent.Current, 10);
        }
        
        [UnityTest]
        public IEnumerator TestThatABreakItBreaksTheWholeExecution()
        {
            yield return null;

            var severalTasksParent = SeveralTasksParentBreak();
            severalTasksParent.RunOnScheduler(new SyncRunner());

            Assert.True(_iterable1.AllRight);
            Assert.False(_iterable2.AllRight); 
            Assert.AreNotEqual((int)severalTasksParent.Current, 10);
        }
        
        IEnumerator<TaskContract?> SeveralTasksParent()
        {
            yield return SeveralTasks().Continue();

            yield return 10;
        }
        
        IEnumerator<TaskContract?> SeveralTasks()
        {
            yield return _iterable1.Continue();

            yield break;

#pragma warning disable 162
            yield return _iterable2.Continue();
#pragma warning restore 162
        }
        
        IEnumerator<TaskContract?> SeveralTasksParentBreak()
        {
            yield return SeveralTasksBreak().Continue();

            yield return 10;
        }
        
        IEnumerator<TaskContract?> SeveralTasksBreak()
        {
            yield return _iterable1.Continue();

            yield return Break.It;

            yield return _iterable2.Continue();
        }
        
        Enumerator _iterable1;
        Enumerator _iterable2;
    }
}
