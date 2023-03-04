using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class displayConsole : MonoBehaviour
{
    static List<string> recentLogs = new();
    public TMPro.TMP_Text logText;

    void OnEnable()
    {
        Application.logMessageReceived += log;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= log;
    }

    public void log(string logString, string stackTrace, LogType type)
    {
        string currentLog = logString;
        if(currentLog.Length > 1100) { currentLog = currentLog.Substring(0, 1000); }

        recentLogs.Add(currentLog);
        if(recentLogs.Count > 16)
            recentLogs.RemoveAt(0);

        updateVisual();
    }

    public void updateVisual()
    {
        logText.text = "";
        foreach(string log in recentLogs)
            logText.text += log + "\n";
    }
}
