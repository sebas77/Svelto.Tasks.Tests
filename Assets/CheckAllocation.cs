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
		while (true)
		{
			yield return waitForSecondsEnumerator.RunOnScheduler(syncRunner);
			
			yield return null;
		}
	}
}