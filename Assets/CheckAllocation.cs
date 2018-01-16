using System.Collections;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using UnityEngine;

public class CheckAllocation : MonoBehaviour
{

	// Use this for initialization
	void Start()
	{
		UpdateIT().Run();
	}

	IEnumerator UpdateIT()
	{
		var waitForSecondsEnumerator = new WaitForSecondsEnumerator(0.1f);
		
		while (true)
		{
			yield return waitForSecondsEnumerator.RunOnSchedule(StandardSchedulers.syncScheduler);
			
			yield return null;
		}
	}
}