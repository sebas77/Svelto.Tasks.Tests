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
		var syncRunner = new SyncRunner();
		var task = waitForSecondsEnumerator.AllocateNewRoutine().SetScheduler(syncRunner);
		while (true)
		{
			yield return task;
			
			yield return null;
		}
	}
}