/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

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

    private static string status;
    private static float startTime;

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

        if (previousCardName == null)
            startTime = 0;

        Sprite background = Resources.Load<Sprite>("Cards/" + cardName);
        Sprite buttons = Resources.Load<Sprite>("Cards/" + cardName + "-buttons");
        if (buttons == null)
            buttons = background;
        transform.GetChild(0).GetComponent<UnityEngine.UI.Image>().overrideSprite = background;
        transform.GetChild(0).GetChild(0).GetChild(0).GetComponent<UnityEngine.UI.Image>().overrideSprite = buttons;

        if (tsv != null)
        {
            int count = 0;
            foreach (string card in tsv.Lookup())
            {
                foreach (string condition in tsv.Lookup(card))
                {
                    string flags = condition;
                    string newStatus = null;
                    if (condition.IndexOf('#') != -1)
                    {
                        flags = condition.Substring(0, condition.IndexOf('#'));
                        newStatus = condition.Substring(condition.IndexOf('#') + 1);
                    }
                    if (gameState.EvaluateFlags(flags))
                    {
                        if (newStatus != null && status != newStatus)
                        {
                            status = newStatus;
                            startTime = 0;
                        }

                        if (count > 0)
                            Instantiate(transform.GetChild(0).GetChild(0), transform.GetChild(0));

                        Transform t = transform.GetChild(0).GetChild(count);
                        t.gameObject.name = card;
                        int[] coords
                            = Array.ConvertAll<string, int>(tsv.Lookup(card, condition)[0].Split(','), int.Parse);
                        t.GetComponent<RectTransform>().anchoredPosition
                            = new Vector2(coords[0], 512 - coords[1] - coords[3]);
                        t.GetComponent<RectTransform>().sizeDelta
                            = new Vector2(coords[2], coords[3]);
                        t.GetChild(0).GetComponent<RectTransform>().anchoredPosition
                            = new Vector2(-coords[0], -(512 - coords[1] - coords[3]));
                        if (coords.Length > 4 && (coords[4] & 1) == 0)
                            t.GetChild(0).GetComponent<UnityEngine.UI.Image>().overrideSprite = background;
                        else
                            t.GetChild(0).GetComponent<UnityEngine.UI.Image>().overrideSprite = buttons;
                        if (coords.Length > 4 && (coords[4] & 2) != 0)
                            t.GetChild(0).gameObject.name = "Glow";
                        count++;
                        break;
                    }
                }
            }
        }
    }

    private void Update()
    {
        if (startTime == 0 && ! buttonCanvas.showLoading)
            startTime = Time.realtimeSinceStartup;

        float glowValue = Mathf.Abs((Time.realtimeSinceStartup - ((long) (Time.realtimeSinceStartup))) * 2f - 1f) * 0.33f + 0.67f;
        Color glowColor = new Color(glowValue, glowValue, glowValue);
        foreach (Transform child in transform.GetChild(0))
        {
            if (child.GetChild(0).gameObject.name == "Glow")
                child.GetChild(0).GetComponent<UnityEngine.UI.Image>().color = glowColor;
        }

        if (status != null)
        {
            if (startTime != 0 && ((int) (Time.realtimeSinceStartup - startTime)) % 30 <= 8)
                buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, status);
            else
                buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, null);
        }
    }

    public void ButtonPress()
    {
        GameObject obj = EventSystem.current.currentSelectedGameObject;
        if (obj == null)
            return;
        string nextCard = obj.name;
        buttonCanvas.HideOverlay();
        try
        {
            TSVLookup tsv = new TSVLookup("Cards/" + cardName);
            foreach (string condition in tsv.Lookup(nextCard))
            {
                string flags = condition;
                if (condition.IndexOf('#') != -1)
                    flags = condition.Substring(0, condition.IndexOf('#'));

                if (gameState.EvaluateFlags(flags))
                {
                    string coords = tsv.Lookup(nextCard, condition)[0];
                    string message = tsv.Lookup(nextCard, condition, coords)[0];

                    if (nextCard.Contains("%"))
                    {
                        // this card loads a level
                        gameState.SetFlag("Global%LeaveMapOption", int.Parse(message));
                        if (! gameState.GetFlag(nextCard + "%End"))
                            gameState.EncodeAnalyticsLeaveMap(int.Parse(message));
                        gameState.LoadARScene(nextCard);
                    }
                    else
                    {
                        // this card displays a dialogue and (potentially) resets to a module
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
                                variableValue = DeviceInput.HumanReadableEncoding(DeviceInput.deviceSerial);
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
                                    gameState.AppendToAnalyticsString("_" + nextCard[0]);
                                    gameState.ResetFlags(gameState.GetFlagsStartingWith("M"));
                                    gameState.SetFlag("Global%Module", int.Parse(nextCard));
                                    gameState.SetFlag("Global%ReplayModule", int.Parse(nextCard));
                                    gameState.SetFlag("Global%GameEnd", true);
                                }
                                else
                                    buttonCanvas.ShowCardOverlay(cardName, previousCardName);
                            }
                        );
                    }
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

        if (nextCard == "Options")
        {
            buttonCanvas.ShowOptionsOverlay(delegate()
            {
                startTime = 0;
                buttonCanvas.ShowCardOverlay(cardName, previousCardName);
            });
        }
        else if (nextCard == "Exit")
        {
            buttonCanvas.ShowQuestionOverlay("Are you sure you want to exit?",
                                             "Exit game",
                                             "Continue playing",
                                             delegate(string pressedButton)
            {
                buttonCanvas.HideOverlay();
                if (pressedButton == "Exit game")
                    DeviceInput.ExitGame(buttonCanvas);
                else
                {
                    startTime = 0;
                    buttonCanvas.ShowCardOverlay(cardName, previousCardName);
                }
            });
        }
        else if (nextCard != "Close")
            buttonCanvas.ShowCardOverlay(nextCard, cardName);
    }
}
