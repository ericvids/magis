/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

public class LogCanvasBehaviour : MonoBehaviour
{
    public const int NUM_LINES = 30;

    private string log = "";

    private void Start()
    {
        Application.logMessageReceived += HandleLogMessageReceived;
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void Update()
    {
        if (Input.touchCount == 4 && Input.touches[3].phase == TouchPhase.Began || Input.GetKeyDown(KeyCode.Space))
        {
            if (Input.touchCount == 4 && Input.touches[3].tapCount == 2 && ! AREngineBehaviour.debuggingOverlay)
                log = "";
            AREngineBehaviour.debuggingOverlay = ! AREngineBehaviour.debuggingOverlay;
            if (! AREngineBehaviour.debuggingOverlay)
                GetComponent<UnityEngine.UI.Text>().text = "";
            else
                Debug.Log("Log display ON. Tap with four fingers again to turn off. Battery: " + DeviceInput.batteryLevel + "%\n");
        }
    }
#endif

    private void HandleLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        log = log + type + ": " + logString + "\n";
        if (type != LogType.Log)
            log += stackTrace + "\n";

        int lineCount = 0, pos = log.Length;
        while (lineCount < NUM_LINES)
        {
            pos = log.LastIndexOf('\n', pos - 1);
            if (pos < 0)
            {
                pos = 0;
                break;
            }
            lineCount++;
        }
        log = log.Substring(pos);

        if (AREngineBehaviour.debuggingOverlay)
            GetComponent<UnityEngine.UI.Text>().text = log;
    }
}
