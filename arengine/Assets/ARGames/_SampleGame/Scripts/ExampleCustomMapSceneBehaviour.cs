/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;

// This is a demo map scene behaviour that may be used to replace the default map system.
// To use this instead of the default map system:
// 1. Create a new scene, and overwrite ARGames/_SampleGame/Scenes/MapScene.unity with it.
// 2. Select the Main Camera, then on the Inspector panel, right click on the Audio Listener and select "Remove Component".
// 3. While having the Main Camera selected, drag this file into the bottom of the Inspector panel.
public class ExampleCustomMapSceneBehaviour : SceneBehaviour
{
    private void Start()
    {
        // do not remove
        if (! Init())
            return;

        // set the color of the global interface
        buttonCanvas.SetColors(new Color(0.7f, 0.9f, 1.0f), new Color(0.8f, 0.925f, 1.0f));

        // you can omit the rest of the buttonCanvas code below if you do not want to use buttonCanvas

        // static buttons are always there
        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 0, "Options");
        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, null);
        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 2, null);

        // dynamic buttons emanate from some point on the screen (when they are shown using showDynamicGroup)
        buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 0, "Enter", "Tutorial");
        buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 1, "Enter", "Level 1");
        buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 2, "Enter", "Level 2");
        buttonCanvas.SetDynamicGroupOrigin(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0.0f));
        buttonCanvas.showDynamicGroup = true;
    }

    private void Update()
    {
        // do not remove
        if (! Init())
            return;

        if (buttonCanvas.pressedButton == "Options")
        {
            // if you don't want to use buttonCanvas, at least put a custom way to show the following options dialog
            buttonCanvas.ShowOptionsOverlay();
        }
        else if (buttonCanvas.pressedButton == "Enter")
        {
            // gameState will not start loading until the dynamic button group is invisible
            buttonCanvas.showDynamicGroup = false;

            // use gameState to load an AR scene; an AR scene is comprised of the following files:
            // - Scenes/M1%Scene1.unity (must be in the list of scenes to build in File->Build Settings)
            // - Resources/SceneTSVs/M1%Scene1.txt
            // - MarkerStatuses/M1%Scene1.jpg or .png (set the texture type as Sprite)
            if (buttonCanvas.pressedButtonExtension == "Tutorial")
                gameState.LoadARScene("M0%Scene1");
            if (buttonCanvas.pressedButtonExtension == "Level 1")
                gameState.LoadARScene("M1%Scene1");
            if (buttonCanvas.pressedButtonExtension == "Level 2")
                gameState.LoadARScene("M2%Scene1");
        }
    }
}
