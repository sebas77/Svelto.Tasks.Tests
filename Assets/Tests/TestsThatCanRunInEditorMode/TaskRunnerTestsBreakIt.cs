using System.Collections;
using System.Collections.Generic;
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
            _iterable1 = new LeanEnumerator(10000);
            _iterable2 = new LeanEnumerator(10000);
        }

        [UnityTest]
        public IEnumerator TestThatAStandardBreakBreaksTheCurrentTaskOnly()
        {
            yield return TaskContract.Yield.It;

            IEnumerator<TaskContract> severalTasksParent = SeveralTasksParent();
            severalTasksParent.Complete(1000);
            
            Assert.True(_iterable1.AllRight);
            Assert.False(_iterable2.AllRight);
            Assert.AreEqual(severalTasksParent.Current.ToInt(), 10);
        }

        [UnityTest]
        public IEnumerator TestThatABreakAndStopBreaksTheWholeExecution()
        {
            var severalTasksParent = SeveralTasksParentBreak();
            severalTasksParent.Complete(10000);

            Assert.True(_iterable1.AllRight);
            Assert.False(_iterable2.AllRight);
            Assert.AreNotEqual(severalTasksParent.Current.ToInt(), 10);

            yield break; 
        }
        
        [UnityTest]
        public IEnumerator TestThatABreakItBreaksTheCurrentExecution()
        {
            var severalTasksParent = SeveralTasksBreakIt();
            severalTasksParent.Complete(1000);

            Assert.True(_iterable1.AllRight);
            Assert.False(_iterable2.AllRight);
            Assert.AreNotEqual(severalTasksParent.Current.ToInt(), 10);

            yield break;
        }

        IEnumerator<TaskContract> SeveralTasksParent()
        {
            yield return SeveralTasks().Continue();

            yield return 10;
        }

        IEnumerator<TaskContract> SeveralTasks()
        {
            yield return _iterable1.Continue();

            yield break;

#pragma warning disable 162
            yield return _iterable2.Continue();
#pragma warning restore 162
        }

        IEnumerator<TaskContract> SeveralTasksParentBreak()
        {
            yield return SeveralTasksBreak().Continue();

            yield return 10;
        }

        IEnumerator<TaskContract> SeveralTasksBreak()
        {
            yield return _iterable1.Continue();

            yield return TaskContract.Break.AndStop;

            yield return _iterable2.Continue();
        }
        
        IEnumerator<TaskContract> SeveralTasksBreakIt()
        {
            yield return _iterable1.Continue();

            yield return TaskContract.Break.It;

            yield return 10;
        }

        LeanEnumerator _iterable1;
        LeanEnumerator _iterable2;
    }
}