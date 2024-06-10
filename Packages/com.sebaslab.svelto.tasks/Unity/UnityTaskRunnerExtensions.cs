#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.Tasks;
using UnityEngine;


    public static class UnityTaskRunnerExtensions
    {
        public static TaskContract ToTaskContract(this GameObject go)
        {
            return new TaskContract((object)go);
        }
    }

#endif