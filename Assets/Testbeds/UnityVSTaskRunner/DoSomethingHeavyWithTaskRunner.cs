using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks;
using Svelto.Tasks.Unity;
using UnityEngine;

namespace Test.Editor.UnityVSTaskRunner
{
    public class DoSomethingHeavyWithTaskRunner : MonoBehaviour
    {
        void Awake()
        {
            _direction = new Vector2(Mathf.Cos(Random.Range(0, 3.14f)) / 1000, Mathf.Sin(Random.Range(0, 3.14f) / 1000));
            _transform = this.transform;

            _task = TaskRunner.Instance.AllocateNewTaskRoutine();
            _task.SetEnumeratorProvider(UpdateIt2);
        }

        void OnEnable() 
        {
            _task.Start();
        }
      
        IEnumerator<TaskContract?> UpdateIt2()
        {
            while (true) 
            {
                _transform.Translate(_direction);

                yield return null;
            }
        }

        void OnDisable()
        {
            _task.Stop();
        }

        Vector3 _direction;
        Transform _transform;
        Svelto.Tasks.ITaskRoutine _task;
    }
}
