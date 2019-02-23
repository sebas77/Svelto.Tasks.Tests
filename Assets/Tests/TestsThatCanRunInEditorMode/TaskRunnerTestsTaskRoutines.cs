#if later
using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;


namespace Test
{
    /// <summary>
    /// TaskRoutines enable more advanced feature of Svelto.Tasks, like a promises like behaviour and allow explicit
    /// management of memory.
    /// </summary>
    ///
    /// Restart a task with compiled generated IEnumerator
    /// Restart a task with  IEnumerator<TaskContract> class
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
        }

        [Test]
        public void TestPooledTaskMemoryUsage()
        {
            var syncRunner = new SyncRunner<SlowTaskStruct>(2000);
            var task = TaskRunner.Instance.AllocateNewTaskRoutine(syncRunner);
            
            task.SetEnumerator(new SlowTaskStruct(1));
            task.Start();

            Assert.That(() =>
                        {
                            task.SetEnumerator(new SlowTaskStruct(1));}, Is.Not.AllocatingGCMemory());
            Assert.That(() =>
                        {   task.Start();}, Is.Not.AllocatingGCMemory());
            Assert.That(() =>
                        {   task.SetEnumerator(new SlowTaskStruct(1));}, Is.Not.AllocatingGCMemory());
            Assert.That(() =>
                        {   task.Start();}, Is.Not.AllocatingGCMemory());

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
            yield return Yield.It;


            var runner = new MultiThreadRunner("TestMultithread");
            {
                var _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
                _reusableTaskRoutine.SetEnumerator(_iterable1);
                var continuator = _reusableTaskRoutine.Start();

                while ((continuator).isRunning) yield return Yield.It;

                Assert.That(_iterable1.AllRight == true);

                continuator = _reusableTaskRoutine.Start(); //another start will reset the enumerator
                Assert.That(_iterable1.AllRight == false); //did it reset?

                while ((continuator).isRunning) yield return Yield.It;

                Assert.That(_iterable1.AllRight == true);
            }
            runner.Dispose();
        }
        
        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineRestartsWithProvider()
        {
            yield return Yield.It;
            
            var result = new ValueObject();

            var runner = new MultiThreadRunner("TestSimpleTaskRoutineStartStart");
            {
                var taskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
                taskRoutine.SetEnumeratorProvider(() => SimpleEnumerator(result));
                
                taskRoutine.Start();
                yield return Yield.It; //since the enumerator waits for 1 second, it shouldn't have the time to increment
                var continuation = taskRoutine.Start();

                while ((continuation).isRunning)
                        yield return Yield.It; //now increment

                Assert.That(result.counter, Is.EqualTo(1));

                taskRoutine.Start();
                yield return Yield.It; //since the enumerator waits for 1 second, it shouldn't have the time to increment
                continuation = taskRoutine.Start();

                while ((continuation).isRunning)
                    yield return Yield.It; //now increment
                
                Assert.That(result.counter, Is.EqualTo(2));
                
                taskRoutine.Start();
                yield return Yield.It; //since the enumerator waits for 1 second, it shouldn't have the time to increment
                continuation = taskRoutine.Start();

                while ((continuation).isRunning)
                    yield return Yield.It; //now increment

                Assert.That(result.counter, Is.EqualTo(3));
            }
            runner.Dispose();
        }
        
        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineStopsStartsWithProviderAndWaiting()
        {
            yield return Yield.It;

            ValueObject result = new ValueObject();

            var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider");
            {
                bool isCallbackCalled = false;
                
                var _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
                _reusableTaskRoutine.SetEnumeratorProvider(() => SimpleEnumerator(result));
                var continuator = _reusableTaskRoutine
                                                      .Start(onStop: () => isCallbackCalled = true);

                Assert.That((continuator).isRunning == true, "can't be completed");
                _reusableTaskRoutine.Stop();

                Thread.Sleep(500); //let's be sure the runner has the time to complete it

                Assert.That((continuator).isRunning == false, "must be completed");
                Assert.True(isCallbackCalled);               

                continuator = _reusableTaskRoutine.Start();

                while ((continuator).isRunning) yield return Yield.It;
            }
            
            runner.Dispose();

            Assert.That(result.counter, Is.EqualTo(1));
        }
        
        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineStopsStartsWithProviderForPending()
        {
            yield return Yield.It;

            ValueObject result = new ValueObject();

            var runner = new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider");
            {
                int index = 0;
                
                var _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
                _reusableTaskRoutine.SetEnumeratorProvider(() => SimpleEnumerator(result));
                var continuator = _reusableTaskRoutine
                   .Start(onStop: () => Interlocked.Add(ref index, 1));

                Assert.That((continuator).isRunning == true, "can't be completed");

                runner.isPaused = true;
                _reusableTaskRoutine.Stop();

                Assert.That((continuator).isRunning == true, "can't be completed");

                continuator = _reusableTaskRoutine.Start(onStop: () => Interlocked.Add(ref index, 1));
                
                runner.isPaused = false;

                while (index == 0) yield return Yield.It;

                while ((continuator).isRunning) yield return Yield.It;
            
                Assert.True(index == 1); //on stop is called only if explicitly stopped
            }
            
            runner.Dispose();

            Assert.That(result.counter, Is.EqualTo(1));
        }
        
        [UnityTest]
        public IEnumerator TestExceptionsAreCaughtByTaskRoutines()
        {
            yield return Yield.It;

            var runner = new MultiThreadRunner("TestStopStartTaskRoutine");
            {
                var _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
                bool isCallbackCalled = false;
                _reusableTaskRoutine.SetEnumerator(TestWithThrow());
                
                var continuator = _reusableTaskRoutine.Start(onFail: (e) => isCallbackCalled = true);
                while ((continuator).isRunning) yield return Yield.It;

                Assert.True(isCallbackCalled);
            }
            runner.Dispose();
        }
        
        [UnityTest]
        public IEnumerator TestPauseAndResume()
        {
            yield return Yield.It;

            var runner = new MultiThreadRunner("TestStopStartTaskRoutine");
            {
                var _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine(runner);
                _reusableTaskRoutine.SetEnumerator(_iterable1);
                var continuator = _reusableTaskRoutine.Start();

                DateTime then = DateTime.Now.AddSeconds(2);

                while ((continuator).isRunning && DateTime.Now > then)
                {
                    yield return Yield.It;
                    
                    _reusableTaskRoutine.Pause();
                }
                
                Assert.That(_iterable1.AllRight == false);
                
                _reusableTaskRoutine.Resume();
                
                while ((continuator).isRunning) yield return Yield.It;

                Assert.That(_iterable1.AllRight == true);
            }
            runner.Dispose();
        }
        
         IEnumerator<TaskContract> TestWithThrow()
        {
            yield return new WaitForSecondsEnumerator(0.1f);

            throw new Exception();
        }

         IEnumerator<TaskContract> SimpleEnumerator(ValueObject result)
        {
            yield return new WaitForSecondsEnumerator(1);

            Interlocked.Increment(ref result.counter);
        }
        
        Enumerator   _iterable1;
    }
}
#endif