using System.Collections;
using NUnit.Framework;
using Svelto.Tasks;
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
            Assert.AreEqual(severalTasksParent.Current, 10);
        }
        
        [UnityTest]
        public IEnumerator TestThatABreakItBreaksTheWholeExecution()
        {
            yield return null;

            var severalTasksParent = SeveralTasksParentBreak();
            severalTasksParent.RunOnScheduler(new SyncRunner());

            Assert.True(_iterable1.AllRight);
            Assert.False(_iterable2.AllRight); 
            Assert.AreNotEqual(severalTasksParent.Current, 10);
        }
        
        IEnumerator SeveralTasksParent()
        {
            yield return SeveralTasks();

            yield return 10;
        }
        
        IEnumerator SeveralTasks()
        {
            yield return _iterable1;

            yield break;

#pragma warning disable 162
            yield return _iterable2;
#pragma warning restore 162
        }
        
        IEnumerator SeveralTasksParentBreak()
        {
            yield return SeveralTasksBreak();

            yield return 10;
        }
        
        IEnumerator SeveralTasksBreak()
        {
            yield return _iterable1;

            yield return Break.It;

            yield return _iterable2;
        }
        
        Enumerator _iterable1;
        Enumerator _iterable2;
    }
}