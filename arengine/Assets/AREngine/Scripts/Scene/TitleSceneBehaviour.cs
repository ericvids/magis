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
    private void Start()
    {
        if (! Init())
            return;
        GameObject.Find("ScreenCanvas/Title").GetComponent<UnityEngine.UI.Image>().overrideSprite = Resources.Load<Sprite>("TitleScreen");
    }

    public void ButtonPress()
    {
        if (! Init())
            return;

        GameObject obj = EventSystem.current.currentSelectedGameObject;
        if (obj.name == "Title" && ! buttonCanvas.overlayShowing)
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
                    DeviceInput.RequestCameraPermission();
                    buttonCanvas.ShowBattery();
                    gameState.LoadScene("MapScene");
#if DEVELOPMENT_BUILD && ! UNITY_EDITOR
                }
            );
#endif
        }
    }
}
