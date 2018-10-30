/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
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

#if UNITY_ANDROID && ! UNITY_EDITOR
    private float obbDelay;
#endif

    public void LoadScene(string sceneName)
    {
        if (baseScene != null)
            return;

        buttonCanvas.StopMusic();
        subsceneName = null;
        baseScene = SceneManager.LoadSceneAsync(sceneName);
        baseScene.allowSceneActivation = false;
    }

    public void LoadARScene(string sceneName)
    {
        if (baseScene != null)
            return;

        buttonCanvas.StopMusic();
        subsceneName = null;
        baseScene = SceneManager.LoadSceneAsync(sceneName);
        addedScene = SceneManager.LoadSceneAsync("ARScene", LoadSceneMode.Additive);
        baseScene.allowSceneActivation = false;
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

    public void ResetFlags()
    {
        flags.Clear();
        SetFlag("System%AlreadyRunOnce", true);
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
        if (! GetFlag("System%AlreadyRunOnce"))
        {
            DeviceInput.gyro = DeviceInput.gyroPresent;
            DeviceInput.compass = DeviceInput.compassPresent && ! DeviceInput.gyro;
            SetFlag("System%AlreadyRunOnce", true);
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
#if UNITY_ANDROID && ! UNITY_EDITOR
            // check first for obb
            if (obbDelay > 0.0f)
            {
                obbDelay -= Time.deltaTime;
                return;
            }
            else if (GooglePlayDownloader.GetMainOBBPath(GooglePlayDownloader.GetExpansionFilePath()) == null)
            {
                buttonCanvas.ShowQuestionOverlay("The game needs to download resources from the Google Play Store.\n\nYou may want to switch to Wi-Fi to avoid mobile data charges.",
                    "Exit game",
                    "Download now",
                    delegate(string pressedButton)
                    {
                        buttonCanvas.HideOverlay();
                        if (pressedButton == "Download now")
                        {
                            GooglePlayDownloader.FetchOBB();
                            obbDelay = 1.0f;
                        }
                        else
                            Application.Quit();
                    });
                return;
            }
#endif
            LoadScene("TitleScene");
        }

        // every update frame, test sensors for any malfunction
        DeviceInput.CheckMalfunction();

        // allow async level load to proceed only when button canvas animation is finished
        if (baseScene != null)
        {
            if (! buttonCanvas.showDynamicGroup)
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
