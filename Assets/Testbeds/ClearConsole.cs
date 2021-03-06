﻿using System.Reflection;
using UnityEditor;

public static class UnityConsole
{
    public static void Clear()
    {
#if UNITY_EDITOR        
        var assembly = Assembly.GetAssembly(typeof (SceneView));
        var type = assembly.GetType("UnityEditor.LogEntries");
        var method = type.GetMethod("Clear");
        method.Invoke(new object(), null);
#endif        
    }
}