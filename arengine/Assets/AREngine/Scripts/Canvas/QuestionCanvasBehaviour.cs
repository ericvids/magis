/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using UnityEngine.EventSystems;

public class QuestionCanvasBehaviour : CanvasBehaviour
{
    public QuestionDelegate questionDelegate;

    void Update()
    {
        UnityEngine.UI.Text text = transform.GetChild(0).GetChild(0).GetComponent<UnityEngine.UI.Text>();
        while (text.preferredHeight > text.rectTransform.rect.height && text.fontSize > 10)
        {
            text.fontSize--;
        }
    }

    public void ButtonPress()
    {
        GameObject obj = EventSystem.current.currentSelectedGameObject;
        if (obj != null)
            questionDelegate(obj.transform.GetChild(0).gameObject.GetComponentInChildren<UnityEngine.UI.Text>().text);
    }
}
