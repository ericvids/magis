/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using System.Collections;

public class CreditsCanvasBehaviour : CanvasBehaviour
{
    public GameObject text;

    private void Start()
    {
        TextAsset asset = Resources.Load<TextAsset>("Credits");
        text.GetComponent<UnityEngine.UI.Text>().text = asset.text;
    }

    private void Update()
    {
        Vector2 pos = text.GetComponent<RectTransform>().anchoredPosition;
        pos.y += Time.deltaTime * 25.0f;
        if (pos.y > text.GetComponent<UnityEngine.UI.Text>().preferredHeight)
            Done();
        else
            text.GetComponent<RectTransform>().anchoredPosition = pos;
    }

    public void Done()
    {
        buttonCanvas.HideOverlay();
    }
}
