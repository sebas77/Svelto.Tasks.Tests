#if !NETFX_CORE

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Unity;
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
        
        /// <summary>
        /// basic way to run an Enumerator using a custom Runner.
        /// This will force an allocation per run as the Enumerator is created dynamically
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestUltraNaiveEnumerator2()
        {
            yield return null;
            
            IEnumerator<TaskContract?> SubEnumerator(int iinterna, int total)
            {
                int count = iinterna + total;
                do
                {
                    yield return null; //enable asynchronous execution
                } while (++iinterna < count);

                yield return iinterna; //this will be returned as TaskContract field
            }

            var subEnumerator = SubEnumerator(0, 10);
            subEnumerator.RunOnScheduler(new SyncRunner());

            Assert.That((int)subEnumerator.Current, Is.EqualTo(10));
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

            using (var updateMonoRunner = new UpdateMonoRunner("test"))
            {
                int i = 0;

                /// <summary>
                /// very naive implementation, it's boxing and allocation madness. Just for testing purposes
                /// </summary>
                /// <param name="callback"></param>
                /// <returns></returns>
                IEnumerator<TaskContract?> ComplexEnumerator()
                {
                    IEnumerator<TaskContract?> SubEnumerator(int iinterna, int total)
                    {
                        int count = iinterna + total;
                        do
                        {
                            yield return null; //enable asynchronous execution
                        } while (++iinterna < count);

                        yield return iinterna; //this will be returned as TaskContract field
                    }

                    int j = 0;
                    while (j < 5) //do it five times 
                    {
                        j++;

                        var enumerator = SubEnumerator(i, 10);                    //naive enumerator! it allocates
                        yield return enumerator.Continue();                       //yield until is done 
                        enumerator = SubEnumerator((int) enumerator.Current, 10); //naive enumerator! it allocates 
                        yield return enumerator.Continue();                       //yield until is done 
                        i = (int) enumerator.Current;                             //careful it will be unboxed
                    }
                }

                var continuationWrapper = ComplexEnumerator().RunOnScheduler(updateMonoRunner);

                int steps = 0;
                while (continuationWrapper.MoveNext() && steps < 101)
                {
                    updateMonoRunner.Step();
                    steps++;
                }

                Assert.That(i, Is.EqualTo(100));
            }
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
            
            Assert.That((int)gameLoop2.Current, Is.EqualTo(2));
        }
        
        [Test]
        public void TestCoroutineMonoRunnerStartsTheFirstIterationImmediately()
        {
            using (var runner = new CoroutineMonoRunner("test"))
            {
                var testFirstInstruction = TestFirstInstruction();
                testFirstInstruction.RunOnScheduler(runner);

                Assert.That((int)testFirstInstruction.Current, Is.EqualTo(1));
            }
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

                updateMonoRunner.Step();

                Assert.That(cont.MoveNext, Is.True);

                updateMonoRunner.Step();

                Assert.That(cont.MoveNext, Is.False);
            }
        }

        static IEnumerator<TaskContract?> NestedEnumerator(MultiThreadRunner runner)
        {
            yield return null;
            
            new WaitForSecondsEnumerator(0.1f).RunOnScheduler(runner);

            yield return null;
        }


        static IEnumerator<TaskContract?> TestFirstInstruction()
        {
            yield return 1;
        }

        static IEnumerator<TaskContract?> GameLoop()
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
                
                yield return reusableEnumerator.Continue();
                
                reusableEnumerator.Reset();
                
                yield return null; //yield one iteration, if you forget this, will enter in an infinite loop!
                                   //it's not mandatory but there must be at least one yield in a loop
            }
        }

        /// <summary>
        /// since in order to avoid allocations is needed to preallocate enumerators, the SmartFunctionEnumerator
        /// can avoid some boilerplate 
        /// </summary>
        static IEnumerator<TaskContract?> GameLoop2()
        {
            //initialization phase, for example you can precreate reusable enumerators or taskroutines
            //to avoid runtime allocations
            var smartFunctionEnumerator = new SmartFunctionEnumerator<int>(ExitTest);

            //start a loop, you can actually start multiple loops with different conditions so that
            //you can wait for specific states to be valid before to start the real loop 
            yield return smartFunctionEnumerator.Continue();
            yield return smartFunctionEnumerator.Continue(); //it can be reused differently than a compiler generated iterator block
            yield return smartFunctionEnumerator.Current; //boxiiiiiiiing there are better way to do this, but it's ok if performance is not a problem
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
        /// this give a first glimpse to the powerful concept of Svelto Tasks continuation (running
        /// tasks on other runners and white their completion on the current runner)
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        IEnumerator<TaskContract?> MoreComplexEnumerator(Action<int> callback, MultiThreadRunner runner)
        {
            IEnumerator<TaskContract?> SubEnumerator(int iinterna, int total)
            {
                int count = iinterna + total;
                do
                {
                    yield return null; //enable asynchronous execution
                } while (++iinterna < count);

                yield return iinterna; //this will be returned as TaskContract field
            }
            
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
        
        Enumerator        _iterable1;
    }
    
}
#endif