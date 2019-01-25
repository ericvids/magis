/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;

public class CardCanvasBehaviour : CanvasBehaviour
{
    public string cardName;
    public string previousCardName;

    private GameStateBehaviour gameState;

    private void Start()
    {
        gameState = GameObject.Find("GameState").GetComponent<GameStateBehaviour>();
        TSVLookup tsv = null;
        try
        {
            tsv = new TSVLookup("Cards/" + cardName);
        }
        catch (NullReferenceException)
        {
            if (previousCardName != null)
                transform.GetChild(0).GetChild(0).gameObject.name = previousCardName;
        }

        Sprite background = Resources.Load<Sprite>("Cards/" + cardName);
        Sprite buttons = Resources.Load<Sprite>("Cards/" + cardName + "-buttons");
        if (buttons == null)
            buttons = background;
        transform.GetChild(0).GetComponent<UnityEngine.UI.Image>().sprite = background;
        transform.GetChild(0).GetChild(0).GetChild(0).GetComponent<UnityEngine.UI.Image>().sprite = buttons;

        if (tsv != null)
        {
            int count = 0;
            foreach (string card in tsv.Lookup())
            {
                string flags = tsv.Lookup(card)[0];
                if (gameState.EvaluateFlags(flags))
                {
                    if (count > 0)
                        Instantiate(transform.GetChild(0).GetChild(0), transform.GetChild(0));

                    Transform t = transform.GetChild(0).GetChild(count);
                    t.gameObject.name = card;
                    int[] coords
                        = Array.ConvertAll<string, int>(tsv.Lookup(card, flags)[0].Split(','), int.Parse);
                    t.GetComponent<RectTransform>().anchoredPosition
                        = new Vector2(coords[0], 512 - coords[1] - coords[3]);
                    t.GetComponent<RectTransform>().sizeDelta
                        = new Vector2(coords[2], coords[3]);
                    t.GetChild(0).GetComponent<RectTransform>().anchoredPosition
                        = new Vector2(-coords[0], -(512 - coords[1] - coords[3]));
                    count++;
                }
            }
        }
    }

    public void Click()
    {
        string nextCard = EventSystem.current.currentSelectedGameObject.name;
        buttonCanvas.HideOverlay();
        if (nextCard != "Close")
            buttonCanvas.ShowCardOverlay(nextCard, cardName);
    }
}
