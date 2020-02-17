using System.Collections;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using UnityEngine;
using UnityEngine.Networking;

namespace Test.Editor
{
    public class ExampleTaskRoutine : MonoBehaviour 
    {
        [TextArea]
        public string Notes = "This example shows how to use the ITaskRoutine feature";

        int i;
    
        ITaskRoutine<IEnumerator> _taskRountine;
        bool         _paused;

        ParallelTaskCollection pt = new ParallelTaskCollection();
        SerialTaskCollection   st = new SerialTaskCollection();

        void OnEnable()
        {
            UnityConsole.Clear();
        }

        // Use this for initialization
        void Start () 
        {
            _taskRountine = TaskRunner.Instance.AllocateNewTaskRoutine();
                _taskRountine.SetEnumeratorProvider(ResetTaskAndRun); //The Task routine is pooled! You could have used Start directly, but you need to use SetEnumeratorProvider if you want restart the TaskRoutine later
            _taskRountine.Start();
        }

        IEnumerator ResetTaskAndRun() //this is the suggested why to reset complicated tasks
        {
            st.Clear();
            pt.Clear();

            st.Add(Print("s1"));
            st.Add(Print("s2"));
            st.Add(Print("s3"));
            st.Add(Print("s4"));
        
            pt.Add(Print("p1"));
            pt.Add(Print("p2"));
            pt.Add(Print("p3"));
            pt.Add(Print("p4"));
            pt.Add(Print("p5"));
            pt.Add(st);
            pt.Add(Print("p6"));
            pt.Add(WWWTest());
            pt.Add(Print("p7"));
            pt.Add(Print("p8"));

            return pt;
        }

        IEnumerator Print(string i)
        {
            Debug.Log(i);

            yield return null;
        }

        IEnumerator WWWTest()
        {
            UnityWebRequest www = new UnityWebRequest("http://download.thinkbroadband.com/5MB.zip");
        
            yield return new UnityWebRequestEnumerator(www);
        
            Debug.Log("www done:" + www.GetResponseHeaders().ToString());
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_paused == false)
                {
                    Debug.LogWarning("Paused!");

                    _taskRountine.Pause();
                    _paused = true;
                }
                else
                {
                    Debug.LogWarning("Resumed!");

                    _paused = false;
                    _taskRountine.Resume();
                }
            }

            if (Input.GetKeyUp(KeyCode.S))
                _taskRountine.Start();
        }
    
        IEnumerator DoSomethingAsynchonously()
        {
            yield return SomethingAsyncHappens();
        
            Debug.Log("index is: " + i);
        }
    
        IEnumerator SomethingAsyncHappens()
        {
            for (i = 0; i < 100; i++)
                yield return null;
        }
    }
}
