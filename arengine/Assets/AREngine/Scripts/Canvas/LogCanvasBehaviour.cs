/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using System.Collections;

public class LogCanvasBehaviour : MonoBehaviour
{
    // set to true to show the log
    public static bool showing;

    public const int NUM_LINES = 30;

    private string log = "";

    private void Start()
    {
        Application.logMessageReceived += HandleLogMessageReceived;
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void Update()
    {
        if (Input.touchCount == 3 && Input.GetTouch(2).tapCount % 2 == 0 && Input.GetTouch(2).phase == TouchPhase.Began || Input.GetKeyDown(KeyCode.Space))
        {
#if ! UNITY_EDITOR
            if (Input.touchCount == 3 && Input.GetTouch(2).tapCount % 4 == 0 && ! LogCanvasBehaviour.showing)
#endif
                log = "";
            LogCanvasBehaviour.showing = ! LogCanvasBehaviour.showing;
            if (! LogCanvasBehaviour.showing)
                GetComponent<UnityEngine.UI.Text>().text = "";
            else
                Debug.Log("Log display ON. Double-tap with three fingers again to turn off.");
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

        if (LogCanvasBehaviour.showing && this != null)  // weirdest thing ever: this pointer can actually be null during GC, causing an exception on GetComponent
            GetComponent<UnityEngine.UI.Text>().text = log;
    }
}
