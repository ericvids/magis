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
                foreach (string flags in tsv.Lookup(card))
                {
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
                        if (coords.Length > 4 && coords[4] == 0)
                            t.GetChild(0).GetComponent<UnityEngine.UI.Image>().sprite = background;
                        count++;
                        break;
                    }
                }
            }
        }
    }

    public void Click()
    {
        string nextCard = EventSystem.current.currentSelectedGameObject.name;
        buttonCanvas.HideOverlay();
        try
        {
            TSVLookup tsv = new TSVLookup("Cards/" + cardName);
            foreach (string flags in tsv.Lookup(nextCard))
            {
                if (gameState.EvaluateFlags(flags))
                {
                    string coords = tsv.Lookup(nextCard, flags)[0];
                    string message = tsv.Lookup(nextCard, flags, coords)[0];
                    message = message.Replace("\\n", "\n");
                    while (message.IndexOf('{') != -1)
                    {
                        int i = message.IndexOf('{');
                        int j = i + 1;
                        while (j < message.Length && message[j] != '}')
                            j++;
                        string variableName = message.Substring(i + 1, j - i - 1);
                        string variableValue;
                        if (variableName == "SERIAL")
                            variableValue = DeviceInput.deviceSerial;
                        else
                            variableValue = "" + gameState.GetFlagIntValue(variableName);
                        if (j != message.Length)
                            j++;
                        message = message.Substring(0, i) + variableValue + message.Substring(j);
                    }
                    buttonCanvas.ShowQuestionOverlay(
                        message,
                        nextCard[0] >= '0' && nextCard[0] <= '9' ? "Proceed" : "OK",
                        nextCard[0] >= '0' && nextCard[0] <= '9' ? "Don't Proceed" : null,
                        delegate(string pressedButton)
                        {
                            buttonCanvas.HideOverlay();
                            if (pressedButton == "Proceed")
                            {
                                gameState.ResetFlags(gameState.GetFlagsStartingWith("M"));
                                gameState.SetFlag("Global%Module", int.Parse(nextCard));
                                gameState.SetFlag("Global%ReplayModule", int.Parse(nextCard));
                                gameState.SetFlag("Global%GameEnd", true);
                            }
                            else
                                buttonCanvas.ShowCardOverlay(cardName, previousCardName);
                        }
                    );
                    return;
                }
            }
        }
        catch (NullReferenceException)
        {
        }
        catch (UnityException)
        {
        }
        if (nextCard != "Close")
            buttonCanvas.ShowCardOverlay(nextCard, cardName);
    }
}
