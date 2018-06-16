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
		var syncRunner = new SyncRunner<Allocation0Enumerator>();
		var syncRunner2 = new SyncRunner<WaitForSecondsEnumerator>();
		var task = waitForSecondsEnumerator.AllocateNewRoutine().SetScheduler(syncRunner2);
		var task2 = TaskRunner.Instance.AllocateNewTaskRoutine<Allocation0Enumerator>().SetScheduler(syncRunner);
		var serialtask = new SerialTaskCollection<Allocation0Enumerator>();
		
		int counter = 0;
		
		while (true)
		{
			yield return task.Start();

			//yield return new Allocation0Enumerator(counter++); //nay this allocates
			yield return task2.SetEnumerator(new Allocation0Enumerator(counter++)).Start(); //yay, this doesn't allocate!

			yield return serialtask.Add(new Allocation0Enumerator(counter)); //yay, this doesn't allocate!
			
			serialtask.Clear();

			yield return null;
		}
	}

	struct Allocation0Enumerator : IEnumerator
	{
		readonly int _counter;
		
		public Allocation0Enumerator(int counter) : this()
		{
			_counter = counter;
		}

		public bool MoveNext()
		{
			return false;
		}

		public void Reset()
		{}

		public object Current { get; }
	}
}