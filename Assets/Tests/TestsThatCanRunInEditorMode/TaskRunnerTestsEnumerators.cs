#if later
#if !NETFX_CORE

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Internal;
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

            _iterable1.Run(new SyncRunner());

            Assert.That(_iterable1.AllRight, Is.True);
        }

        [Test]
        public void TestPooledTaskMemoryUsage()
        {
            WaitForSecondsEnumerator enumerator = new WaitForSecondsEnumerator(0.1f);
            
            var syncRunner = new SyncRunner();
            enumerator.Run(syncRunner);

            Assert.That(() =>
                        {
                            enumerator.Run(syncRunner);
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
            subEnumerator.Run(new SyncRunner());

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

                var continuationWrapper = ComplexEnumerator().Run(updateMonoRunner);

                int steps = 0;
                while (continuationWrapper.MoveNext() && steps < 101)
                {
                    updateMonoRunner.Step();
                    steps++;
                }

                Assert.That(i, Is.EqualTo(100));
            }
        }
        
     /*   public IEnumerator TestUltraFancyEnumerator()
        {
            yield return null;

            using (var updateMonoRunner = new UpdateMonoRunner("test"))
            {
                using (var updateMonoRunner2 = new UpdateMonoRunner<SlowTaskStruct>("test"))
                {
                    int i = 0;

                    ITaskRoutine<SlowTaskStruct> task =
                        TaskRunner.Instance.AllocateNewTaskRoutine(updateMonoRunner2);

                    /// <summary>
                    /// very naive implementation, it's boxing and allocation madness. Just for testing purposes
                    /// </summary>
                    /// <param name="callback"></param>
                    /// <returns></returns>
                    IEnumerable<TaskContract?> ComplexEnumerator()
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
                            yield return enumerator.Continue(updateMonoRunner);                       //yield until is done 
                            enumerator = SubEnumerator((int) enumerator.Current, 10); //naive enumerator! it allocates 
                            yield return enumerator.Continue(updateMonoRunner);                       //yield until is done
                            yield return new SlowTaskStruct(1).Continue(updateMonoRunner2);
                            i = (int) enumerator.Current;                             //careful it will be unboxed
                            yield return task.Start();
                        }
                    }

                    var continuationWrapper = ComplexEnumerator().Run(updateMonoRunner);

                    int steps = 0;
                    while (continuationWrapper.MoveNext() && steps < 101)
                    {
                        updateMonoRunner.Step();
                        steps++;
                    }

                    Assert.That(i, Is.EqualTo(100));
                }
            }
        }
        */
     [UnityTest]
     public IEnumerator TestCoroutineMonoRunner()
     {
         var monorunner = new CoroutineMonoRunner("test");
         {
             int test = 0;
             int frame = 0;
             
             IEnumerator<TaskContract?> MonoRunner2()
             {
                 Assert.That(test, Is.EqualTo(1));
                 Assert.That(frame, Is.EqualTo(0));
                 test++;
                 yield return null;
                 Assert.That(test, Is.EqualTo(2));
                 Assert.That(frame, Is.EqualTo(1));
                 test++;
             }

             IEnumerator<TaskContract?> MonoRunner()
             {
                 Assert.That(test, Is.EqualTo(0));
                 Assert.That(frame, Is.EqualTo(0));
                 test++;
                 yield return MonoRunner2().Run(monorunner);
                 Assert.That(test, Is.EqualTo(3));
                 Assert.That(frame, Is.EqualTo(2));
             }

             var continuation = MonoRunner().Run(monorunner);
             
             while (continuation.MoveNext())
             {
                 frame++;
                 monorunner.Step();
                 yield return null;
             }
         }
         monorunner.Dispose();
     }


     /// <summary>
        /// shows how simple is to concatenate parallel and serial sequence of naive enumerators
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestEvenFancierEnumerator()
        {
            yield return null;

            MoreComplexEnumerator((i) => Assert.That(i, Is.EqualTo(20)), new MultiThreadRunner("test")).Run(new SyncRunner());
        }

        [UnityTest]
        public IEnumerator StandardUseOfEnumerators()
        {
            using (var runner = new UpdateMonoRunner("test"))
            {
                var systemLoop = SystemLoop();
                systemLoop.Start(runner);

                while (runner.numberOfQueuedTasks + runner.numberOfRunningTasks > 0)
                {
                    runner.Step();
                    yield return null;
                }
                
                Assert.That((int)systemLoop.Current, Is.EqualTo(3));
            }
        }

        [UnityTest]
        public IEnumerator StandardUseOfEnumerators3()
        {
            using (var runner = new UpdateMonoRunner<TaskRoutine<IEnumerator<TaskContract?>>>("test"))
            {
                using (var runner2 = new UpdateMonoRunner<LeanSveltoTask<IEnumerator<TaskContract?>>>("test"))
                {
                    var systemLoop = SystemLoop2(runner2);
                    systemLoop.ToTaskRoutine(runner).Start();

                    while (runner.numberOfQueuedTasks + runner.numberOfRunningTasks > 0)
                    {
                        runner.Step();
                        runner2.Step();
                        yield return null;
                    }

                    Assert.That((int) systemLoop.Current, Is.EqualTo(3));
                }
            }
        }

        [Test]
        public void StandardUseOfEnumerators2()
        {
            var gameLoop2 = GameLoop2();
            gameLoop2.Run(new SyncRunner(4000));
            
            Assert.That((int)gameLoop2.Current, Is.EqualTo(2));
        }
        
        [Test]
        public void TestCoroutineMonoRunnerStartsTheFirstIterationImmediately()
        {
            using (var runner = new CoroutineMonoRunner("test"))
            {
                var testFirstInstruction = TestFirstInstruction();
                testFirstInstruction.Run(runner);

                Assert.That((int)testFirstInstruction.Current, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator TestEnumeratorStartingFromEnumeratorIndependently()
        {
            yield return null;

            using (var runner = new MultiThreadRunner("test"))
            {
                NestedEnumerator(runner).Run(runner);
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
                var cont = new Enumerator(1).Run(updateMonoRunner);
                
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
            
            new WaitForSecondsEnumerator(0.1f).Run(runner);

            yield return null;
        }


        static IEnumerator<TaskContract?> TestFirstInstruction()
        {
            yield return 1;
        }

        static IEnumerator<TaskContract?> SystemLoop()
        {
            //initialize here, it happens only once
            int i = 0;
            //you may want to wait for something to happen before to start the mainloop
            var reusableEnumerator = new WaitForSecondsEnumerator(1);
            
            var taskContract = reusableEnumerator.Continue(); //continue means execute immediately
            
            DateTime then = DateTime.Now;
            
            yield return taskContract; //just to show you what continue returns. Yield means wait until it's done

            var totalSeconds = (DateTime.Now - then).TotalSeconds;
            
            Assert.That(totalSeconds, Is.InRange(0.9, 1.1));
            //start a loop, you can actually start multiple loops with different conditions so that
            //you can wait for specific states to be valid before to start the real loop 
            while (i < 3) //usually lasts as long as the application
            {
                i++;

                yield return null;
            }
            
            Assert.That(i, Is.EqualTo(3));

            yield return i;
        }
        
        static IEnumerator<TaskContract?> SystemLoop2(UpdateMonoRunner<LeanSveltoTask<IEnumerator<TaskContract?>>> runner)
        {
            //initialize here, it happens only once
            int i = 0;
            //you may want to wait for something to happen before to start the mainloop
            var reusableEnumerator = new WaitForSecondsEnumerator(1);
            
            var taskContract = reusableEnumerator.Run(runner); //continue means execute immediately
            
            DateTime then = DateTime.Now;
            
            yield return taskContract; //just to show you what continue returns. Yield means wait until it's done

            var totalSeconds = (DateTime.Now - then).TotalSeconds;
            
            Assert.That(totalSeconds, Is.InRange(0.9, 1.1));
            //start a loop, you can actually start multiple loops with different conditions so that
            //you can wait for specific states to be valid before to start the real loop 
            while (i < 3) //usually lasts as long as the application
            {
                i++;

                yield return null;
            }
            
            Assert.That(i, Is.EqualTo(3));

            yield return i;
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
            yield return smartFunctionEnumerator.Continue(); //it can be reused differently than a compiler
                                                             //generated iterator block
            yield return smartFunctionEnumerator.Current; //not boxing! ;)
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
                var continuator1 = enumerator1.Run(runner);
                var continuator2 = enumerator2.Run(runner);

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
#endif