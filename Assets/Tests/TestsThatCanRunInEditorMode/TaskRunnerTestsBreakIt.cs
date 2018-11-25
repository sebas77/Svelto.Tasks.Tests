using System.Collections;
using NUnit.Framework;
using Svelto.Tasks;
using UnityEngine.TestTools;

namespace Test
{
    [TestFixture]
    public class TaskRunnerTestsBreakIt
    {
        [SetUp]
        public void Setup()
        {
            _iterable1 = new Enumerable(10000);
            _iterable2 = new Enumerable(10000);
        }

        [UnityTest]
        public IEnumerator TestBreakIt()
        {
            yield return null;

            SeveralTasksParent().RunOnScheduler(new SyncRunner());

            Assert.That(_iterable1.AllRight == true && _iterable2.AllRight == false);
        }
        
        IEnumerator SeveralTasksParent()
        {
            yield return SeveralTasks();

            yield return 10;
        }
        
        IEnumerator SeveralTasks()
        {
            yield return _iterable1.GetEnumerator();

            yield break;

            yield return _iterable2.GetEnumerator();
        }
        
        Enumerable _iterable1;
        Enumerable _iterable2;
    }
}