/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public enum ButtonCanvasGroup
{
    STATIC,
    DYNAMIC,
    NUM_GROUPS
}

public enum ButtonCanvasStatusType
{
    PROGRESS = 0,
    TIP,
    ERROR,
    NUM_STATUS_TYPES
}

public delegate void QuestionDelegate(string pressedButton);
public delegate void CloseDelegate();

public class ButtonCanvasBehaviour : MonoBehaviour
{
    // number of actual displayed frames before diagnostic messages may show up
    public static float COOL_OFF_TIME_AFTER_LOAD_SCENE = 5.0f;

    // game objects for assignment into the editor
    public GameObject[] crosshair;
    public GameObject still;
    public GameObject fader;
    public GameObject status;
    public GameObject statusImage;
    public GameObject staticPanel;
    public GameObject dynamicPanel;
    public GameObject dialogue;
    public GameObject loading;
    public GameObject battery;

    public Transform optionsCanvas;
    public Transform questionCanvas;
    public Transform creditsCanvas;
    public Transform cardCanvas;

    public Transform overlayCanvas;
    public Transform oldOverlayCanvas;

    public Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    // messages
    private float timeSinceSceneStart;
    private bool noRotationShown;
    private bool accelerometerMalfunctioningShown;
    private bool locationPermissionDisabledShown;
    private bool locationHardwareDisabledShown;

    // crosshair
    public const int NUM_CROSSHAIR_FRAMES = 9;     // number of frames the crosshair will animate
    private float crossX = -1f, crossY = -1f, crossWidth = 0f, crossHeight = 0f;

    // fader
    private Color faderTargetColor;
    private float faderTime;
    private bool faderSuppressingText;
    private bool faderTurningToStill;

    // status
    public const float STATUS_BASE = 42.0f;    // position of status
    public const float STATUS_OFFSET = 30.0f;  // Y units to move status into view when it is showing
    public const float STATUS_FACTOR = 90.0f;  // amount to multiply to Time.deltaTime for offsetting the status for the next frame
    private Color[] statusColor = new [] {     // background colors for each of the status types
        new Color(0.0f, 0.25f, 0.5f, 0.875f),
        new Color(0.5f, 0.25f, 0.0f, 0.875f),
        new Color(0.5f, 0.0f, 0.0f, 0.875f)
    };
    private string[] statusString = new string[(int) ButtonCanvasStatusType.NUM_STATUS_TYPES];
    private string[] queuedStatusString = new string[(int) ButtonCanvasStatusType.NUM_STATUS_TYPES];
    private float statusTime;

    // buttons
    private Color buttonColor = Color.white;
    private Color panelColor = Color.white;
    public const int NUM_BUTTONS_PER_GROUP = 3;   // number of buttons on each side of the screen
    public const float BUTTON_ZOOM_TIME = 0.33f;  // amount of time to zoom buttons in seconds
    private static bool _swapButtonGroups;
    private string _pressedButton;
    private string _pressedButtonExtension;
    private bool pressedButtonIsNew;
    private bool[,] buttonGlow = new bool[(int) ButtonCanvasGroup.NUM_GROUPS, NUM_BUTTONS_PER_GROUP];
    private float fromX, fromY;
    private float currentZoomTime;

    // dialogue
    public const float CHARACTERS_PER_SECOND = 120.0f;  // number of characters to show per second (cps)
    public const float TEXT_SPEEDUP_FACTOR = 12.0f;     // factor to multiply to cps when user presses the dialogue button in advance
    public const float DIALOGUE_DELAY = 0.25f;          // number of seconds before user is allowed to tap the dialogue button
    public const float DIALOGUE_LINGERING = 10.0f;      // number of seconds before user is warned that he's taking too long
    private string text = "";
    private float textDisplayedFinalLength;
    private float textDisplayedLength;
    private float textInternalLength;
    private float textDelay;
    private bool advancing;

    // music
    private Dictionary<string, AudioClip> preloadedClips = new Dictionary<string, AudioClip>();
    private string lastMusic = null;
    private float volumeDecreasePerSecond = 0.0f;

    public bool readyToLoadLevel
    {
        get
        {
            return ! showDynamicGroup && ! dialogueShowing && ! overlayShowing && oldOverlayCanvas == null;
        }
    }

    public void SetCrosshair(Vector3 lowerLeft, Vector3 upperRight)
    {
        crossX = (lowerLeft.x / Screen.width);
        crossY = (lowerLeft.y / Screen.height);
        crossWidth = ((upperRight.x - lowerLeft.x) / Screen.width);
        crossHeight = ((upperRight.y - lowerLeft.y) / Screen.height);
    }

    private bool stillEnabled
    {
        get
        {
            return still.GetComponent<UnityEngine.UI.Image>().enabled;
        }
        set
        {
            still.GetComponent<UnityEngine.UI.Image>().enabled = value;
        }
    }

    private bool faderEnabled
    {
        get
        {
            return fader.GetComponent<UnityEngine.UI.Image>().enabled;
        }
        set
        {
            fader.GetComponent<UnityEngine.UI.Image>().enabled = value;
        }
    }

    public bool faderActive
    {
        get
        {
            return faderTime > float.Epsilon;
        }
    }

    public bool SetStill(string stillImage)
    {
        faderTurningToStill = false;
        if (faderTargetColor.a != 0)
        {
            // direct change to still
            if (stillImage == null)
                stillEnabled = false;
            else
            {
                still.GetComponent<UnityEngine.UI.Image>().overrideSprite = Resources.Load<Sprite>(stillImage);
                stillEnabled = true;
            }
            return true;
        }
        else
        {
            if (stillImage == null)
            {
                if (! stillEnabled)
                    return true;
                fader.GetComponent<UnityEngine.UI.Image>().overrideSprite = still.GetComponent<UnityEngine.UI.Image>().overrideSprite;
                fader.GetComponent<UnityEngine.UI.Image>().color = Color.white;
                faderTargetColor = new Color(0, 0, 0, 0);
                stillEnabled = false;
            }
            else
            {
                fader.GetComponent<UnityEngine.UI.Image>().overrideSprite = Resources.Load<Sprite>(stillImage);
                fader.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0);
                faderTargetColor = Color.white;
                faderTurningToStill = true;
            }
            faderEnabled = true;
            faderSuppressingText = true;
            faderTime = 0.25f;
            return false;
        }
    }

    public void SetFade(Color targetColor, float time)
    {
        faderTurningToStill = false;
        fader.GetComponent<UnityEngine.UI.Image>().overrideSprite = null;
        faderTargetColor = targetColor;
        if (time == 0)
        {
            fader.GetComponent<UnityEngine.UI.Image>().color = faderTargetColor;
            faderEnabled = (faderTargetColor.a != 0);
        }
        else
        {
            faderEnabled = true;
            faderSuppressingText = true;
        }
        faderTime = time;
    }

    public void SetStatus(ButtonCanvasStatusType whichStatusType, string status)
    {
        if (status == "")
            status = null;
        queuedStatusString[(int) whichStatusType] = status;
    }

    public void SetColors(Color button, Color panel)
    {
        buttonColor = button;
        panelColor = panel;
    }

    public bool swapButtonGroups
    {
        get
        {
            return _swapButtonGroups;
        }
        set
        {
            if (_swapButtonGroups != value)
            {
                for (int i = 0; i < NUM_BUTTONS_PER_GROUP; i++)
                {
                    GameObject objStatic = staticPanel.transform.GetChild(i).gameObject;
                    GameObject objDynamic = dynamicPanel.transform.GetChild(i).gameObject;
                    bool active = objStatic.activeSelf;
                    objStatic.SetActive(objDynamic.activeSelf);
                    objDynamic.SetActive(active);
                    Sprite sprite = objStatic.GetComponent<UnityEngine.UI.Image>().overrideSprite;
                    objStatic.GetComponent<UnityEngine.UI.Image>().overrideSprite = objDynamic.GetComponent<UnityEngine.UI.Image>().overrideSprite;
                    objDynamic.GetComponent<UnityEngine.UI.Image>().overrideSprite = sprite;
                    string buttonName = objStatic.transform.GetChild(0).GetComponent<UnityEngine.UI.Text>().text;
                    objStatic.transform.GetChild(0).GetComponent<UnityEngine.UI.Text>().text = objDynamic.transform.GetChild(0).GetComponent<UnityEngine.UI.Text>().text;
                    objDynamic.transform.GetChild(0).GetComponent<UnityEngine.UI.Text>().text = buttonName;
                }
                _swapButtonGroups = value;
                GameObject panel = staticPanel;
                staticPanel = dynamicPanel;
                dynamicPanel = panel;
            }
        }
    }

    public void SetButton(ButtonCanvasGroup whichGroup, int whichButton, string buttonName, string buttonNameExtension = null, bool glow = false)
    {
        GameObject obj = (whichGroup == ButtonCanvasGroup.STATIC ? staticPanel : dynamicPanel).transform.GetChild(whichButton).gameObject;
        if (buttonNameExtension == null)
            buttonNameExtension = "";
        else
            buttonNameExtension = ((char) 160) + buttonNameExtension;
        if (buttonName == null)
            obj.SetActive(false);
        else
        {
            obj.SetActive(true);
            if (! sprites.ContainsKey(buttonName))
                sprites.Add(buttonName, Resources.Load<Sprite>("Buttons/" + buttonName));
            obj.GetComponent<UnityEngine.UI.Image>().overrideSprite = sprites[buttonName];
            obj.transform.GetChild(0).GetComponent<UnityEngine.UI.Text>().text = buttonName + buttonNameExtension;
        }
        buttonGlow[(int) whichGroup, whichButton] = glow;
    }

    public void SetDynamicGroupOrigin(Vector3 origin)
    {
        fromX = (origin.x / Screen.width);
        fromY = (origin.y / Screen.height);
    }

    public bool showDynamicGroup
    {
        get
        {
            return currentZoomTime != 0;
        }
        set
        {
            if (value)
            {
                if (currentZoomTime == 0)
                    currentZoomTime = Time.fixedDeltaTime;
            }
            else
            {
                if (currentZoomTime > 0)
                    currentZoomTime = -currentZoomTime + Time.fixedDeltaTime;
            }
        }
    }

    public string pressedButton
    {
        get
        {
            // if a pressed button is being queried by some other script,
            // the button is "consumed" immediately
            pressedButtonIsNew = false;
            return _pressedButton;
        }
    }

    public string pressedButtonExtension
    {
        get
        {
            // if a pressed button is being queried by some other script,
            // the button is "consumed" immediately
            pressedButtonIsNew = false;
            return _pressedButtonExtension;
        }
    }

    public bool dialogueShowing
    {
        get
        {
            return dialogue.GetComponent<CanvasGroup>().alpha > 0.0f;
        }
    }

    public bool dialogueLingering
    {
        get
        {
            return textDelay >= DIALOGUE_LINGERING;
        }
    }

    public void SetDialogue(string text)
    {
        faderSuppressingText = false;
        if (text == null)
            text = "";
        else
            text = text.Replace("\\n", "\n");

        int start = text.IndexOf("#");
        string speaker = "";
        if (start != -1 && (start == 0 || text[start - 1] != '\\'))
            speaker = text.Substring(0, start);
        else
            start = -1;
        this.text = CenterText(InsertNewlines(text.Substring(start + 1).Replace("\\#", "#")));
        textDisplayedFinalLength = this.text.Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "").Replace("\u200a", "").Length;
        textDisplayedLength = 0;
        textInternalLength = 0;
        textDelay = 0;

        dialogue.transform.GetChild(1).gameObject.GetComponent<UnityEngine.UI.Text>().text = "";
        dialogue.transform.GetChild(2).gameObject.GetComponent<UnityEngine.UI.Text>().text = speaker;

        if (start == -1)  // if there's no #, make the dialogue transparent
            dialogue.transform.GetChild(0).gameObject.GetComponent<UnityEngine.UI.Image>().color = new Color(panelColor.r / 2.0f, panelColor.g / 2.0f, panelColor.b / 2.0f, 0.75f);
        else              // # will make the dialogue opaque even if the speaker is ""
            dialogue.transform.GetChild(0).gameObject.GetComponent<UnityEngine.UI.Image>().color = new Color(panelColor.r / 2.0f, panelColor.g / 2.0f, panelColor.b / 2.0f, 0.95f);

        if (text != "")
        {
            advancing = false;
            Update();  // mask any changes to buttons if we are going to show a dialogue
        }
    }

    private void InstantiateCanvas(Transform canvas)
    {
        overlayCanvas = Instantiate(canvas);
        overlayCanvas.GetComponentInChildren<CanvasGroup>().interactable = false;
        overlayCanvas.GetComponentInChildren<CanvasGroup>().alpha = 0.0f;
        if (canvas == creditsCanvas)
        {
            overlayCanvas.GetComponentInChildren<UnityEngine.UI.Image>().color = new Color(panelColor.r / 4.0f, panelColor.g / 4.0f, panelColor.b / 4.0f, 0.75f);
        }
        else if (canvas != cardCanvas)
        {
            foreach (var component in overlayCanvas.GetComponentsInChildren<UnityEngine.UI.Image>())
            {
                if (component.gameObject.name != "AppIcon")
                    component.color = new Color(panelColor.r * 0.9f, panelColor.g * 0.9f, panelColor.b * 0.9f);
            }
            overlayCanvas.GetComponentInChildren<UnityEngine.UI.Image>().color = panelColor;
        }
        overlayCanvas.GetChild(0).localScale = new Vector3(0.8f, 0.8f, 1.0f);
        overlayCanvas.GetComponent<CanvasBehaviour>().SetButtonCanvas(this);
    }

    public void ShowOptionsOverlay(CloseDelegate closeDelegate = null)
    {
        if (overlayCanvas != null)
            return;

        InstantiateCanvas(optionsCanvas);
        overlayCanvas.GetComponent<OptionsCanvasBehaviour>().closeDelegate = closeDelegate;
    }

    public bool ShowQuestionOverlay(string question, string button1, string button2, QuestionDelegate questionDelegate)
    {
        if (overlayCanvas != null)
            return false;

        InstantiateCanvas(questionCanvas);
        overlayCanvas.GetComponent<QuestionCanvasBehaviour>().questionDelegate = questionDelegate;
        overlayCanvas.GetChild(0).GetChild(0).GetComponent<UnityEngine.UI.Text>().text = question;
        overlayCanvas.GetChild(0).GetChild(1).GetChild(0).GetComponent<UnityEngine.UI.Text>().text = button1;
        if (button2 != null)
            overlayCanvas.GetChild(0).GetChild(2).GetChild(0).GetComponent<UnityEngine.UI.Text>().text = button2;
        else
        {
            overlayCanvas.GetChild(0).GetChild(1).localPosition =
                (overlayCanvas.GetChild(0).GetChild(1).localPosition + overlayCanvas.GetChild(0).GetChild(2).localPosition) / 2;
            Destroy(overlayCanvas.GetChild(0).GetChild(2).gameObject);
        }
        return true;
    }

    public void ShowCreditsOverlay(CloseDelegate closeDelegate = null)
    {
        if (overlayCanvas != null)
            return;

        InstantiateCanvas(creditsCanvas);
        overlayCanvas.GetComponent<CreditsCanvasBehaviour>().closeDelegate = closeDelegate;
    }

    public void ShowCardOverlay(string cardName, string previousCardName = null)
    {
        if (overlayCanvas != null)
            return;

        InstantiateCanvas(cardCanvas);
        overlayCanvas.GetComponent<CardCanvasBehaviour>().cardName = cardName;
        overlayCanvas.GetComponent<CardCanvasBehaviour>().previousCardName = previousCardName;
    }

    public void HideOverlay()
    {
        if (overlayCanvas != null)
        {
            if (oldOverlayCanvas != null)
                Destroy(oldOverlayCanvas.gameObject);  // if we hide the next dialog too fast, destroy the older one quick
            oldOverlayCanvas = overlayCanvas;
            oldOverlayCanvas.GetComponentInChildren<CanvasGroup>().interactable = false;
        }
        overlayCanvas = null;
    }

    public bool overlayShowing
    {
        get
        {
            return overlayCanvas != null;
        }
    }

    public bool showLoading
    {
        get
        {
            return loading.activeSelf;
        }
        set
        {
            loading.SetActive(value);
            if (loading.activeSelf)
                timeSinceSceneStart = 0.0f;
        }
    }

    public void ShowBattery()
    {
        battery.SetActive(true);
    }

    private float LineHeight(string text)
    {
        UnityEngine.UI.Text t = dialogue.transform.GetChild(1).gameObject.GetComponent<UnityEngine.UI.Text>();
        t.text = text;
        return t.preferredHeight;
    }

    private string InsertNewlines(string text)
    {
        string[] words = text.Split(' ');

        string current = null;
        foreach (string word in words)
        {
            if (current == null)
                current = word;
            else
            {
                string option1 = current + " " + word;
                string option2 = current + "\n" + word;
                if (LineHeight(option1) < LineHeight(option2))
                    current = option1;
                else
                    current = option2;
            }
        }

        return current;
    }

    private string CenterText(string text)
    {
        string result = "";
        string[] lines = text.Split('\n');
        foreach (string line in lines)
        {
            string newline;
            if (line.Length > 0 && line[0] == '=')
            {
                newline = line.Substring(1);
                while (true)
                {
                    string option1 = newline.Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "");
                    string option2 = "\u200a" + option1 + "\u200a";
                    if (LineHeight(option1) < LineHeight(option2))
                        break;
                    newline = "\u200a" + newline + "\u200a";
                }
            }
            else
                newline = line;

            if (result == "")
                result = newline;
            else
                result = result + "\n" + newline;
        }
        return result;
    }

    private string Colorize(string text, string open, string close, string color)
    {
        // close up any open brackets
        if (open.Length != close.Length)
            throw new UnityException("Opening tag must be of equal length with closing tag");
        string currentText = text;
        int numOpen = currentText.Length - currentText.Replace(open, "").Length;
        int numClose = currentText.Length - currentText.Replace(close, "").Length;
        while (numOpen > numClose)
        {
            currentText += "</color>";
            numOpen -= open.Length;
        }
        currentText = currentText.Replace(open, "<color=" + color + ">").Replace(close, "</color>");
        return currentText;
    }

    private void Awake()
    {
        SetButton(ButtonCanvasGroup.STATIC, 0, null);
        SetButton(ButtonCanvasGroup.STATIC, 1, null);
        SetButton(ButtonCanvasGroup.STATIC, 2, null);
        stillEnabled = false;
        faderEnabled = false;
        dialogue.GetComponent<CanvasGroup>().alpha = 0.0f;
    }

    private void MessageResponse(string pressedButton)
    {
        HideOverlay();
        if (pressedButton == "Exit game")
            DeviceInput.ExitGame(this);
        else if (pressedButton == "Take me to Settings")
            DeviceInput.ShowLocationAccess();
    }

    private void UpdateMessages()
    {
        timeSinceSceneStart += Time.deltaTime;
        if (timeSinceSceneStart <= COOL_OFF_TIME_AFTER_LOAD_SCENE
            || overlayCanvas != null
            || oldOverlayCanvas != null
            || GameObject.FindWithTag("MainCamera") == null
            || showLoading)
        {
            return;
        }

#if ! UNITY_EDITOR
        if (! noRotationShown && ! DeviceInput.compassPresent && ! DeviceInput.gyroPresent)
        {
            noRotationShown = true;
            ShowQuestionOverlay("It looks like your device does not have a rotation sensor (gyroscope and/or digital compass).\n\nIt is highly recommended to play this game on a higher-end device for the best experience.",
                                "Exit game",
                                "Continue playing",
                                MessageResponse);
        }
        else if (! accelerometerMalfunctioningShown && DeviceInput.accelerometerMalfunctioning)
        {
            accelerometerMalfunctioningShown = true;
            ShowQuestionOverlay("It looks like your device's accelerometer is malfunctioning.\n\nYour AR experience may be adversely affected. Please contact your mobile dealer for repair.",
                                "Exit game",
                                "Continue playing",
                                MessageResponse);
        }
        else if (! locationPermissionDisabledShown && ! DeviceInput.locationPermissionEnabled)
        {
            locationPermissionDisabledShown = true;
            ShowQuestionOverlay("This game requires your device's location during play.\n\nPlease ensure that you have given this app permission to access your Location in your device's Settings.",
                                "Take me to Settings",
                                "Continue playing",
                                MessageResponse);
        }
        else if (! locationHardwareDisabledShown && ! DeviceInput.locationHardwareEnabled)
        {
            locationHardwareDisabledShown = true;
# if ! MAGIS_NOGPS
            ShowQuestionOverlay("Your device's location hardware has been disabled (or has not been found).\n\nPlease enable Location in your device's Settings, and also ensure that \"high-accuracy\" mode (GPS) is enabled.",
                                "Take me to Settings",
                                "Continue playing",
                                MessageResponse);
# elif MAGIS_BLE
            ShowQuestionOverlay("Your device's location hardware has been disabled (or has not been found).\n\nPlease enable Location in your device's Settings.",
                                "Take me to Settings",
                                "Continue playing",
                                MessageResponse);
# endif
        }
#endif
    }

    private void UpdateOverlay()
    {
        if (oldOverlayCanvas != null)
        {
            Vector3 scale = oldOverlayCanvas.GetChild(0).localScale;
            scale.x -= Time.deltaTime;
            scale.y -= Time.deltaTime;
            oldOverlayCanvas.GetChild(0).localScale = scale;
            oldOverlayCanvas.GetComponentInChildren<CanvasGroup>().alpha -= Time.deltaTime * 5;
            if (scale.x < 0.8f)
            {
                Destroy(oldOverlayCanvas.gameObject);
                oldOverlayCanvas = null;
            }
        }
        if (overlayCanvas != null)
        {
            Vector3 scale = overlayCanvas.GetChild(0).localScale;
            scale.x += Time.deltaTime;
            scale.y += Time.deltaTime;
            overlayCanvas.GetComponentInChildren<CanvasGroup>().alpha += Time.deltaTime * 5;
            if (scale.x > 1.0f)
            {
                scale.x = 1.0f;
                scale.y = 1.0f;
                overlayCanvas.GetComponentInChildren<CanvasGroup>().alpha = 1.0f;
                overlayCanvas.GetComponentInChildren<CanvasGroup>().interactable = true;
            }
            overlayCanvas.GetChild(0).localScale = scale;
        }
    }

    private void UpdateCrosshair()
    {
        float glowValue = Mathf.Abs((Time.realtimeSinceStartup / 0.5f - ((long) (Time.realtimeSinceStartup / 0.5f))) * 2f - 1f) * 0.5f + 0.5f;
        Color glowColor = new Color(glowValue, glowValue, glowValue);
        int frame = (int) (currentZoomTime / BUTTON_ZOOM_TIME * NUM_CROSSHAIR_FRAMES);
        for (int i = 0; i < crosshair.Length; i++)
        {
            crosshair[i].SetActive(overlayCanvas == null);
            float offset = 0.125f - (((float) frame + i) / NUM_CROSSHAIR_FRAMES * 0.125f);
            if (offset < 0 || currentZoomTime < 0 || crossWidth == 0 || crossHeight == 0 || frame + i > NUM_CROSSHAIR_FRAMES)
                offset = 0;
            crosshair[i].GetComponent<RectTransform>().anchoredPosition3D = new Vector3((crossX - offset) * GetComponent<RectTransform>().rect.width,
                                                                                        (crossY - offset) * GetComponent<RectTransform>().rect.height,
                                                                                        0.0f);
            crosshair[i].GetComponent<RectTransform>().sizeDelta = new Vector2((crossWidth + 2 * offset) * GetComponent<RectTransform>().rect.width,
                                                                               (crossHeight + 2 * offset) * GetComponent<RectTransform>().rect.height);

            crosshair[i].GetComponent<UnityEngine.UI.Image>().color = glowColor;
        }
    }

    private void UpdateFader()
    {
        if (faderTime == float.Epsilon)
            faderTime = 0;
        else if (faderTime > float.Epsilon)
        {
            Color nextColor = faderTargetColor;
            if (Time.deltaTime < faderTime)
                nextColor = Color.Lerp(fader.GetComponent<UnityEngine.UI.Image>().color, faderTargetColor, Time.deltaTime / faderTime);
            fader.GetComponent<UnityEngine.UI.Image>().color = nextColor;
            faderTime -= Time.deltaTime;
            if (faderTime <= float.Epsilon)
            {
                faderTime = float.Epsilon;
                if (faderTurningToStill)
                {
                    faderTurningToStill = false;
                    still.GetComponent<UnityEngine.UI.Image>().overrideSprite = fader.GetComponent<UnityEngine.UI.Image>().overrideSprite;
                    stillEnabled = true;
                    faderTargetColor = fader.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0);
                }
                if (faderTargetColor.a == 0)
                    faderEnabled = false;
            }
        }
    }

    private void UpdateStatus()
    {
        // do not show status image until it is proven that we do need to show it
        statusImage.GetComponent<UnityEngine.UI.Image>().color = new Color(1.0f, 1.0f, 1.0f, 0.0f);

        // if a status is showing, animate it:
        // first check if the status needs to change
        int changingStatus, highestStatus = -1;
        for (changingStatus = (int) ButtonCanvasStatusType.NUM_STATUS_TYPES - 1; changingStatus >= 0; changingStatus--)
        {
            if (statusString[changingStatus] != null && highestStatus == -1)
                highestStatus = changingStatus;
            if (statusString[changingStatus] != queuedStatusString[changingStatus])
            {
                if (highestStatus > changingStatus)
                {
                    // if the status change is hidden by a higher-priority status, blindly do the change
                    statusString[changingStatus] = queuedStatusString[changingStatus];
                }
                else
                    break;
            }
        }

        // if changing status, or non-card overlay is visible, or if dialogue/fader/card overlay is visible when the highest status
        // is just game progress, hide the status
        if (changingStatus != -1 || overlayCanvas != null && overlayCanvas.GetComponent<CardCanvasBehaviour>() == null
            || highestStatus == 0 && (text != "" || faderSuppressingText || overlayCanvas != null))
        {
            if (statusTime > 0)
            {
                statusTime -= Time.deltaTime * STATUS_FACTOR;
                if (statusTime < 0)
                    statusTime = 0;
            }
            else
            {
                // only if status frame is zero can we actually do the change
                if (changingStatus != -1)
                {
                    statusString[changingStatus] = queuedStatusString[changingStatus];

                    // reposition the status image depending on whether the dialogue is active
                    Vector3 v = statusImage.GetComponent<RectTransform>().anchoredPosition3D;
                    v.y = (text != "" ? dialogue.transform.parent.GetComponent<RectTransform>().rect.height * 3.0f / 5.0f : 40.0f);
                    statusImage.GetComponent<RectTransform>().anchoredPosition3D = v;
                }
            }
        }
        else if (highestStatus != -1)
        {
            if (statusTime < STATUS_OFFSET)
            {
                statusTime += Time.deltaTime * STATUS_FACTOR;
                if (statusTime > STATUS_OFFSET)
                    statusTime = STATUS_OFFSET;
            }
        }

        if (highestStatus != -1 && statusString[highestStatus] != null)
        {
            // display the highest status
            int start = statusString[highestStatus].IndexOf("#");
            string statusImageFile = "";
            if (start != -1)
                statusImageFile = statusString[highestStatus].Substring(0, start);
            string statusText = Colorize(Colorize(statusString[highestStatus].Substring(start + 1), "{", "}", "#ff8080"), "[", "]", "#80ff80");
            status.transform.GetChild(0).gameObject.GetComponent<UnityEngine.UI.Text>().text = statusText;
            status.GetComponent<UnityEngine.UI.Image>().color = statusColor[highestStatus];
            if (statusImageFile != "")
            {
                if (! sprites.ContainsKey(statusImageFile))
                    sprites.Add(statusImageFile, Resources.Load<Sprite>(statusImageFile));
                if (sprites[statusImageFile] != null)
                {
                    statusImage.GetComponent<UnityEngine.UI.Image>().overrideSprite = sprites[statusImageFile];
                    float alpha = Mathf.Abs(statusTime / STATUS_OFFSET);
                    statusImage.transform.localScale = new Vector3(alpha * 0.2f + 0.8f, alpha * 0.2f + 0.8f, 1.0f);
                    statusImage.GetComponent<UnityEngine.UI.Image>().color = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }
        }

        Vector3 pos = status.GetComponent<RectTransform>().anchoredPosition3D;
        pos.y = STATUS_BASE - statusTime;
        status.GetComponent<RectTransform>().anchoredPosition3D = pos;
    }

    private void UpdateButtons()
    {
        if (pressedButtonIsNew)
        {
            // in case the pressed button is not consumed by another script,
            // we "consume" it here (but still make it available for one tick
            // in case the intended consuming script was delayed)
            pressedButtonIsNew = false;
        }
        else
        {
            // if the pressed button is already consumed, clear it
            _pressedButton = null;
            _pressedButtonExtension = null;
        }

        // calculate glow factor of the buttons
        float glowValue = Mathf.Abs((Time.realtimeSinceStartup / 1f - ((long) (Time.realtimeSinceStartup / 1f))) * 2f - 1f);
        Color glowColor = Color.Lerp(buttonColor, Color.black, glowValue);
        for (ButtonCanvasGroup i = ButtonCanvasGroup.STATIC; i < ButtonCanvasGroup.NUM_GROUPS; i++)
        {
            for (int j = 0; j < NUM_BUTTONS_PER_GROUP; j++)
            {
                GameObject button = (i == ButtonCanvasGroup.STATIC ? staticPanel : dynamicPanel).transform.GetChild(j).gameObject;
                if (buttonGlow[(int) i, j])
                    button.GetComponent<UnityEngine.UI.Image>().color = glowColor;
                else
                    button.GetComponent<UnityEngine.UI.Image>().color = buttonColor;
            }
        }

        // calculate zoom factor of the dynamic group and transform the graphics accordingly
        float zoom = Mathf.Abs(currentZoomTime) / BUTTON_ZOOM_TIME;
        dynamicPanel.SetActive(text == "" && overlayCanvas == null && ! faderSuppressingText);
        dynamicPanel.transform.localScale = new Vector3(zoom, zoom, 1.0f);
        Vector3 position = new Vector3((1.0f - zoom) * (fromX * GetComponent<RectTransform>().rect.width),
                                       (1.0f - zoom) * ((fromY - 0.5f) * GetComponent<RectTransform>().rect.height),
                                       0.0f);
        dynamicPanel.GetComponent<RectTransform>().anchoredPosition3D = position;

        // update frame
        if (currentZoomTime != 0 && currentZoomTime != BUTTON_ZOOM_TIME)
        {
            if (text != "")
            {
                if (currentZoomTime > 0)
                    currentZoomTime = Time.fixedDeltaTime;
                else
                    currentZoomTime = 0;
            }
            else if (currentZoomTime > 0)
            {
                if (! showLoading)
                {
                    currentZoomTime += Time.deltaTime;
                    if (currentZoomTime > BUTTON_ZOOM_TIME)
                        currentZoomTime = BUTTON_ZOOM_TIME;
                }
            }
            else
            {
                currentZoomTime += Time.deltaTime;
                if (currentZoomTime > 0)
                    currentZoomTime = 0;
            }
        }

        // show the static panel without any zoom
        staticPanel.SetActive(text == "" && overlayCanvas == null && ! faderSuppressingText);
        staticPanel.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        staticPanel.GetComponent<RectTransform>().anchoredPosition3D = new Vector3();
    }

    private void UpdateDialogue()
    {
        dialogue.GetComponent<CanvasGroup>().interactable = (text != "" && overlayCanvas == null && ! faderSuppressingText);
        if (dialogue.GetComponent<CanvasGroup>().interactable)
        {
            dialogue.GetComponent<CanvasGroup>().alpha += Time.deltaTime * 5;
            if (dialogue.GetComponent<CanvasGroup>().alpha > 1.0f)
                dialogue.GetComponent<CanvasGroup>().alpha = 1.0f;
        }
        else
        {
            dialogue.GetComponent<CanvasGroup>().alpha -= Time.deltaTime * 5;
            if (dialogue.GetComponent<CanvasGroup>().alpha < 0.0f)
                dialogue.GetComponent<CanvasGroup>().alpha = 0.0f;
        }
        dialogue.SetActive(dialogue.GetComponent<CanvasGroup>().alpha > 0.0f);
        dialogue.transform.localScale = new Vector3(dialogue.GetComponent<CanvasGroup>().alpha * 0.2f + 0.8f,
                                                    dialogue.GetComponent<CanvasGroup>().alpha * 0.2f + 0.8f,
                                                    1.0f);

        // if the dialogue is showing, animate it
        if (dialogue.GetComponent<CanvasGroup>().interactable)
        {
            bool blink = true;

            if (dialogue.GetComponent<CanvasGroup>().alpha == 1.0f)
            {
                float delta = (advancing ? TEXT_SPEEDUP_FACTOR : 1) * Time.deltaTime * CHARACTERS_PER_SECOND;
                textDisplayedLength += delta;
                textInternalLength += delta;
            }
            if (textDisplayedLength >= textDisplayedFinalLength)
            {
                textDisplayedLength = textDisplayedFinalLength;
                textInternalLength = text.Length;
                textDelay += Time.deltaTime;
            }
            if (textDisplayedLength == textDisplayedFinalLength && textDelay >= DIALOGUE_DELAY)
                blink = ((textDelay - DIALOGUE_DELAY) * 2.0f - Mathf.Floor((textDelay - DIALOGUE_DELAY) * 2.0f)) < 0.5f;

            string actualText;
            int actualLength;
            do
            {
                actualText = text.Substring(0, (int) textInternalLength);
                actualLength = actualText.Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "").Replace("\u200a", "").Length;
                if (actualLength < textDisplayedLength)
                    textInternalLength = ((int) textInternalLength) + 1;
            }
            while (actualLength < textDisplayedLength);
            string colorizedText = Colorize(Colorize(actualText, "{", "}", "#ff8080"), "[", "]", "#80ff80");

            dialogue.transform.GetChild(1).gameObject.GetComponent<UnityEngine.UI.Text>().text = colorizedText;
            dialogue.transform.GetChild(3).localScale = (blink ? new Vector3(0.0f, 0.0f, 0.0f) : new Vector3(1.0f, 1.0f, 1.0f));
        }
    }

    private void UpdateBattery()
    {
        battery.GetComponent<UnityEngine.UI.Text>().text = DeviceInput.batteryLevel + "%";

        // determine battery color
        Color color;
        if (DeviceInput.batteryLevel > 30 || DeviceInput.batteryCharging)
            color = new Color(0.125f, 0.875f, 0.375f);
        else if (DeviceInput.batteryLevel > 15)
            color = new Color(0.75f, 0.625f, 0.25f);
        else
            color = new Color(0.875f, 0.25f, 0.25f);

        // if charging, pulsate the battery color
        float add = Mathf.Abs(Time.time - ((long) Time.time) - 0.5f);
        if (DeviceInput.batteryCharging)
            color = new Color(color.r + add, color.g + add, color.b + add);

        // set battery color
        battery.GetComponent<UnityEngine.UI.Text>().color =
            battery.transform.GetChild(3).GetComponent<UnityEngine.UI.Image>().color = color;

        // set cell contact color to battery color if fully charged
        if (DeviceInput.batteryLevel == 100)
            battery.transform.GetChild(1).GetComponent<UnityEngine.UI.Image>().color = color;
        else
            battery.transform.GetChild(1).GetComponent<UnityEngine.UI.Image>().color = new Color(0.5f, 0.5f, 0.5f);

        // resize battery cell according to level
        battery.transform.GetChild(3).GetComponent<RectTransform>().sizeDelta = new Vector2(DeviceInput.batteryLevel + 10, 50);

        // add charging icon if applicable
        battery.transform.GetChild(4).GetComponent<UnityEngine.UI.Image>().enabled = DeviceInput.batteryCharging;
    }

    public void UnloadSounds()
    {
        preloadedClips = new Dictionary<string, AudioClip>();
    }

    public AudioClip PreloadSound(string sound)
    {
        if (! preloadedClips.ContainsKey(sound))
        {
            preloadedClips[sound] = Resources.Load<AudioClip>("Sounds/" + sound);
            if (preloadedClips[sound] == null)
            {
                preloadedClips[sound + "-intro"] = Resources.Load<AudioClip>("Music/" + sound + "-intro");
                preloadedClips[sound] = Resources.Load<AudioClip>("Music/" + sound);
            }
        }
        return preloadedClips[sound];
    }

    public void PlaySound(string sound)
    {
        AudioSource source = GetComponent<AudioSource>();

        // immediately stop a fading bgm if a sound effect is played
        // (or else we won't hear the sound at full volume)
        if (volumeDecreasePerSecond > 0.0f)
        {
            source.Stop();
            volumeDecreasePerSecond = 0.0f;
        }
        source.volume = 1.0f;

        source.PlayOneShot(PreloadSound(sound));
    }

    private System.Collections.IEnumerator PlayMusicCoroutine(string sound)
    {
        yield return new WaitUntil(() => (volumeDecreasePerSecond == 0.0f));
        PlayMusic(sound);
    }

    public void PlayMusic(string sound, bool playAfterStoppingFade = false)
    {
        if (lastMusic == sound)
            return;

        if (playAfterStoppingFade)
        {
            StopMusic();
            PreloadSound(sound);
            PreloadSound(sound + "-intro");
            StartCoroutine(PlayMusicCoroutine(sound));
            return;
        }

        lastMusic = sound;
        AudioSource source = GetComponent<AudioSource>();
        volumeDecreasePerSecond = 0.0f;
        source.clip = PreloadSound(sound);
        source.loop = true;
        source.Stop();
        source.volume = 1.0f;
        AudioClip intro = PreloadSound(sound + "-intro");
        if (intro != null)
        {
            source.PlayOneShot(intro);
            source.PlayScheduled(AudioSettings.dspTime + intro.length);
        }
        else
            source.Play();
    }

    public void StopMusic(float volumeDecreasePerSecond = 2.0f)
    {
        lastMusic = null;
        this.volumeDecreasePerSecond = volumeDecreasePerSecond;
    }

    private void UpdateMusic()
    {
        AudioSource source = GetComponent<AudioSource>();

        if (volumeDecreasePerSecond > 0.0f)
        {
            if (source.volume - volumeDecreasePerSecond * Time.deltaTime <= 0.0f)
            {
                source.Stop();
                volumeDecreasePerSecond = 0.0f;
            }
            else
                source.volume -= volumeDecreasePerSecond * Time.deltaTime;
        }
    }

    private void Start()
    {
        GameObject.Find("ButtonCanvas/Loading").GetComponent<UnityEngine.UI.Image>().overrideSprite = Resources.Load<Sprite>("LoadingScreen");
    }

    private void Update()
    {
        UpdateMessages();
        UpdateOverlay();
        UpdateCrosshair();
        UpdateFader();
        UpdateStatus();
        UpdateButtons();
        UpdateDialogue();
        UpdateBattery();
        UpdateMusic();
    }

    // button event handler
    public void ButtonPress()
    {
        GameObject obj = EventSystem.current.currentSelectedGameObject;
        EventSystem.current.SetSelectedGameObject(null, null);  // prevent Unity Editor spacebar from pressing the button again
        if (obj == null)
            return;
        if (obj.transform.parent.gameObject == dynamicPanel || obj.transform.parent.gameObject == staticPanel)
        {
            if (obj.transform.parent.gameObject == staticPanel || currentZoomTime == BUTTON_ZOOM_TIME)
            {
                // only allow clicking if button is zoomed-in completely
                _pressedButton = obj.transform.GetChild(0).gameObject.GetComponentInChildren<UnityEngine.UI.Text>().text;
                if (_pressedButton.IndexOf((char) 160) != -1)
                {
                    _pressedButtonExtension = _pressedButton.Substring(_pressedButton.IndexOf((char) 160) + 1);
                    _pressedButton = _pressedButton.Substring(0, _pressedButton.IndexOf((char) 160));
                }
                else
                    _pressedButtonExtension = null;
                pressedButtonIsNew = true;
            }
        }
        else
        {
            // only report the clicking of the Dialogue box if it is not advancing anymore
            if (obj == dialogue)
            {
                advancing = true;
                if (textDisplayedLength < textDisplayedFinalLength || textDelay < DIALOGUE_DELAY)
                    return;

                _pressedButton = obj.name;
                _pressedButtonExtension = null;
                pressedButtonIsNew = true;
            }
        }
    }

    private void OnApplicationPause(bool paused)
    {
        timeSinceSceneStart = 0.0f;
#if ! UNITY_EDITOR
        locationPermissionDisabledShown = false;
        locationHardwareDisabledShown = false;
#endif
    }
}
