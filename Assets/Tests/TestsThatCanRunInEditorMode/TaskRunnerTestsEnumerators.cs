using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Lean;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

#if !NETFX_CORE
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
            _iterable1 = new LeanEnumerator(10000);
        }

        [UnityTest]
        public IEnumerator TestPooledTaskMemoryUsage()
        {
            IEnumerator<TaskContract> enumerator = new WaitForSecondsEnumerator(0.1f);

            var runner = new MultiThreadRunner("test");
            var RunOn  = enumerator.RunOn(runner);
            while ((RunOn).isRunning == true)
                yield return Yield.It;

            Assert.That(() =>
            {
                var continuationWrapper = enumerator.RunOn(runner);
                while ((continuationWrapper).isRunning == true) ;
            }, Is.Not.AllocatingGCMemory());

            runner.Dispose();
        }

        [UnityTest]
        public IEnumerator TestContinuatorSimple()
        {
            yield return Yield.It;

            //careful, runners can be garbage collected if they are not referenced somewhere and the
            //framework does not keep a reference
            using (var updateMonoRunner = new SteppableRunner("update"))
            {
                var cont = new LeanEnumerator(1).RunOn(updateMonoRunner);

                Assert.That(cont.isRunning, Is.True);

                updateMonoRunner.Step();

                Assert.That(cont.isRunning, Is.True);

                updateMonoRunner.Step();

                Assert.That(cont.isRunning, Is.False);
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
            yield return Yield.It;

            _iterable1.Complete();

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
            yield return Yield.It;

            var subEnumerator = SubEnumerator(0, 10);
            subEnumerator.Complete(10000);

            Assert.That(subEnumerator.Current.ToInt(), Is.EqualTo(10));
        }

        /// <summary>
        /// Svelto tasks allows to yield enumerators from inside other enumerators allowing more complex
        /// sequence of actions
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestFancyEnumerator()
        {
            yield return Yield.It;

            ComplexEnumerator((i) => Assert.That(i, Is.EqualTo(100))).Complete();
        }

        /// <summary>
        /// shows how simple is to concatenate parallel and serial sequence of naive enumerators
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestEvenFancierEnumerator()
        {
            yield return Yield.It;

            var multiThreadRunner = new MultiThreadRunner("test");
            MoreComplexEnumerator((i) => Assert.That(i, Is.EqualTo(20)), multiThreadRunner).Complete();
            multiThreadRunner.Dispose();
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
            //according the platform (on Unity the standard scheduler is the CoroutineMonoRunner, which
            //cannot run during these tests)
            GameLoop().Complete();

            Assert.Pass();
        }

        [Test]
        public void StandardUseOfEnumerators2()
        {
            /// <summary>
            /// since in order to avoid allocations is needed to preallocate enumerators, the SmartFunctionEnumerator
            /// can avoid some boilerplate 
            /// </summary>
            static IEnumerator<TaskContract> GameLoop2()
            {
                //initialization phase, for example you can precreate reusable enumerators or taskroutines
                //to avoid runtime allocations
                var smartFunctionEnumerator = new SmartFunctionEnumerator<int>(ExitTest, 0);

                //start a loop, you can actually start multiple loops with different conditions so that
                //you can wait for specific states to be valid before to start the real loop 
                yield return smartFunctionEnumerator.Continue();
                yield return smartFunctionEnumerator.Continue(); //it can be reused differently than a compiler generated iterator block
                yield return smartFunctionEnumerator.value;
            }

            var gameLoop2 = GameLoop2();
            gameLoop2.Complete(10000);

            Assert.That(gameLoop2.Current.ToInt(), Is.EqualTo(2));
        }
        
        public class SerialSteppableRunner : SyncRunner
        {
            public SerialSteppableRunner(string name):base(name)
            {
                var modifier = new SerialFlow {runnerName = name};
                
                UseModifier(modifier);
            }
        }
        class RefHolder
        {
            public uint value;
        }        
        
        [Test]
        public void SerialFlowModifierRunsTasksInSerial()
        {
            IEnumerator<TaskContract> TestEnum(RefHolder value, uint testvalue)
            {
                Assert.That(value.value == testvalue);
                
                yield return Yield.It;
                value.value++;
                yield return Yield.It;
                value.value++;
                yield return Yield.It;
                value.value++;
                
                Assert.That(value.value == testvalue + 3);
            }

            var                   refHolder = new RefHolder();
            SerialSteppableRunner runner    = new SerialSteppableRunner("test");

            //although the tasks run in serial, they order of execution is not guaranteed (that's why it's 0, 6, 3)
            TestEnum(refHolder, 0).RunOn(runner);
            TestEnum(refHolder, 6).RunOn(runner);
            TestEnum(refHolder, 3).RunOn(runner);
            
            runner.ForceComplete(1000);
            
            Assert.That(refHolder.value, Is.EqualTo(9));
        }

        [UnityTest]
        public IEnumerator TestEnumeratorStartingFromEnumeratorIndependently()
        {
            yield return Yield.It;

            using (var runner = new MultiThreadRunner("test"))
            {
                var continuation = NestedEnumerator(runner).RunOn(runner);

                while ((continuation).isRunning)
                    yield return Yield.It;
            }
        }

        static IEnumerator<TaskContract> NestedEnumerator(MultiThreadRunner runner)
        {
            yield return Yield.It;

            new WaitForSecondsEnumerator(0.1f).RunOn(runner);

            yield return Yield.It;
        }

        static IEnumerator<TaskContract> GameLoop()
        {
            //initialization phase, for example you can precreate reusable enumerators or taskroutines
            //to avoid runtime allocations
            var reusableEnumerator = new WaitForSecondsEnumerator(1);
            int i                  = 0;

            //start a loop, you can actually start multiple loops with different conditions so that
            //you can wait for specific states to be valid before to start the real loop 
            while (true) //usually last as long as the application run
            {
                if (i++ > 1)
                    yield break;

                yield return reusableEnumerator.Continue();

                reusableEnumerator.Reset();

                yield return Yield.It; //yield one iteration, if you forget this, will enter in an infinite loop!
                //it's not mandatory but there must be at least one yield in a loop
            }
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
        IEnumerator<TaskContract> ComplexEnumerator(Action<int> callback)
        {
            int i = 0;
            int j = 0;
            while (j < 5) //do it five times
            {
                j++;

                var enumerator = SubEnumerator(i, 10); //naive enumerator! it allocates
                yield return enumerator.Continue(); //yield until is done 
                enumerator = SubEnumerator((int)enumerator.Current.ToInt(), 10); //naive enumerator! it allocates
                yield return enumerator.Continue(); //yield until is done 
                i = (int)enumerator.Current.ToInt(); //careful it will be unboxed
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
        IEnumerator<TaskContract> MoreComplexEnumerator(Action<int> callback, MultiThreadRunner runner)
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
                yield return enumerator1.RunOn(runner);
                yield return enumerator2.RunOn(runner);

                i = (int)enumerator1.Current.ToInt() + (int)enumerator2.Current.ToInt();
            }

            callback(i);
        }

        IEnumerator<TaskContract> SubEnumerator(int i, int total)
        {
            int count = i + total;
            do
            {
                yield return Yield.It; //enable asynchronous execution
            } while (++i < count);

            yield return i; //careful it will be boxed;
        }

        LeanEnumerator _iterable1;
    }
}
#endif