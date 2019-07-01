/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

public class GameStateBehaviour : MonoBehaviour
{
    private ButtonCanvasBehaviour buttonCanvas;

    private Dictionary<string, int> flags = new Dictionary<string, int>();

    private AsyncOperation baseScene;
    private AsyncOperation addedScene;

    private string subsceneName;

    private string _selectedItem = "";
    private string _glowingButton = "";

    private bool hasIncrementedModuleNumber;
    private bool gameEndFlag;
    private bool tutorialPlayedBefore;
    private float nonOverridenTime;
    private string musicToPlay;
    private long lastMapTimeCounterUpdateTime;  // Global%MapTimeCounter is incremented whenever player is in the map

    public void LoadScene(string sceneName)
    {
        if (baseScene != null)
            return;

        baseScene = SceneManager.LoadSceneAsync(sceneName);
        if (baseScene != null)
        {
            baseScene.allowSceneActivation = false;
            buttonCanvas.StopMusic();
            musicToPlay = null;
            glowingButton = null;
            subsceneName = null;
        }
    }

    public void LoadARScene(string sceneName)
    {
        if (baseScene != null)
            return;

        baseScene = SceneManager.LoadSceneAsync(sceneName);
        if (baseScene != null)
        {
            baseScene.allowSceneActivation = false;
            buttonCanvas.StopMusic();
            musicToPlay = null;
            glowingButton = null;
            subsceneName = null;
            addedScene = SceneManager.LoadSceneAsync("ARScene", LoadSceneMode.Additive);
        }
    }

    public void LoadARSubscene(string subsceneName)
    {
        this.subsceneName = subsceneName;
        SceneManager.LoadScene(subsceneName, LoadSceneMode.Additive);
        SceneManager.LoadScene("ARSubscene", LoadSceneMode.Additive);
    }

    public void UnloadARSubscene()
    {
        subsceneName = null;
    }

    public void ProcessMapSceneModuleInitialize()
    {
        // track the highest module ever attained
        if (GetFlagIntValue("Global%Module") > GetFlagIntValue("Global%HighestModule"))
        {
            SetFlag("Global%HighestModule", GetFlagIntValue("Global%Module"));
        }

        // if we are replaying a module but the game has moved past that module,
        // go back to the highest module (which should be the ending screen)
        if (GetFlagIntValue("Global%Module") != GetFlagIntValue("Global%ReplayModule"))
        {
            SetFlag("Global%GameEnd", false);  // Global%GameEnd == true unhides the Replay Ending button
            SetFlag("Global%Module", GetFlagIntValue("Global%HighestModule"));
       }

        // cache game end flag
        gameEndFlag = GetFlag("Global%GameEnd");

        // if loading for the first time and tutorial has been played, save that fact
        if (GetFlag("M0%Scene1%End") || GetFlag("Global%FinishedFirstTutorial"))
        {
            SetFlag("M0%Scene1%End", true);
            SetFlag("Global%FinishedFirstTutorial", true);
            tutorialPlayedBefore = true;
        }
        else
            tutorialPlayedBefore = false;

        nonOverridenTime = 0;

        // prevent sleep on dev mode, enable sleep otherwise
#if DEVELOPMENT_BUILD
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
#else
        Screen.sleepTimeout = SleepTimeout.SystemSetting;
#endif
    }

    public bool ProcessMapSceneModuleIncrement(bool scenesAvailable)
    {
        // if not in edit mode and there are no active waypoints, we have reached the end of a module
        // and should increment the module number and reread the waypoints
        if (scenesAvailable)
        {
            hasIncrementedModuleNumber = false;
        }
        else if (! hasIncrementedModuleNumber)
        {
            hasIncrementedModuleNumber = true;
            AppendToAnalyticsString("_");
            SetFlag("Global%Module", GetFlagIntValue("Global%Module") + 1);
        }
        else
        {
            hasIncrementedModuleNumber = false;
            Debug.LogError("No activatable scenes for module " + GetFlagIntValue("Global%Module") + "!");

            // decrement the module number that we have erroneously incremented
            if (GetFlagIntValue("Global%Module") == GetFlagIntValue("Global%HighestModule"))
                SetFlag("Global%HighestModule", GetFlagIntValue("Global%HighestModule") - 1);
            SetFlag("Global%Module", GetFlagIntValue("Global%Module") - 1);
        }
        if (! hasIncrementedModuleNumber)
            Debug.Log(DeviceInput.HumanReadableEncoding("Analytics: " + PlayerPrefs.GetString("AnalyticsCode")));
        return hasIncrementedModuleNumber;
    }

    public bool ProcessMapSceneRestart()
    {
        if (gameEndFlag != GetFlag("Global%GameEnd"))
            return true;
        else
            return false;
    }

    public bool ProcessMapSceneOverrideUpdate(string music)
    {
        if (! GetFlag("M0%Scene1%End"))
        {
            // special handling for tutorial: auto-start it if it exists and it hasn't been played
            bool tutorialExists = false;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                scenePath = scenePath.Substring(scenePath.LastIndexOf("/") + 1);
                if (scenePath == "M0%Scene1.unity")
                    tutorialExists = true;
            }
            // if the tutorial has been played before, then the tutorial is being played again due to a user retrigger;
            // if the tutorial has NOT been played before, play the tutorial only if it has not run before
            // (this prevents the edge case where the player retriggers the tutorial then exits the game)
            if (tutorialExists && (tutorialPlayedBefore || ! GetFlag("Global%FinishedFirstTutorial")))
            {
                buttonCanvas.SetDialogue(null);  // in case a dialogue was shown
                LoadARScene("M0%Scene1");
                return true;
            }
        }

        if (nonOverridenTime >= 1.0f && DeviceInput.locationDialogUnanswered)
        {
            // if location access question somehow got unanswered
            // (happens on iOS when screen was turned off during the dialog),
            // reload the map scene to ask the question again
            Debug.Log("Location dialog not answered, asking again...");
            BackToMapScene();
            return true;
        }

        // play the music
        // but not on the first frame to allow location starting without music stuttering (Android)
        // and not if the location dialog is unanswered (iOS)
        buttonCanvas.PreloadSound(music);
        if (musicToPlay != music && nonOverridenTime != 0 && ! DeviceInput.locationDialogUnanswered)
        {
            musicToPlay = music;
            buttonCanvas.PlayMusic(musicToPlay, true);
        }

        if (nonOverridenTime <= 1.0f)
            nonOverridenTime += Time.deltaTime;

        ProcessMapSceneCounter();
        return false;
    }

    public void ProcessMapSceneCounter()
    {
        if (! loadingNewScene)
        {
            // if map scene is continuing, log the time spent by the user in it
            if ((long) Time.realtimeSinceStartup != lastMapTimeCounterUpdateTime)
            {
                if (GetFlagIntValue("Global%SceneTimeCounter") != 0)
                    EncodeAnalyticsEndScene(false);  // exiting a scene without finishing

                // regardless of actual time passed, only increment per actual gameplay second
                // (since player may have turned the device off in the interim)
                lastMapTimeCounterUpdateTime = (long) Time.realtimeSinceStartup;
                SetFlag("Global%MapTimeCounter", GetFlagIntValue("Global%MapTimeCounter") + 1, false);
            }
        }
    }

    public void SendOnlineAnalytics(string eventName, Dictionary<string, object> eventData)
    {
        eventData.Add("timeStamp", (System.DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
        eventData.Add("batteryLevel", DeviceInput.batteryLevel);
        eventData.Add("batteryCharging", DeviceInput.batteryCharging);
        eventData.Add("leftHanded", GetFlag("System%SwapButtonGroups"));
        eventData.Add("gyro", GetFlag("System%UseGyroscope"));
        eventData.Add("compass", GetFlag("System%UseCompass"));
        eventData.Add("gyroPresent", DeviceInput.gyroPresent);
        eventData.Add("compassPresent", DeviceInput.compassPresent);

        AnalyticsResult result = AnalyticsEvent.Custom(eventName, eventData);
        if (result != AnalyticsResult.Ok)
        {
            Debug.LogError("Analytics failure: " + result + " " + eventName);
        }
    }

    public void EncodeAnalyticsLeaveMap(int option)
    {
        // ONLINE ANALYTICS
        string eventName = "MapScene.Navigate";
        int duration = GetFlagIntValue("Global%MapTimeCounter");
        SendOnlineAnalytics(eventName, new Dictionary<string, object>
        {
            { "duration", duration }
        });

        // OFFLINE ANALYTICS
        int minutes = GetFlagIntValue("Global%MapTimeCounter") / 60;
        if (minutes > 25)
            minutes = 25;  // out of range (though you travelled so far, boy I'm sorry you are)
        AppendToAnalyticsString("abcdefghijklmnopqrstuvwxyz".Substring(minutes, 1));
        if (option > 0)
        {
            if (option > 26)
                option = 26;
            AppendToAnalyticsString("abcdefghijklmnopqrstuvwxyz".Substring(option - 1, 1));
        }

        SetFlag("Global%MapTimeCounter", 0);
        SetFlag("Global%SceneTimeCounter", 1);
    }

    public void EncodeAnalyticsBeginScene(string scene, bool markerVisible)
    {
        // ONLINE ANALYTICS
        bool gpsValid = GetFlag("Global%GPSValid");
        PlayerPrefs.SetString("AnalyticsScene", scene);
        string eventName = scene + ".Enter";
        SendOnlineAnalytics(eventName, new Dictionary<string, object>
        {
            { "markerVisible", markerVisible },
            { "gpsValid", gpsValid },
        });
    }

    public void EncodeAnalyticsMilestone(string milestone)
    {
        // ONLINE ANALYTICS
        string scene = PlayerPrefs.GetString("AnalyticsScene", "");
        if (scene != "" && milestone != "")
        {
            string eventName = scene + ".Milestone." + milestone;
            int duration = GetFlagIntValue("Global%SceneTimeCounter");
            SendOnlineAnalytics(eventName, new Dictionary<string, object>
            {
                { "duration", duration }
            });
        }
    }

    public void EncodeAnalyticsEndScene(bool endFlagSet)
    {
        // ONLINE ANALYTICS
        string scene = PlayerPrefs.GetString("AnalyticsScene", "");
        if (scene != "")
        {
            if (endFlagSet)
                EncodeAnalyticsMilestone("Finished");
            string eventName = scene + ".Exit";
            int duration = GetFlagIntValue("Global%SceneTimeCounter");
            SendOnlineAnalytics(eventName, new Dictionary<string, object>
            {
                { "duration", duration }
            });
        }

        // OFFLINE ANALYTICS
        // counter == 1 is reserved for scene loaded but not started by the player for some reason
        // (e.g., marker failing to scan, tutorial skipped)
        if (GetFlagIntValue("Global%SceneTimeCounter") > 1)
        {
            int minutes = (GetFlagIntValue("Global%SceneTimeCounter") - 1) / 60;
            if (minutes > 25)
                minutes = 25;  // out of range (though you travelled so far, boy I'm sorry you are)
            AppendToAnalyticsString("ABCDEFGHIJKLMNOPQRSTUVWXYZ".Substring(minutes, 1));
        }
        if (! endFlagSet)
            AppendToAnalyticsString("-");

        PlayerPrefs.SetString("AnalyticsScene", "");
        SetFlag("Global%SceneTimeCounter", 0);
    }

    public void AppendToAnalyticsString(string s)
    {
        PlayerPrefs.SetString("AnalyticsCode", (PlayerPrefs.GetString("AnalyticsCode") + s).TrimStart(new char[]{ '_' }));
        PlayerPrefs.Save();
    }

    public void BackToMapScene(bool fromDialog = true)
    {
        LoadScene("MapScene");
        buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, null);
        buttonCanvas.SetStatus(ButtonCanvasStatusType.ERROR, null);
        buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, null);
        buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
        if (fromDialog)
        {
            buttonCanvas.SetStill(null);
            buttonCanvas.SetFade(new Color(0, 0, 0, 0), 0);
            buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 0, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 1, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 2, null);
        }
        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 0, null);
        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, null);
        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 2, null);
        buttonCanvas.showDynamicGroup = false;
        buttonCanvas.SetDialogue(null);
    }

    public void ProcessReturnToMap()
    {
        bool sceneFinished = GetFlag(sceneName + "%End")
                             || sceneName == "M0%Scene1" && GetFlag("Global%FinishedFirstTutorial")
                             || GetFlag("Global%Module" + moduleName.Substring(1) + "End");
        bool secretAvailable = GetFlag(sceneName + "%SecretAvailable");
        if (GetFlag("Global%EasyBackButton") || sceneFinished && ! secretAvailable)
        {
            BackToMapScene(false);
            return;
        }

        bool tutorial = (sceneName == "M0%Scene1");
        buttonCanvas.ShowQuestionOverlay(tutorial ? "Are you sure you want to skip the tutorial?\n\nYou can still access the tutorial in the future by tapping \"Replay tutorial\" in the Options screen."
                                                  : (sceneFinished && secretAvailable) ? "You seem to be done here, but are you sure you want to leave now?\n\nYou might be leaving something behind..."
                                                                                       : "You are not finished here yet! Are you sure you want to leave now?\n\nIf you leave now, your current progress here will be saved so you can continue the game later.",
                                         tutorial ? "Skip tutorial" : "Leave",
                                         "Continue playing",
                                         delegate(string pressedButton)
        {
            buttonCanvas.HideOverlay();
            if (pressedButton != "Continue playing")
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
# if UNITY_EDITOR
                if (Input.GetMouseButton(1))
# else
                if (Input.touchCount > 1)
# endif
                {
                    if (GetFlagIntValue("Global%SceneTimeCounter") > 1)
                    {
                        SetFlag(sceneName + "%End", true);  // note that we DO encode analytics end scene in development build
                        EncodeAnalyticsEndScene(true);      // if button was pressed via two-finger click/tap and the scene was begun
                    }
                }
                else
#endif
                if (tutorial)
                    SetFlag("M0%Scene1%End", true);  // note that we DON'T encode analytics end scene
                BackToMapScene();
            }
        });
    }

    public string moduleName
    {
        get
        {
            int pos = SceneManager.GetActiveScene().name.IndexOf('%');
            if (pos != -1)
                return SceneManager.GetActiveScene().name.Substring(0, pos);
            else
                return "M0";  // default module
        }
    }

    public string sceneName
    {
        get
        {
            if (subsceneName == null)
                return SceneManager.GetActiveScene().name;
            else
                return subsceneName;
        }
    }

    public bool loadingNewScene
    {
        get
        {
            return baseScene != null && subsceneName == null;
        }
    }

    public bool currentlyInSubscene
    {
        get
        {
            return subsceneName != null;
        }
    }

    public string selectedItem
    {
        get
        {
            return _selectedItem;
        }
        set
        {
            _selectedItem = value;
            if (_selectedItem == null)
                _selectedItem = "";
            GameObject oldObject = GameObject.Find("InventoryObject");
            if (oldObject != null)
                Destroy(oldObject);
        }
    }

    public string glowingButton
    {
        get
        {
            return _glowingButton;
        }
        set
        {
            _glowingButton = value;
            if (_glowingButton == null)
                _glowingButton = "";
        }
    }

    public string[] GetFlagsStartingWith(string value)
    {
        List<string> result = new List<string>();
        foreach (string flag in flags.Keys)
        {
            if (flag.StartsWith(value))
                result.Add(flag);
        }
        return result.ToArray();
    }

    public bool GetFlag(string flagName)
    {
        return flags.ContainsKey(flagName);
    }

    public int GetFlagIntValue(string flagName)
    {
        if (flags.ContainsKey(flagName))
            return flags[flagName];
        else
            return 0;
    }

    public void SetFlag(string flagName, bool value, bool saveImmediately = true)
    {
        if (value)
        {
            if (! flags.ContainsKey(flagName))
            {
                flags.Add(flagName, 1);
                if (saveImmediately)
                    SaveFlags();
            }
        }
        else
        {
             if (flags.ContainsKey(flagName))
            {
                flags.Remove(flagName);
                if (saveImmediately)
                    SaveFlags();
            }
        }
    }

    public void SetFlag(string flagName, int value, bool saveImmediately = true)
    {
        if (value != 0)
        {
            if (flags.ContainsKey(flagName))
            {
                flags[flagName] = value;
                if (saveImmediately)
                    SaveFlags();
            }
            else
            {
                flags.Add(flagName, value);
                if (saveImmediately)
                    SaveFlags();
            }
        }
        else
        {
            if (flags.ContainsKey(flagName))
            {
                flags.Remove(flagName);
                if (saveImmediately)
                    SaveFlags();
            }
        }
    }

    public void LoadFlags()
    {
        flags.Clear();
        foreach (string flag in PlayerPrefs.GetString("GameState").Split(' '))
        {
            string flagName = flag.Split('=')[0];
            if (flagName == flag)
                SetFlag(flagName, true, false);
            else
                SetFlag(flagName, int.Parse(flag.Split('=')[1]), false);
        }
    }

    public void ResetFlags(string[] flagNames)
    {
        foreach (string flagName in flagNames)
            SetFlag(flagName, false);
    }

    public void GenerateNewAnalytics()
    {
        PlayerPrefs.SetString("AnalyticsCode", "");
        PlayerPrefs.SetInt("AnalyticsResetCount", PlayerPrefs.GetInt("AnalyticsResetCount", -1) + 1);

        AnalyticsResult result = Analytics.SetUserId(DeviceInput.deviceSerial + "+" + PlayerPrefs.GetInt("AnalyticsResetCount", -1));
        if (result != AnalyticsResult.Ok)
        {
            Debug.LogError("Analytics failure: " + result + " SetUserId");
        }
    }

    public void ResetFlags()
    {
        GenerateNewAnalytics();
        flags.Clear();
        SaveFlags();  // important to do this; if all flags below are false, the cleared flags are never saved
        SetFlag("System%SwapButtonGroups", buttonCanvas.swapButtonGroups);
        SetFlag("System%UseGyroscope", DeviceInput.gyro);
        SetFlag("System%UseCompass", DeviceInput.compass);
    }

    public void SaveFlags()
    {
        List<string> result = new List<string>();
        foreach (string flagName in flags.Keys)
        {
            string flag = flagName;
            if (flags[flagName] != 1)
                flag += "=" + flags[flagName];
            result.Add(flag);
        }
        PlayerPrefs.SetString("GameState", String.Join(" ", result.ToArray()));
        PlayerPrefs.Save();
    }

    public bool EvaluateFlags(string flags)
    {
        bool stillTrue = true;
        bool negate = false;

        if (flags == "-")
            return true;

        // eliminate special cases
        flags = flags
            .Replace("  ", " ").Replace("< ", "<").Replace("> ", ">").Replace("= ", "=")
            .Replace(" !", "!").Replace(" <", "<").Replace(" >", ">").Replace(" =", "=")
            + " ";

        string currentFlag = "";
        for (int i = 0; i < flags.Length; i++)
        {
            if (flags[i] == ' ' || flags[i] == '|' || flags[i] == '!' && flags[i + 1] != '=')
            {
                if (currentFlag != "")
                {
                    if (currentFlag.IndexOf('%') == -1)
                        currentFlag = sceneName + "%" + currentFlag;
                    bool flagValue = GetFlag(currentFlag);

                    // support comparison operators for integers
                    foreach (string comparator in new[]{ "!=", "==", "<=", ">=", "<", ">" })
                    {
                        if (currentFlag.IndexOf(comparator) != -1)
                        {
                            int compValue = int.Parse(currentFlag.Substring(currentFlag.IndexOf(comparator) + comparator.Length));
                            currentFlag = currentFlag.Substring(0, currentFlag.IndexOf(comparator));

                            if (comparator == "!=")
                                flagValue = (GetFlagIntValue(currentFlag) != compValue);
                            else if (comparator == "==")
                                flagValue = (GetFlagIntValue(currentFlag) == compValue);
                            else if (comparator == "<=")
                                flagValue = (GetFlagIntValue(currentFlag) <= compValue);
                            else if (comparator == ">=")
                                flagValue = (GetFlagIntValue(currentFlag) >= compValue);
                            else if (comparator == "<")
                                flagValue = (GetFlagIntValue(currentFlag) < compValue);
                            else if (comparator == ">")
                                flagValue = (GetFlagIntValue(currentFlag) > compValue);

                            break;  // if one comparator has been found, assume no others are there
                                    // (prevents < or > being executed after <= or >=)
                        }
                    }

                    if (negate)
                        flagValue = ! flagValue;
                    negate = false;
                    if (! flagValue)
                        stillTrue = false;
                    currentFlag = "";
                }

                if (flags[i] == '!' && flags[i + 1] != '=')
                    negate = true;
                else if (flags[i] == '|')
                {
                    negate = false;
                    if (stillTrue)
                        return true;  // if the expression has been true before the OR, no need to check the rest
                    stillTrue = true;
                }
            }
            else
                currentFlag += flags[i];
        }
        return stillTrue;
    }

    private void Start()
    {
        Application.targetFrameRate = 600;  // render as fast as we can (vsync will slow it down)
        GameObject.DontDestroyOnLoad(GameObject.Find("EventSystem"));
        GameObject.DontDestroyOnLoad(GameObject.Find("GameState"));
        GameObject.DontDestroyOnLoad(GameObject.Find("ButtonCanvas"));
        GameObject.DontDestroyOnLoad(GameObject.Find("LogCanvas"));

        buttonCanvas = GameObject.Find("ButtonCanvas").GetComponent<ButtonCanvasBehaviour>();

        LoadFlags();

        buttonCanvas.swapButtonGroups = GetFlag("System%SwapButtonGroups");

        DeviceInput.Init();
        if (PlayerPrefs.GetInt("AnalyticsResetCount", -1) == -1)
        {
            GenerateNewAnalytics();
            DeviceInput.gyro = DeviceInput.gyroPresent;
            DeviceInput.compass = DeviceInput.compassPresent && ! DeviceInput.gyro;
        }
        else
        {
            DeviceInput.gyro = GetFlag("System%UseGyroscope");
            DeviceInput.compass = GetFlag("System%UseCompass") && ! DeviceInput.gyro;
        }
        SetFlag("System%UseGyroscope", DeviceInput.gyro);
        SetFlag("System%UseCompass", DeviceInput.compass);
    }

    private void Update()
    {
        if (baseScene == null
            && GameObject.FindWithTag("ARMarker") == null
            && GameObject.FindWithTag("MainCamera") == null
            && GameObject.Find("ScreenCanvas") == null)
        {
            if (buttonCanvas.overlayShowing)
                return;

            buttonCanvas.showLoading = true;
            LoadScene("TitleScene");
        }

        // every update frame, test sensors for any malfunction
        DeviceInput.CheckMalfunction();

        // allow async level load to proceed only when button canvas animation is finished
        if (baseScene != null)
        {
            if (buttonCanvas.readyToLoadLevel)
            {
                baseScene.allowSceneActivation = true;
                buttonCanvas.showLoading = true;
            }
            if (baseScene.isDone && baseScene.allowSceneActivation)
            {
                if (addedScene == null || addedScene.isDone)
                {
                    baseScene = null;
                    addedScene = null;
                }
            }
        }
        else
            buttonCanvas.showLoading = false;
    }

    private void OnApplicationPause(bool paused)
    {
        // when pausing the app (not the game), the android sensor is destroyed to save battery;
        if (paused)
            DeviceInput.Destroy();
        else
        {
            DeviceInput.Init();
            SetFlag("System%UseGyroscope", DeviceInput.gyro, false);
            SetFlag("System%UseCompass", DeviceInput.compass, false);
        }
    }

    private void OnDestroy()
    {
        DeviceInput.Destroy();
    }
}
