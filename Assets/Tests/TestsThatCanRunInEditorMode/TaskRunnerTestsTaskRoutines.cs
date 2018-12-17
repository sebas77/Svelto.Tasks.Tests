using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using UnityEngine.TestTools;

namespace Test
{
    /// <summary>
    /// TaskRoutines enable more advanced feature of Svelto.Tasks, like a promises like behaviour and allow explicit
    /// management of memory.
    /// </summary>
    ///
    /// Restart a task with compiled generated IEnumerator
    /// Restart a task with IEnumerator class
    /// Restart a task after SetEnumerator has been called (this must be still coded, as it must reset some values)
    /// Restart a task just restarted (pendingRestart == true)
    /// /// test pending coroutine wrapper
    /// /// test pause and resume tasks
    /// /// test stopping tasks
    /// /// test stopping runner 
    /// test pause and reusme runner
   /// 
    /// /// Start a taskroutine twice with different compiler generated enumerators and variants

     
    [TestFixture]
    public class TaskRunnerTestsTaskRoutines
    {
        [SetUp]
        public void Setup()
        {
            _iterable1 = new Enumerator(10000);
            
            //preallocate a task routine and reusing it is the normal pattern
            _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine().SetScheduler(new SyncRunner()); 
        }
        
        [UnityTest]
        public IEnumerator TestTaskRoutinesAllocate0WhenReused()
        {
            Assert.Inconclusive();
            yield break;
        }

        
        [UnityTest]
        public IEnumerator TestMultithreadWitTaskRoutines()
        {
            yield return null;
            
            //you can test an resettable enumerator once
            _reusableTaskRoutine.SetEnumerator(_iterable1);

            using (var runner = new MultiThreadRunner("TestMultithread"))
            {
                var continuator = _reusableTaskRoutine.SetScheduler(runner).Start();

                while (continuator.MoveNext()) yield return null;

                Assert.That(_iterable1.AllRight == true);

                continuator = _reusableTaskRoutine.Start(); //another start will reset the enumerator
                Assert.That(_iterable1.AllRight == false); //did it reset?

                while (continuator.MoveNext()) yield return null;

                Assert.That(_iterable1.AllRight == true);
            }
        }
        
        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineRestartsWithProvider()
        {
            yield return null;
            
            var result = new ValueObject();

            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStartStart"))
            {
                var taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine()
                        .SetScheduler(runner).SetEnumeratorProvider(() => SimpleEnumerator(result));
                
                taskRoutine.Start();
                yield return null; //since the enumerator waits for 1 second, it shouldn't have the time to increment
                var continuation = taskRoutine.Start();

                while (continuation.MoveNext())
                        yield return null; //now increment

                Assert.That(result.counter, Is.EqualTo(1));

                taskRoutine.Start();
                yield return null; //since the enumerator waits for 1 second, it shouldn't have the time to increment
                continuation = taskRoutine.Start();

                while (continuation.MoveNext())
                    yield return null; //now increment
                
                Assert.That(result.counter, Is.EqualTo(2));
                
                taskRoutine.Start();
                yield return null; //since the enumerator waits for 1 second, it shouldn't have the time to increment
                continuation = taskRoutine.Start();

                while (continuation.MoveNext())
                    yield return null; //now increment

                Assert.That(result.counter, Is.EqualTo(3));
            }
        }
        
        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineStopsStartsWithProvider()
        {
            yield return null;

            ValueObject result = new ValueObject();

            using (var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider"))
            {
                bool isCallbackCalled = false;
                
                var continuator = _reusableTaskRoutine.SetScheduler(runner)
                                                      .SetEnumeratorProvider(() => SimpleEnumerator(result))
                                                      .Start(onStop: () => isCallbackCalled = true);

                Assert.That(continuator.completed == false, "can't be completed");
                _reusableTaskRoutine.Stop();

                Thread.Sleep(500); //let's be sure the runner has the time to complete it

                Assert.That(continuator.completed == true, "must be completed");
                Assert.True(isCallbackCalled);               

                continuator = _reusableTaskRoutine.Start();

                while (continuator.MoveNext()) yield return null;
            }

            Assert.That(result.counter, Is.EqualTo(1));
        }
        
        [UnityTest]
        public IEnumerator TestExceptionsAreCaughtByTaskRoutines()
        {
            yield return null;

            using (var runner = new MultiThreadRunner("TestStopStartTaskRoutine"))
            {
                bool isCallbackCalled = false;
                _reusableTaskRoutine.SetScheduler(runner);
                _reusableTaskRoutine.SetEnumerator(TestWithThrow());
                
                var continuator = _reusableTaskRoutine.Start(onFail: (e) => isCallbackCalled = true);
                while (continuator.MoveNext()) yield return null;

                Assert.True(isCallbackCalled);
            }
        }
        
        [UnityTest]
        public IEnumerator TestPauseAndResume()
        {
            yield return null;
            
            _reusableTaskRoutine.SetEnumerator(_iterable1);

            using (var runner = new MultiThreadRunner("TestStopStartTaskRoutine"))
            {
                var continuator = _reusableTaskRoutine.SetScheduler(runner).Start();

                DateTime then = DateTime.Now.AddSeconds(2);

                while (continuator.MoveNext() && DateTime.Now > then)
                {
                    yield return null;
                    
                    _reusableTaskRoutine.Pause();
                }
                
                Assert.That(_iterable1.AllRight == false);
                
                _reusableTaskRoutine.Resume();
                
                while (continuator.MoveNext()) yield return null;

                Assert.That(_iterable1.AllRight == true);
            }
        }
        
        IEnumerator TestWithThrow()
        {
            yield return new WaitForSecondsEnumerator(0.1f);

            throw new Exception();
        }

        IEnumerator SimpleEnumerator(ValueObject result)
        {
            yield return new WaitForSecondsEnumerator(1);

            Interlocked.Increment(ref result.counter);
        }
        
        ITaskRoutine _reusableTaskRoutine;
        Enumerator   _iterable1;
    }
}