/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

public class TitleSceneBehaviour : SceneBehaviour
{
    private bool done;

    private void Start()
    {
        if (! Init())
            return;
        GameObject.Find("ScreenCanvas/Title").GetComponent<UnityEngine.UI.Image>().overrideSprite = Resources.Load<Sprite>("TitleScreen");
        StartCoroutine(Stream());
    }

    public void ButtonPress()
    {
        if (! Init())
            return;

        GameObject obj = EventSystem.current.currentSelectedGameObject;
        if (obj.name == "Title" && done && ! buttonCanvas.overlayShowing)
        {
#if DEVELOPMENT_BUILD && ! UNITY_EDITOR
            buttonCanvas.ShowQuestionOverlay(
                "THIS IS A DEVELOPMENT BUILD OF THE GAME.\n\nThis version of the game is for testing and evaluation purposes only. Please do not distribute.",
                "Proceed to game",
                null,
                delegate(string pressedButton)
                {
                    buttonCanvas.HideOverlay();
#endif
                    buttonCanvas.ShowBattery();
                    gameState.LoadScene("MapScene");
#if DEVELOPMENT_BUILD && ! UNITY_EDITOR
                }
            );
#endif
        }
    }

    private IEnumerator Stream()
    {
#if UNITY_ANDROID && ! UNITY_EDITOR && MAGIS_OBB
        if (! Directory.Exists(Application.persistentDataPath + "/Vuforia"))
            Directory.CreateDirectory(Application.persistentDataPath + "/Vuforia");
        yield return StartCoroutine(StreamFromOBB("/Vuforia/magis-default.xml", true));
        yield return StartCoroutine(StreamFromOBB("/Vuforia/magis-default.dat", true));
        yield return StartCoroutine(StreamFromOBB("/Vuforia/" + DeviceInput.GameName() + ".xml"));
        yield return StartCoroutine(StreamFromOBB("/Vuforia/" + DeviceInput.GameName() + ".dat"));
#endif
        done = true;
        yield return null;
    }

    private IEnumerator StreamFromOBB(string file, bool doNotIgnore = false)
    {
        UnityWebRequest www = UnityWebRequest.Get(Application.streamingAssetsPath + file);
        yield return www.SendWebRequest();
        if (string.IsNullOrEmpty(www.error))
        {
            try
            {
                File.WriteAllBytes(Application.persistentDataPath + file, www.downloadHandler.data);
            }
            catch
            {
                buttonCanvas.ShowQuestionOverlay("The application failed to extract required game files. Please free up some space on your device. (If this fails, you might need to uninstall then re-install the application.)",
                                                 "Exit game",
                                                 null,
                                                 delegate(string pressedButton)
                {
                    buttonCanvas.HideOverlay();
                    DeviceInput.ExitGame(buttonCanvas);
                });
            }
        }
        else if (doNotIgnore)
        {
            buttonCanvas.ShowQuestionOverlay("The application failed to download required game files. Please uninstall then re-install the application.",
                                             "Exit game",
                                             null,
                                             delegate(string pressedButton)
            {
                buttonCanvas.HideOverlay();
                DeviceInput.ExitGame(buttonCanvas);
            });
        }
        yield return null;
    }
}
