using System.Collections;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using UnityEngine;
using UnityEngine.Networking;

namespace Test.Editor
{
    public class ExampleSerialTasks : MonoBehaviour 
    {
        [TextArea]
        public string Notes = "This example shows how to run different types of tasks in Serial with the TaskRunner";

        void OnEnable()
        {
            UnityConsole.Clear();
        }

        void Start () 
        {
            SerialTaskCollection st = new SerialTaskCollection();
		
            st.Add(Print(1));
            st.Add(Print(2));
            st.Add(DoSomethingAsynchonously(1));
            st.Add(Print(3));
            st.Add(WaitForSecondsTest());
            st.Add(Print(4));
            st.Add(WWWTest ());
            st.Add(Print(5));
            st.Add(Print(6));

            TaskRunner.Instance.Run(st);
        }
	
        IEnumerator Print(int i)
        {
            Debug.Log(i);
            yield return null;
        }
	
        IEnumerator DoSomethingAsynchonously(float time)
        {
            yield return new WaitForSeconds(time);
		
            Debug.Log("waited " + time);
        }
	
        IEnumerator WWWTest()
        {
            UnityWebRequest www = new UnityWebRequest("www.google.com");
		
            yield return new UnityWebRequestEnumerator(www);
		
            Debug.Log("www done:" + www.GetResponseHeaders());
        }

        IEnumerator WaitForSecondsTest()
        {
            int counter = 0;
            while (counter < 2)
            {
                Debug.Log("TestTwice Loop: Time " + Time.time + " C: " + counter);
                counter++;

                yield return new WaitForSeconds(1f);
            }
        }
    }
}
