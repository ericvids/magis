/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using UnityEngine.Analytics;

using System;
using System.Collections;
using System.Collections.Generic;

public class UnityAnalyticsIntegration
{
    private static Dictionary<string, string> map = new Dictionary<string, string>();

    private static readonly string TIME_STAMP_PARAM_KEY = "timeStamp";
    private static readonly string TIME_DIFF_PARAM_KEY = "timeDiff";
    private static readonly string LATITUDE_DIFF_PARAM_KEY = "latitudeDiff";
    private static readonly string LONGITUDE_DIFF_PARAM_KEY = "longitudeDiff";
    private static readonly string BATTERY_LEVEL_PARAM_KEY = "batteryLevel";
    private static readonly string BATTERY_DIFF_PARAM_KEY = "batteryDiff";

    private static readonly DateTime BASE_DATE_TIME = new DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);

    static UnityAnalyticsIntegration()
    {
        string analyticsData = PlayerPrefs.GetString ("Analytics");
        if(analyticsData != "")
        {
            string[] values = analyticsData.Split (' ');

            foreach(string val in values)
            {
                int index = val.IndexOf ('-');

                string scene = val.Substring (0, index);
                string v = val.Substring (index + 1);

                Debug.Log ("Analytics dictionary load: " + scene + "-" + v);
                map.Add (scene, v);
            }
        }

        string userId = PlayerPrefs.GetString ("AnalyticsUserID");
        if(userId == "")
        {
            userId = GenerateUniqueID();

            PlayerPrefs.SetString ("AnalyticsUserID", userId);
            PlayerPrefs.Save ();
        }

#if ! (DEVELOPMENT_BUILD || UNITY_EDITOR)
        Debug.Log ("Analytics set user to " + userId);
        Analytics.SetUserId (userId);
#endif
    }

    public enum SceneEvent
    {
        Enter, EnterGpsInvalid, Start, StartNoMarker, Complete
    }

    public static void Reset()
    {
        PlayerPrefs.DeleteKey ("Analytics");
        PlayerPrefs.DeleteKey ("AnalyticsUserID");
        PlayerPrefs.Save ();

        string userId = PlayerPrefs.GetString ("AnalyticsUserID");
        if(userId == "")
        {
            userId = GenerateUniqueID();

            PlayerPrefs.SetString ("AnalyticsUserID", userId);
            PlayerPrefs.Save ();
        }

#if ! (DEVELOPMENT_BUILD || UNITY_EDITOR)
        Debug.Log ("Analytics set user to " + userId);
        Analytics.SetUserId (userId);
#endif
    }

    public static void ParseCommand(GameStateBehaviour gameState, string[] parameters)
    {
        if(parameters[1] == "Milestone")
        {
            if(!IsSceneDone (gameState, gameState.sceneName))
            {
                string milestoneName = gameState.sceneName + "%" + parameters[2];
                string milestoneFlag = parameters[1] + "%" + milestoneName;

                if(!gameState.GetFlag (milestoneFlag))
                {
                    gameState.SetFlag (milestoneFlag, true, false);
                    Milestone (milestoneName);
                }
            }
        }
        else
        {
            Debug.LogError ("Analytics: Unknown parameter " + parameters[1]);
        }
    }

    public static void EvaluateSetTrueFlag(GameStateBehaviour gameState, string parameter)
    {
        if(IsSceneDone (gameState, gameState.sceneName))
        {
            return;
        }

        if(parameter.EndsWith ("Minigame_Finished"))
        {
            SceneComplete (gameState, gameState.sceneName);
        }
        else if(parameter.EndsWith ("End") && (parameter != "Global%TutorialEnd") && (parameter != "Global%GameEnd"))
        {
            SceneComplete (gameState, gameState.sceneName);
        }
    }

    public static void TutorialSkip(GameStateBehaviour gameState, string sceneName)
    {
        if(IsSceneDone (gameState, sceneName))
        {
            return;
        }

        int startTimeStamp = GetSceneTimeStamp (sceneName);
        int startBatteryLevel = GetSceneBatteryLevel (sceneName);

        int timeDiff = GenerateTimeStamp () - startTimeStamp;
        int batteryDiff = DeviceInput.batteryLevel - startBatteryLevel;

        Dictionary<string, object> parameters = new Dictionary<string, object>();
        parameters.Add (TIME_STAMP_PARAM_KEY, GenerateTimeStamp ());
        parameters.Add (TIME_DIFF_PARAM_KEY, timeDiff);
        parameters.Add (BATTERY_DIFF_PARAM_KEY, batteryDiff);

        SendCustomEvent ("Tutorial.Skip", parameters);

        RemoveSceneFromData (sceneName);
    }

    public static void SceneEnter(GameStateBehaviour gameState, string sceneName, double latitudeDiff, double longitudeDiff, bool normalEnter = true)
    {
        if(IsSceneDone (gameState, sceneName))
        {
            return;
        }

        Dictionary<string, object> parameters = new Dictionary<string, object>();
        parameters.Add (LATITUDE_DIFF_PARAM_KEY, latitudeDiff);
        parameters.Add (LONGITUDE_DIFF_PARAM_KEY, longitudeDiff);
        parameters.Add (TIME_STAMP_PARAM_KEY, GenerateTimeStamp ());
        parameters.Add (BATTERY_LEVEL_PARAM_KEY, DeviceInput.batteryLevel);

        SendSceneEvent (sceneName, (normalEnter ? SceneEvent.Enter : SceneEvent.EnterGpsInvalid), parameters);
    }

    public static void SceneStart(GameStateBehaviour gameState, string sceneName, bool withMarker = true)
    {
        if(IsSceneDone (gameState, sceneName))
        {
            return;
        }

        int timeStamp = GenerateTimeStamp ();
        int batteryLevel = DeviceInput.batteryLevel;

        Dictionary<string, object> parameters = new Dictionary<string, object>();
        parameters.Add (TIME_STAMP_PARAM_KEY, timeStamp);
        parameters.Add (BATTERY_LEVEL_PARAM_KEY, batteryLevel);

        SendSceneEvent (sceneName, (withMarker ? SceneEvent.Start : SceneEvent.StartNoMarker), parameters);

        if(!IsSceneInData (sceneName))
        {
            SetSceneTimeStampAndBatteryLevel (sceneName, timeStamp, batteryLevel);
        }
    }

    public static void SceneComplete(GameStateBehaviour gameState, string sceneName)
    {
        if(IsSceneDone (gameState, sceneName))
        {
            return;
        }

        int startTimeStamp = GetSceneTimeStamp (sceneName);
        int startBatteryLevel = GetSceneBatteryLevel (sceneName);

        int timeDiff = GenerateTimeStamp () - startTimeStamp;
        int batteryDiff = DeviceInput.batteryLevel - startBatteryLevel;

        Dictionary<string, object> parameters = new Dictionary<string, object>();
        parameters.Add (TIME_STAMP_PARAM_KEY, GenerateTimeStamp ());
        parameters.Add (BATTERY_LEVEL_PARAM_KEY, DeviceInput.batteryLevel);
        parameters.Add (TIME_DIFF_PARAM_KEY, timeDiff);
        parameters.Add (BATTERY_DIFF_PARAM_KEY, batteryDiff);

        parameters.Add ("LeftHanded", gameState.GetFlag("System%SwapButtonGroups"));
        parameters.Add ("Gyro", gameState.GetFlag("System%UseGyroscope"));
        parameters.Add ("Compass", gameState.GetFlag("System%UseCompass"));

        SendSceneEvent (sceneName, SceneEvent.Complete, parameters);

        RemoveSceneFromData (sceneName);
    }

    private static void SendSceneEvent(string sceneName, SceneEvent sceneEvent, Dictionary<string, object> extraParameters = null)
    {
        string customEventName = ConstructCustomEventName (sceneName, sceneEvent.ToString ());
        SendCustomEvent (customEventName, extraParameters);
    }

    public static void Milestone(string milestoneName)
    {
        string customEventName = ConstructCustomEventName ("Milestone", milestoneName);

        Dictionary<string, object> parameters = new Dictionary<string, object>();
        parameters.Add (TIME_STAMP_PARAM_KEY, GenerateTimeStamp ());
        parameters.Add (BATTERY_LEVEL_PARAM_KEY, DeviceInput.batteryLevel);

        SendCustomEvent (customEventName, parameters);
    }

    public static void SendCustomEvent(string customEventName, Dictionary<string, object> parameters = null)
    {
        string p = "";
        if(parameters != null)
        {
            Dictionary<string, object>.KeyCollection keys = parameters.Keys;
            foreach(string key in keys)
            {
                p += key + ": " + parameters[key] + ";";
            }
        }

#if ! (DEVELOPMENT_BUILD || UNITY_EDITOR)
        Debug.Log ("Analytics sending event: " + customEventName + " parameters: " + p);
        Analytics.CustomEvent (customEventName, parameters);
#endif
    }

    private static string ConstructCustomEventName(params string[] args)
    {
        string ret = "";

        if(args.Length == 0)
        {
            return ret;
        }

        ret += args[0];

        for(int i = 1; i < args.Length; i++)
        {
            ret += "." + args[i];
        }

        return ret;
    }

    private static int GenerateTimeStamp()
    {
        return (int)(DateTime.UtcNow - BASE_DATE_TIME).TotalSeconds;
    }

    private static int GetSceneTimeStamp(string sceneName)
    {
        if(map.ContainsKey (sceneName))
        {
            string val = map[sceneName];
            string[] split = val.Split ('/');

            return Int32.Parse (split[0]);
        }

        return -1;
    }

    private static int GetSceneBatteryLevel(string sceneName)
    {
        if(map.ContainsKey (sceneName))
        {
            string val = map[sceneName];
            string[] split = val.Split ('/');

            return Int32.Parse (split[1]);
        }

        return -1;
    }

    private static void SetSceneTimeStampAndBatteryLevel(string sceneName)
    {
        SetSceneTimeStampAndBatteryLevel (sceneName, GenerateTimeStamp (), DeviceInput.batteryLevel);
    }

    private static void SetSceneTimeStampAndBatteryLevel(string sceneName, int timeStamp, int batteryLevel)
    {
        Debug.Log ("Set: " + sceneName + " timeStamp: " + timeStamp + " batteryLevel: " + batteryLevel);
        map.Add (sceneName, timeStamp + "/" + batteryLevel);
        SaveDictionary ();
    }

    private static void RemoveSceneFromData(string sceneName)
    {
        Debug.Log ("Remove: " + sceneName);
        map.Remove (sceneName);
        SaveDictionary ();
    }

    private static bool IsSceneInData(string sceneName)
    {
        return map.ContainsKey (sceneName);
    }

    private static void SaveDictionary()
    {
        List<string> values = new List<string>(map.Keys.Count);
        foreach(string key in map.Keys)
        {
            values.Add (key + "-" + map[key]);
        }

        string joined = string.Join (" ", values.ToArray ());

        PlayerPrefs.SetString ("Analytics", joined);
        PlayerPrefs.Save ();
    }

    private static string GenerateUniqueID()
    {
        var random = new System.Random();
        int timeStamp = GenerateTimeStamp ();

        string uniqueID = Application.systemLanguage                            //Language
            +"-"+Application.platform                                           //Device
            +"-"+String.Format("{0:X}", timeStamp)                              //Time
            +"-"+String.Format("{0:X}", Time.frameCount)                        //Time in game
            +"-"+String.Format("{0:X}", random.Next(1000000000));               //random number

        Debug.Log("Generated Unique ID: "+uniqueID);

        return uniqueID;
    }

    private static bool IsSceneDone(GameStateBehaviour gameState, string sceneName)
    {
        if(sceneName == "M0%Scene1")
        {
            return gameState.GetFlag ("Global%TutorialPlayed");
        }
        else if(sceneName == "M0%Scene2")
        {
            return gameState.GetFlag ("Global%EndingPlayed");
        }

        string moduleName = sceneName.Split ('%')[0];
        string moduleEndFlag = "Global%Module" + moduleName.Substring (1) + "End";

        if(!gameState.GetFlag (moduleEndFlag))
        {
            string sceneEndFlag = sceneName + "%End";
            return gameState.GetFlag (sceneEndFlag);
        }

        return true;
    }
}
