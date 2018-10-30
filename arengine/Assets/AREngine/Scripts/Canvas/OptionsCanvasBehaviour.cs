/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using System.Collections;

public class OptionsCanvasBehaviour : CanvasBehaviour
{
    private GameStateBehaviour gameState;
    private IAREngine engine;

    private void Start()
    {
        gameState = GameObject.Find("GameState").GetComponent<GameStateBehaviour>();
        if (GameObject.FindWithTag("AREngine") != null)
            engine = GameObject.FindWithTag("AREngine").GetComponent<AREngineBehaviour>();

        GameObject.Find("Panel/AppDetails/AppIconMask/AppIcon").GetComponent<UnityEngine.UI.Image>().sprite = Resources.Load<Sprite>("AppIcon");
        GameObject.Find("Panel/AppDetails/AppTitle").GetComponent<UnityEngine.UI.Text>().text = Application.productName;
        GameObject.Find("Panel/AppDetails/AppVersion").GetComponent<UnityEngine.UI.Text>().text = "Version " + Application.version + "  <color=#000080>Credits...</color>";

        if (engine != null)
        {
            if (gameState.sceneName == "M0%Scene1" && ! gameState.GetFlag("M0%Scene1%End"))
                GameObject.Find("Panel/ReturnToMap/Label").GetComponent<UnityEngine.UI.Text>().text = "Skip tutorial";
            Destroy(GameObject.Find("Panel/ReplayTutorial"));
            Destroy(GameObject.Find("Panel/ReplayEnding"));
        }
        else if (! gameState.GetFlag("Global%GameEnd"))
        {
            GameObject.Find("Panel/ReturnToMap/Label").GetComponent<UnityEngine.UI.Text>().text = "Replay tutorial";
            Destroy(GameObject.Find("Panel/ReplayTutorial"));
            Destroy(GameObject.Find("Panel/ReplayEnding"));
        }
        else
            Destroy(GameObject.Find("Panel/ReturnToMap"));

        GameObject.Find("Panel/LeftHandedMode").GetComponent<UnityEngine.UI.Toggle>().isOn = gameState.GetFlag("System%SwapButtonGroups");
        if (! DeviceInput.gyroPresent)
        {
            GameObject.Find("Panel/UseGyroscope").GetComponent<UnityEngine.UI.Toggle>().enabled = false;
            GameObject.Find("Panel/UseGyroscope/Label").GetComponent<UnityEngine.UI.Text>().color = Color.gray;
            GameObject.Find("Panel/UseGyroscope/Text").GetComponent<UnityEngine.UI.Text>().text = "Unable to detect gyroscope";
            GameObject.Find("Panel/UseGyroscope/Text").GetComponent<UnityEngine.UI.Text>().color = Color.red;
        }
        GameObject.Find("Panel/UseGyroscope").GetComponent<UnityEngine.UI.Toggle>().isOn = gameState.GetFlag("System%UseGyroscope");
        if (! DeviceInput.compassPresent)
        {
            GameObject.Find("Panel/UseCompass").GetComponent<UnityEngine.UI.Toggle>().enabled = false;
            GameObject.Find("Panel/UseCompass/Label").GetComponent<UnityEngine.UI.Text>().color = Color.gray;
            GameObject.Find("Panel/UseCompass/Text").GetComponent<UnityEngine.UI.Text>().text = "Unable to detect compass";
            GameObject.Find("Panel/UseCompass/Text").GetComponent<UnityEngine.UI.Text>().color = Color.red;
        }
        GameObject.Find("Panel/UseCompass").GetComponent<UnityEngine.UI.Toggle>().isOn = gameState.GetFlag("System%UseCompass");

#if ! UNITY_ANDROID
        // compass only supported on android
        Destroy(GameObject.Find("Panel/UseCompass"));
#endif
    }

    public void Credits()
    {
        buttonCanvas.HideOverlay();
        buttonCanvas.ShowCreditsOverlay();
    }

    public void LeftHandedMode()
    {
        bool isOn = GameObject.Find("Panel/LeftHandedMode").GetComponent<UnityEngine.UI.Toggle>().isOn;
        buttonCanvas.swapButtonGroups = isOn;
        gameState.SetFlag("System%SwapButtonGroups", isOn);
    }

    public void UseGyroscope()
    {
        bool isOn = GameObject.Find("Panel/UseGyroscope").GetComponent<UnityEngine.UI.Toggle>().isOn;
        DeviceInput.gyro = isOn;
        if (isOn)  // turning on gyroscope also turns off compass
        {
            GameObject.Find("Panel/UseCompass").GetComponent<UnityEngine.UI.Toggle>().isOn = false;
            DeviceInput.compass = false;
        }
        gameState.SetFlag("System%UseGyroscope", isOn);
    }

    public void UseCompass()
    {
        bool isOn = GameObject.Find("Panel/UseCompass").GetComponent<UnityEngine.UI.Toggle>().isOn;
        DeviceInput.compass = isOn;
        if (isOn)  // turning on compass also turns off gyro
        {
            GameObject.Find("Panel/UseGyroscope").GetComponent<UnityEngine.UI.Toggle>().isOn = false;
            DeviceInput.gyro = false;
        }
        gameState.SetFlag("System%UseCompass", isOn);
    }

    public void ExitGame()
    {
        buttonCanvas.HideOverlay();
        buttonCanvas.ShowQuestionOverlay("Are you sure you want to exit?",
                                         "Exit game",
                                         "Continue playing",
                                         delegate(string pressedButton)
        {
            buttonCanvas.HideOverlay();
            if (pressedButton == "Exit game")
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        });
    }

    public void ResetProgress()
    {
        buttonCanvas.HideOverlay();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (engine != null)
        {
            buttonCanvas.ShowQuestionOverlay("Since this is a development build, we will restart from the beginning of the current module.\n\nTesters: please test different ways to progress through the module!",
                                             "Reset",
                                             "Do not reset",
                                             delegate(string pressedButton)
            {
                buttonCanvas.HideOverlay();
                if (pressedButton == "Do not reset")
                    buttonCanvas.ShowOptionsOverlay();
                else
                {
                    gameState.ResetFlags(gameState.GetFlagsStartingWith(gameState.moduleName + "%"));
                    gameState.LoadScene("MapScene");
                    buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, null);
                    buttonCanvas.SetStatus(ButtonCanvasStatusType.ERROR, null);
                    buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, null);
                    buttonCanvas.SetFade(new Color(0, 0, 0, 0), 0);
                    buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
                    buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 0, null);
                    buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, null);
                    buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 2, null);
                    buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 0, null);
                    buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 1, null);
                    buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 2, null);
                    buttonCanvas.showDynamicGroup = false;
                    buttonCanvas.SetDialogue(null);
                }
            });
        }
#endif
        buttonCanvas.ShowQuestionOverlay("Are you sure you want to reset the game and restart from the very beginning?",
                                         "Reset",
                                         "Do not reset",
                                         delegate(string pressedButton)
        {
            buttonCanvas.HideOverlay();
            if (pressedButton == "Do not reset")
                buttonCanvas.ShowOptionsOverlay();
            else
            {
                buttonCanvas.ShowQuestionOverlay("Are you REALLY sure?\n\nAll game progress will be lost and you will start from the opening tutorial.",
                                                 "Do not reset",
                                                 "I'm sure, reset!",
                                                 delegate(string pressedButton2)
                {
                    buttonCanvas.HideOverlay();
                    if (pressedButton2 == "Do not reset")
                        buttonCanvas.ShowOptionsOverlay();
                    else
                    {
                        // Clear analytics dictionary when resetting the game
                        UnityAnalyticsIntegration.Reset ();

                        gameState.ResetFlags();
                        gameState.LoadScene("MapScene");
                        buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, null);
                        buttonCanvas.SetStatus(ButtonCanvasStatusType.ERROR, null);
                        buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, null);
                        buttonCanvas.SetFade(new Color(0, 0, 0, 0), 0);
                        buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
                        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 0, null);
                        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, null);
                        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 2, null);
                        buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 0, null);
                        buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 1, null);
                        buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 2, null);
                        buttonCanvas.showDynamicGroup = false;
                        buttonCanvas.SetDialogue(null);
                    }
                });
            }
        });
    }

    public void ReplayTutorial()
    {
        buttonCanvas.HideOverlay();
        gameState.SetFlag("M0%Scene1%End", false);
    }

    public void ReplayEnding()
    {
        buttonCanvas.HideOverlay();
        gameState.SetFlag("Global%GameEnd", false);
        gameState.SetFlag("Global%Module", gameState.GetFlagIntValue("Global%HighestModule"));
    }

    public void ReturnToMap()
    {
        if (engine == null)
        {
            // return to map was replaced by replay tutorial button
            ReplayTutorial();
            return;
        }

        buttonCanvas.HideOverlay();

        bool sceneFinished = gameState.GetFlag(gameState.sceneName + "%End")
                             || gameState.GetFlag("Global%Module" + gameState.moduleName.Substring(1) + "End");
        if (sceneFinished)
        {
            gameState.LoadScene("MapScene");
            buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, null);
            buttonCanvas.SetStatus(ButtonCanvasStatusType.ERROR, null);
            buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, null);
            buttonCanvas.SetFade(new Color(0, 0, 0, 0), 0);
            buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
            buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 0, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 2, null);
            buttonCanvas.showDynamicGroup = false;
            buttonCanvas.SetDialogue(null);
            return;
        }

        bool tutorial = (gameState.sceneName == "M0%Scene1" && ! gameState.GetFlag("M0%Scene1%End"));
        buttonCanvas.ShowQuestionOverlay(tutorial ? "Are you sure you want to skip the tutorial?\n\nYou can still access the tutorial in the future by tapping \"Replay tutorial\" in the Options screen."
                                                  : "You are not finished here yet! Are you sure you want to return to the map screen?\n\nYour current progress here will be saved.",
                                         tutorial ? "Skip tutorial" : "Return to map",
                                         "Continue playing",
                                         delegate(string pressedButton)
        {
            buttonCanvas.HideOverlay();
            if (pressedButton != "Continue playing")
            {
                if (tutorial)
                {
                    gameState.SetFlag("M0%Scene1%End", true);

                    // Send an analytics event when the tutorial is skipped
                    UnityAnalyticsIntegration.TutorialSkip (gameState, gameState.sceneName);
                }
                gameState.LoadScene("MapScene");
                buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, null);
                buttonCanvas.SetStatus(ButtonCanvasStatusType.ERROR, null);
                buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, null);
                buttonCanvas.SetFade(new Color(0, 0, 0, 0), 0);
                buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
                buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 0, null);
                buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, null);
                buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 2, null);
                buttonCanvas.showDynamicGroup = false;
                buttonCanvas.SetDialogue(null);
            }
        });
    }

    public void CloseOptions()
    {
        buttonCanvas.HideOverlay();
    }
}
