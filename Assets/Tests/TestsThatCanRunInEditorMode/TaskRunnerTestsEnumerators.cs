#if !NETFX_CORE

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Unity;
using Svelto.Tasks.Unity.Internal;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Test
{
    /// <summary>
    ///  Shows how to use Svelto.Tasks with plain Enumerator. While convenient, using just enumerators
    ///  would enable a subset of the Svelto.Task potential.
    ///  TaskRoutines should be exploited to enable advanced features. 
    /// </summary>
    [TestFixture]
    public class TaskRunnerTestsWithPooledTasks
    {
        [SetUp]
        public void Setup()
        {
            _iterable1 = new Enumerator(10000);
        }
        
        [Test]
        public void TestPooledTaskMemoryUsage()
        {
            WaitForSecondsEnumerator enumerator = new WaitForSecondsEnumerator(0.1f);
            
            var syncRunner = new SyncRunner();
            enumerator.RunOnScheduler(syncRunner);

            Assert.That(() =>
                        {
                            enumerator.RunOnScheduler(syncRunner);
                        }, Is.Not.AllocatingGCMemory());
        }
        
        [UnityTest]
        public IEnumerator TestContinuatorSimple()
        {
            yield return null;

            //careful, runners can be garbage collected if they are not referenced somewhere and the
            //framework does not keep a reference
            using (var updateMonoRunner = new UpdateMonoRunner("update"))
            {
                var cont = new Enumerator(1).RunOnScheduler(updateMonoRunner);
                
                Assert.That(cont.MoveNext, Is.True);

                var runnerBehaviour = updateMonoRunner._go.GetComponent<RunnerBehaviourUpdate>();
                
                runnerBehaviour.Update();

                Assert.That(cont.MoveNext, Is.True);

                runnerBehaviour.Update();

                Assert.That(cont.MoveNext, Is.False);
            }
        }

        /// <summary>
        /// basic way to run an Enumerator using a custom Runner.
        /// This will force an allocation per run as the Enumerator is created dynamically
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestUltraNaiveEnumerator()
        {
            yield return null;

            _iterable1.RunOnScheduler(new SyncRunner());

            Assert.That(_iterable1.AllRight, Is.True);
        }
        
        /// <summary>
        /// basic way to run an Enumerator using a custom Runner.
        /// This will force an allocation per run as the Enumerator is created dynamically
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestUltraNaiveEnumerator2()
        {
            yield return null;

            var subEnumerator = SubEnumerator(0, 10);
            subEnumerator.RunOnScheduler(new SyncRunner());

            Assert.That(subEnumerator.Current, Is.EqualTo(10));
        }
        
        /// <summary>
        /// implementation of disposable enumerator will call Dispose once the task is done
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestUltraDisposableEnumerator()
        {
            yield return null;

            var timeoutEnumerator = new TimeoutEnumerator();
            timeoutEnumerator.RunOnScheduler(new SyncRunner(2000));

            Assert.That(timeoutEnumerator.disposed, Is.True);
        }

        /// <summary>
        /// Svelto tasks allows to yield enumerators from inside other enumerators allowing more complex
        /// sequence of actions
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestFancyEnumerator()
        {
            yield return null;

            ComplexEnumerator((i) => Assert.That(i, Is.EqualTo(100))).RunOnScheduler(new SyncRunner());
        }
        
        /// <summary>
        /// shows how simple is to concatenate parallel and serial sequence of naive enumerators
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestEvenFancierEnumerator()
        {
            yield return null;

            MoreComplexEnumerator((i) => Assert.That(i, Is.EqualTo(20)), new MultiThreadRunner("test")).RunOnScheduler(new SyncRunner());
        }

        /// <summary>
        /// this is the most common use of the Enumerators running on tasks. Svelto.Tasks is the core of
        /// every looped routine and nothing else should be used to create loops. (This is how loops
        /// are create in Svelto.ECS engines for examples)
        /// </summary>
        /// <returns></returns>
        [Test]
        public void StandardUseOfEnumerators()
        {
            //you would normally write GameLoop().Run() to run on the standard scheduler, which changes
            //according the platfform (on Unity the standard scheduler is the CoroutineMonoRunner, which
            //cannot run during these tests)
            GameLoop().RunOnScheduler(new SyncRunner(4000));
            
            Assert.Pass();
        }
        
        [Test]
        public void StandardUseOfEnumerators2()
        {
            var gameLoop2 = GameLoop2();
            gameLoop2.RunOnScheduler(new SyncRunner(4000));
            
            Assert.That(gameLoop2.Current, Is.EqualTo(2));
        }
        
        [Test]
        public void TestCoroutineMonoRunnerStartsTheFirstIterationImmediately()
        {
            var testFirstInstruction = TestFirstInstruction();
            testFirstInstruction.RunOnScheduler(StandardSchedulers.coroutineScheduler);
            
            Assert.That(testFirstInstruction.Current, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TestEnumeratorStartingFromEnumeratorIndipendently()
        {
            yield return null;

            using (var runner = new MultiThreadRunner("test"))
            {
                NestedEnumerator(runner).RunOnScheduler(runner);
            }
        }

        static IEnumerator NestedEnumerator(MultiThreadRunner runner)
        {
            yield return null;
            
            new WaitForSecondsEnumerator(0.1f).RunOnScheduler(runner);

            yield return null;
        }


        static IEnumerator TestFirstInstruction()
        {
            yield return 1;
        }

        static IEnumerator GameLoop()
        {
            //initialization phase, for example you can precreate reusable enumerators or taskroutines
            //to avoid runtime allocations
            var reusableEnumerator = new WaitForSecondsEnumerator(1);
            int i = 0;

            //start a loop, you can actually start multiple loops with different conditions so that
            //you can wait for specific states to be valid before to start the real loop 
            while (true) //usually last as long as the application run
            {
                if (i++ > 1) yield break;
                
                yield return reusableEnumerator;
                
                reusableEnumerator.Reset();
                
                yield return null; //yield one iteration, if you forget this, will enter in an infinite loop!
                                   //it's not mandatory but there must be at least one yield in a loop
            }
        }

        /// <summary>
        /// since in order to avoid allocations is needed to preallocate enumerators, the SmartFunctionEnumerator
        /// can avoid some boilerplate 
        /// </summary>
        static IEnumerator GameLoop2()
        {
            //initialization phase, for example you can precreate reusable enumerators or taskroutines
            //to avoid runtime allocations
            var smartFunctionEnumerator = new SmartFunctionEnumerator<int>(ExitTest);

            //start a loop, you can actually start multiple loops with different conditions so that
            //you can wait for specific states to be valid before to start the real loop 
            yield return smartFunctionEnumerator;
            yield return smartFunctionEnumerator; //it can be reused differently than a compiler generated iterator block
            yield return (smartFunctionEnumerator as IEnumerator<int>).Current; //boxiiiiiiiing there are better way to do this, but it's ok if performance is not a problem
        }
        
        /// <summary>
        /// it will be called until i >= 2
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        static bool ExitTest(ref int i)
        {
            if (i < 2)
            {
                i++;
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// very naive implementation, it's boxing and allocation madness. Just for testing purposes
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        IEnumerator ComplexEnumerator(Action<int> callback)
        {
            int i = 0;
            int j = 0;
            while (j < 5) //do it five times
            {
                j++;

                var enumerator = SubEnumerator(i, 10); //naive enumerator! it allocates
                yield return enumerator; //yield until is done 
                enumerator = SubEnumerator((int) enumerator.Current, 10); //naive enumerator! it allocates
                yield return enumerator; //yield until is done 
                i = (int) enumerator.Current; //careful it will be unboxed
            }

            callback(i);
        }
        
        /// <summary>
        /// very naive implementation, it's boxing and allocation madness. Just for testing purposes
        /// this give a first glimpse to the powerful concept of Svelto Tasks continuation (running
        /// tasks on other runners and white their completion on the current runner)
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        IEnumerator MoreComplexEnumerator(Action<int> callback, MultiThreadRunner runner)
        {
            int i = 0;
            {
                var enumerator1 = SubEnumerator(0, 10); //naive enumerator! it allocates
                var enumerator2 = SubEnumerator(0, 10); //naive enumerator! it allocates

                //the two enumerator will run in "parallel" on the multithread runner
                //I need to use a multithread runner as these tests won't allow using the normal
                //coroutine while the syncrunner is not suitable as it forces the current task to be finished first 
                //defeating the point of testing the parallelism
                //Running an enumerator from inside another enumerator is different than yielding. A yield
                //enumerator is yield on the same runner, while in this way the enumerator starts on another
                //runner. The generated continuator is a new enumerator, used to spin the current runner
                //untile the enumerators are done.
                //In a real scenario, any compatible runner can be used.
                var continuator1 = enumerator1.RunOnScheduler(runner);
                var continuator2 = enumerator2.RunOnScheduler(runner);

                while (continuator1.completed == false || continuator2.completed == false)
                    yield return null;

                i = (int)enumerator1.Current + (int)enumerator2.Current;
            }

            callback(i);
        }

        IEnumerator SubEnumerator(int i, int total)
        {
            int count = i + total;
            do
            {
                yield return null; //enable asynchronous execution
            } while (++i < count);

            yield return i; //careful it will be boxed;
        }
        
        Enumerator        _iterable1;
    }
}
#endif