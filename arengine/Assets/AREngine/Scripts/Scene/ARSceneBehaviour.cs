/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class ARSceneBehaviour : SceneBehaviour
{
    public const float SAFETY_WARNING_TIMEOUT = 5.0f; // seconds before the safety warning will disappear
    public const float MARKER_TIMEOUT = 10.0f;        // seconds before the marker image will disappear
    public const float NO_MARKER_TIMEOUT = 30.0f;     // seconds before the starting without marker button will show
    public const float NO_MARKER_MESSAGE = 5.0f;      // seconds to tell user to hold the device steady
    public const float NO_MARKER_STEADY = 3.0f;       // seconds to hold the device steady
    public const float NO_MARKER_ANGLE = 10.0f;       // maximum angle variation to consider the device as steady (+/-)
    public const float PITCH_ONLY_MAX_ANGLE = 15.0f;  // in PitchOnly mode, the scene will not allow the player to move past this pitch (+/-)

    private IAREngine engine;
    private TSVLookup tsv;

    private Dictionary<string, GameObject> gameObjects = new Dictionary<string, GameObject>();
    private string currentObject = "dummy";  // this ensures that any existing buttons (e.g., from parent scenes) will be removed properly
    private static bool inventoryMirrored = false;  // set to true if the inventory object is to be displayed mirrored

    private string[] dialogue;
    private int dialogueLine;

    private Dictionary<string, bool> autorun = new Dictionary<string, bool>();
    private bool fading;
    private bool waitAfterFading;
    private bool finished;

    private float centerPitch = float.PositiveInfinity;
    private float startTime = -1;
    private bool startWithoutMarker;
    private int trackingInterval;
    private float lastTrackingTime;
    private long lastSceneTimeCounterUpdateTime;  // Global%SceneTimeCounter is incremented whenever player is in a scene that has not ended

    private bool markerNotNeeded;
    private bool introTextShown;

    private bool isSubscene
    {
        get
        {
            return gameObject.layer == 10;  // subscene layer
        }
    }

    private Camera sceneCamera
    {
        get
        {
            if (isSubscene)
                return GameObject.FindWithTag("SubsceneCamera").GetComponent<Camera>();
            else
                return GameObject.FindWithTag("SceneCamera").GetComponent<Camera>();
        }
    }

    private Vector3 KeepOnScreen(Vector3 screenPoint)
    {
        if (screenPoint.x < 0)
            screenPoint.x = 0;
        if (screenPoint.x > Screen.width)
            screenPoint.x = Screen.width;
        if (screenPoint.y < 0)
            screenPoint.y = 0;
        if (screenPoint.y > Screen.height)
            screenPoint.y = Screen.height;
        return screenPoint;
    }

    private void AdjustSceneCamera()
    {
        if (gameState.GetFlag(gameState.sceneName + "%IgnoreRotation"))
        {
            Vector3 rotation = GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.rotation.eulerAngles;
            rotation.x = 0.0f;
            rotation.y = 0.0f;
            GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.rotation = Quaternion.Euler(rotation);
        }

        if (isSubscene)
        {
            Camera camera = GameObject.FindWithTag("SubsceneCamera").GetComponent<Camera>();
            camera.transform.position = GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.position;
            Vector3 rotation = GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.rotation.eulerAngles;
            if (gameState.GetFlag(gameState.sceneName + "%PitchOnly"))
            {
                // pitch-only mode
                // adjust rotation to [-180, 180] range
                if (rotation.x > 180.0f)
                    rotation.x -= 360.0f;

                // clamp to [-90, 90] when device roll is inverted
                if (rotation.z >= 90.0f && rotation.z <= 270.0f)
                {
                    if (rotation.x < 0.0f)
                        rotation.x = -90.0f;
                    else
                        rotation.x = 90.0f;
                }

                if (float.IsPositiveInfinity(centerPitch))
                {
                    // set center pitch if not yet set
                    centerPitch = rotation.x;
                }
                else
                {
                    // adjust center pitch when player moves beyond max angles
                    if (rotation.x < centerPitch - PITCH_ONLY_MAX_ANGLE)
                        centerPitch = rotation.x + PITCH_ONLY_MAX_ANGLE;
                    else if (rotation.x > centerPitch + PITCH_ONLY_MAX_ANGLE)
                        centerPitch = rotation.x - PITCH_ONLY_MAX_ANGLE;
                }

                // make sure that the pitch-only max angle is attainable even if the device is near flat horizontal
                if (centerPitch < -90.0f + PITCH_ONLY_MAX_ANGLE)
                    centerPitch = -90.0f + PITCH_ONLY_MAX_ANGLE;
                else if (centerPitch > 90.0f - PITCH_ONLY_MAX_ANGLE)
                    centerPitch = 90.0f - PITCH_ONLY_MAX_ANGLE;
                rotation.x -= centerPitch;

                // no yaw and roll during pitch-only mode
                rotation.y = 0.0f;
                rotation.z = 0.0f;
            }
            else
                centerPitch = float.PositiveInfinity;
            camera.transform.rotation = Quaternion.Euler(rotation);

            float fov = GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().fieldOfView;
            camera.fieldOfView = (gameState.GetFlag(gameState.sceneName + "%PitchOnly") ? fov * 0.8f : fov);
        }

        GameObject inventoryObject = GameObject.Find("InventoryObject");
        if (inventoryObject != null)
        {
            if (gameState.GetFlag(gameState.sceneName + "%PitchOnly"))
                SetLayer(inventoryObject, 1);  // invisible layer
            else
                SetLayer(inventoryObject, gameObject.layer);
            Vector3 hand = buttonCanvas.swapButtonGroups ? GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.right * -1.25f
                                                         : GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.right * 1.25f;
            inventoryObject.transform.position = GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.position
                                                 + GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.forward * 10.0f
                                                 + GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.up * -2.0f
                                                 + hand * 2.0f;
            inventoryObject.transform.rotation = GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.rotation
                                                 * Quaternion.Euler(0.0f, -90.0f, 0.0f);
            inventoryObject.transform.localScale = new Vector3(1.0f, 1.0f, inventoryMirrored && buttonCanvas.swapButtonGroups ? -1.0f : 1.0f);
        }
    }

    private string GetObjectInFocus()
    {
        // use the camera parameters of the main camera and change to the fieldOfView
        // and aspect specified by the user for calculating the frustum planes
        Ray ray = new Ray(sceneCamera.transform.position, sceneCamera.transform.forward);

        // for each GameObject...
        float closestDistance = 1000.0f;
        GameObject closestObject = null;
        foreach (GameObject obj in gameObjects.Values)
        {
            // check whether the GameObject is active and enabled
            if (! obj.activeSelf || obj.name[0] == 'D')
                continue;

            // use the object's collider to determine whether we are intersecting
            SphereCollider collider = obj.GetComponent<SphereCollider>();
            if (collider != null)
            {
                float oldRadius = collider.radius;
                collider.radius *= 2.0f;
                RaycastHit hit;
                if (collider.Raycast(ray, out hit, closestDistance))
                {
                    closestDistance = hit.distance;
                    closestObject = obj;
                }
                collider.radius = oldRadius;
            }
        }

        if (closestObject != null)
            return closestObject.name.Substring(2);
        else
            return null;
    }

    private bool DoDialogue()
    {
        while (dialogueLine < dialogue.Length && dialogue[dialogueLine][0] == '@')
        {
            Debug.Log(dialogue[dialogueLine]);

            string[] parameters = dialogue[dialogueLine].Split(new char[]{ ' ' },
                                                               System.StringSplitOptions.RemoveEmptyEntries);
            if (parameters.Length == 1)
            {
                // some commands have an implicit default parameter of null-string
                parameters = new string[]{ parameters[0], "" };
            }
            if (parameters[0] == "@show")
            {
                if (gameObjects.ContainsKey(parameters[1]))
                {
                    gameObjects[parameters[1]].SetActive(true);
                    gameState.SetFlag(gameState.sceneName + "%" + parameters[1] + "%H", false, false);
                }
            }
            else if (parameters[0] == "@hide")
            {
                if (gameObjects.ContainsKey(parameters[1]))
                {
                    gameObjects[parameters[1]].SetActive(false);
                    gameState.SetFlag(gameState.sceneName + "%" + parameters[1] + "%H", true, false);
                }
            }
            else if (parameters[0] == "@enable")
            {
                if (gameObjects.ContainsKey(parameters[1]))
                {
                    gameObjects[parameters[1]].name = "G_" + parameters[1];
                    gameState.SetFlag(gameState.sceneName + "%" + parameters[1] + "%D", false, false);
                }
            }
            else if (parameters[0] == "@disable")
            {
                if (gameObjects.ContainsKey(parameters[1]))
                {
                    gameObjects[parameters[1]].name = "D_" + parameters[1];
                    gameState.SetFlag(gameState.sceneName + "%" + parameters[1] + "%D", true, false);
                }
            }
            else if (parameters[0] == "@settrue")
            {
                if (parameters[1].IndexOf('%') == -1)
                    parameters[1] = gameState.sceneName + "%" + parameters[1];
                gameState.SetFlag(parameters[1], true, false);
            }
            else if (parameters[0] == "@setfalse")
            {
                if (parameters[1].IndexOf('%') == -1)
                    parameters[1] = gameState.sceneName + "%" + parameters[1];
                gameState.SetFlag(parameters[1], false, false);
            }
            else if (parameters[0] == "@incr")
            {
                if (parameters[1].IndexOf('%') == -1)
                    parameters[1] = gameState.sceneName + "%" + parameters[1];
                int value = 1;
                if (parameters.Length > 2)
                    value = int.Parse(parameters[2]);
                gameState.SetFlag(parameters[1], gameState.GetFlagIntValue(parameters[1]) + value, false);
            }
            else if (parameters[0] == "@decr")
            {
                if (parameters[1].IndexOf('%') == -1)
                    parameters[1] = gameState.sceneName + "%" + parameters[1];
                int value = 1;
                if (parameters.Length > 2)
                    value = int.Parse(parameters[2]);
                gameState.SetFlag(parameters[1], gameState.GetFlagIntValue(parameters[1]) - value, false);
            }
            else if (parameters[0] == "@set")
            {
                if (parameters[1].IndexOf('%') == -1)
                    parameters[1] = gameState.sceneName + "%" + parameters[1];
                gameState.SetFlag(parameters[1], int.Parse(parameters[2]), false);
            }
            else if (parameters[0] == "@status")
            {
                buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, dialogue[dialogueLine].Substring(8));
            }
            else if (parameters[0] == "@subscene")
            {
                gameState.LoadARSubscene(parameters[1]);
            }
            else if (parameters[0] == "@select")
            {
                gameState.selectedItem = parameters[1];
                if (parameters[1] != "")
                {
                    GameObject newObject = Instantiate(gameObjects[currentObject].transform).gameObject;
                    newObject.name = "InventoryObject";
                }
                currentObject = "dummy";
                inventoryMirrored = false;
            }
            else if (parameters[0] == "@select_mirrored")
            {
                gameState.selectedItem = parameters[1];
                if (parameters[1] != "")
                {
                    GameObject newObject = Instantiate(gameObjects[currentObject].transform).gameObject;
                    newObject.name = "InventoryObject";
                }
                currentObject = "dummy";
                inventoryMirrored = true;
            }
            else if (parameters[0] == "@glowbutton")
            {
                gameState.glowingButton = parameters[1].Replace('_', ' ').Replace("  ", " ").Trim();
            }
            else if (parameters[0] == "@fade")
            {
                buttonCanvas.SetFade(new Color(float.Parse(parameters[1]),
                                               float.Parse(parameters[2]),
                                               float.Parse(parameters[3]),
                                               float.Parse(parameters[4])),
                                     float.Parse(parameters[5]));
                if (float.Parse(parameters[5]) > 0)
                {
                    fading = true;
                    return true;
                }
            }
            else if (parameters[0] == "@showstill")
            {
                waitAfterFading = fading = ! buttonCanvas.SetStill("Stills/" + parameters[1]);
                if (fading)
                    return true;
            }
            else if (parameters[0] == "@hidestill")
            {
                fading = ! buttonCanvas.SetStill(null);
                if (fading)
                    return true;
            }
            else if (parameters[0] == "@playsound")
            {
                buttonCanvas.PlaySound(parameters[1]);
            }
            else if (parameters[0] == "@playmusic")
            {
                buttonCanvas.PlayMusic(parameters[1]);
            }
            else if (parameters[0] == "@stopmusic")
            {
                buttonCanvas.StopMusic();
            }
            else if (parameters[0] == "@animate")
            {
                Animator animator = gameObjects[parameters[1]].GetComponentInChildren<Animator>();
                float blendFactor = 0.25f;
                if (parameters.Length > 3)
                    blendFactor = float.Parse(parameters[3]);
                if (animator != null)
                    animator.CrossFade(parameters[2], blendFactor);
                else
                    Debug.LogError("No Animator found in " + parameters[1]);
            }
            else if (parameters[0] == "@stopanimation")
            {
                Animator animator = gameObjects[parameters[1]].GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.Rebind();
                }
                else
                    Debug.LogError("No Animator found in " + parameters[1]);
            }
            else if (parameters[0] == "@credits")
            {
                gameState.SetFlag("Global%PlayCredits", true, false);
                buttonCanvas.ShowCreditsOverlay(delegate()
                {
                    gameState.SetFlag("Global%PlayCredits", false, false);
                });
            }
            else if (parameters[0] == "@card")
            {
                buttonCanvas.ShowCardOverlay(parameters[1]);
            }
            else if (parameters[0] == "@resetautorun")
            {
                autorun.Clear();
            }
            else if (parameters[0] == "@exit")
            {
                gameState.SaveFlags();
                ProcessSceneTimeCounterEnd();
                dialogue = null;
                buttonCanvas.SetDialogue(null);
                if (isSubscene)
                {
                    finished = true;
                    buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
                    buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 0, null);
                    buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, null);
                    buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 2, null);
                    buttonCanvas.showDynamicGroup = false;
                }
                else
                    gameState.ProcessReturnToMap();
                return true;
            }
            else if (parameters[0] == "@analytics")
            {
                if (gameState.GetFlagIntValue("Global%SceneTimeCounter") != 0)
                {
                    if (parameters[1] == "Milestone")
                        gameState.EncodeAnalyticsMilestone(parameters[2]);  // backward compatibility
                    else
                        gameState.EncodeAnalyticsMilestone(parameters[1]);
                }
            }

            dialogueLine++;
        }

        if (dialogueLine == dialogue.Length)
        {
            gameState.SaveFlags();  // only save the new game state after a dialogue is done
            ProcessSceneTimeCounterEnd();
            dialogue = null;
            buttonCanvas.SetDialogue(null);
            return false;  // continue on with the next script that may trigger on the same frame
        }
        else
        {
            Debug.Log(dialogue[dialogueLine]);
            buttonCanvas.SetDialogue(dialogue[dialogueLine]);
            return true;
        }

    }

    private void Start()
    {
        if (! Init(true))
            return;

        engine = GameObject.FindWithTag("AREngine").GetComponent<AREngineBehaviour>();

        tsv = new TSVLookup("SceneTSVs/" + gameState.sceneName);

        // queue all sounds and music
        buttonCanvas.UnloadSounds();
        foreach (string marker in tsv.Lookup())
        {
            foreach (string target in tsv.Lookup(marker))
            {
                foreach (string action in tsv.Lookup(marker, target))
                {
                    foreach (string flags in tsv.Lookup(marker, target, action))
                    {
                        foreach (string dialogue in tsv.Lookup(marker, target, action, flags))
                        {
                            string[] parameters = dialogue.Split(new char[]{ ' ' },
                                                                 System.StringSplitOptions.RemoveEmptyEntries);
                            if (parameters[0] == "@playmusic" || parameters[0] == "@playsound")
                            {
                                buttonCanvas.PreloadSound(parameters[1]);
                            }
                        }
                    }
                }
            }
        }

        if (! isSubscene && (tsv.Lookup()[1].StartsWith("_NM ") || gameState.moduleName == "M0" || gameState.GetFlag("Global%Module" + gameState.moduleName.Substring(1) + "End")))
        {
            // special case for _NM scenes, all M0 scenes and all other scenes where the player has already unlocked the ending:
            // we don't need a marker, and we always start from the beginning for these scenes (except for _NM)
            markerNotNeeded = true;
            if (! tsv.Lookup()[1].StartsWith("_NM "))
                gameState.ResetFlags(gameState.GetFlagsStartingWith(gameState.moduleName + "%"));
        }

        // a subscene called from a scene assumes that the AR camera has been initialized, so we can setup objects immediately
        if (isSubscene)
            SetupObjects();

        buttonCanvas.SetStill(null);
        buttonCanvas.SetFade(new Color(0, 0, 0, 0), 0);
        buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 0, null);
        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, null);
        buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 2, null);
        buttonCanvas.showDynamicGroup = false;
        buttonCanvas.SetDialogue(null);
    }

    private void SetupObjects()
    {
        bool firstTimeDone = gameState.GetFlag(gameState.sceneName + "%_FirstTimeDone");

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            bool emptyScene = true;
            foreach (GameObject obj in SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                emptyScene = false;

                // consider only top-level objects that start with X_ where X is any letter
                if (obj.layer != gameObject.layer || obj.transform.parent != null
                    || obj.name.Length < 2 || obj.name[1] != '_')
                {
                    continue;
                }

                string fullName = gameState.sceneName + "%" + obj.name.Substring(2);
                gameObjects[obj.name.Substring(2)] = obj;
                if (! firstTimeDone)
                {
                    if (obj.name[0] == 'H')
                    {
                        obj.name = "G_" + obj.name.Substring(2);
                        obj.SetActive(false);
                        gameState.SetFlag(fullName + "%H", true);
                    }
                    if (obj.name[0] == 'D')
                        gameState.SetFlag(fullName + "%D", true);
                }
                else
                {
                    obj.name = "G_" + obj.name.Substring(2);
                    if (gameState.GetFlag(fullName + "%H"))
                        obj.SetActive(false);
                    else
                        obj.SetActive(true);
                    if (gameState.GetFlag(fullName + "%D"))
                        obj.name = "D_" + obj.name.Substring(2);
                }
            }

            // cleanup scenes whose gameobjects have all been removed
            if (emptyScene)
                SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
        }

        if (! firstTimeDone)
            gameState.SetFlag(gameState.sceneName + "%_FirstTimeDone", true);
    }

    private void Update()
    {
        if (! Init(true))
            return;

        // if a subscene is active, and we're not that scene, ignore updates
        if (! isSubscene && gameState.currentlyInSubscene)
            return;

        AdjustSceneCamera();

        bool sameFrame = false;

        if (finished || gameState.loadingNewScene)
        {
            if (isSubscene && ! buttonCanvas.showDynamicGroup)
            {
                // destroy the objects of the subscene (including auxiliary ones)
                foreach (GameObject obj in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (obj.layer == gameObject.layer && obj.transform.parent == null && obj.name != "InventoryObject")
                    {
                        obj.SetActive(false);
                        gameObjects.Add(obj.name, obj);
                    }
                }
                foreach (GameObject obj in gameObjects.Values)
                    Destroy(obj);
                gameObjects.Clear();
                gameState.UnloadARSubscene();
            }

            return;
        }

        int staticButtonIndex = 0;
        int dynamicButtonIndex = 0;
        if (! isSubscene)
        {
            if (gameState.GetFlag("Global%EasyBackButton"))
            {
                buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, staticButtonIndex++, "Back", null, gameState.glowingButton == "Back");
                if (buttonCanvas.pressedButton == "Back")
                {
                    gameState.ProcessReturnToMap();
                    return;
                }
            }
            else
            {
                buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, staticButtonIndex++, "Options", null, gameState.glowingButton == "Options");
                if (buttonCanvas.pressedButton == "Options")
                    buttonCanvas.ShowOptionsOverlay();
            }
        }

        string objectInFocus = null;
        bool actionExecuted = false;

        if (engine.markerState == IARMarkerState.SELECTING)
        {
            if (isSubscene)
            {
                // if we were in a subscene and the marker state is suddenly back to selecting, finish the subscene prematurely
                finished = true;
                buttonCanvas.SetStill(null);
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
                return;
            }

            dialogue = null;
            if (tsv.Lookup()[1].StartsWith("_NW "))
                buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, "MarkerStatuses/" + tsv.Lookup()[1].Substring(4) + "#Take a photo of the [" + tsv.Lookup()[1].Substring(4) + "]! See the example below.");
            else if ((Time.timeSinceLevelLoad % (SAFETY_WARNING_TIMEOUT + MARKER_TIMEOUT)) < SAFETY_WARNING_TIMEOUT)
                buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, "Statuses/SafetyWarning#CAUTION: Be mindful of your surroundings! Stay off the road!");
            else
                buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, "MarkerStatuses/" + tsv.Lookup()[1] + "#Take a photo of the marker! See the example below.");
            buttonCanvas.SetStatus(ButtonCanvasStatusType.ERROR, null);
            buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, null);
            buttonCanvas.SetStill(null);
            buttonCanvas.SetFade(new Color(0, 0, 0, 0), 0);
            buttonCanvas.SetDialogue(null);
            fading = false;
            waitAfterFading = false;

            if (startTime == -1)
            {
                startTime = 0;
                startWithoutMarker = false;

                // reset game state to last-known-good state
                buttonCanvas.HideOverlay();
                buttonCanvas.StopMusic();
                gameState.LoadFlags();

                // for a main scene (not subscene), we setup objects at this point
                SetupObjects();
            }

            if (gameState.sceneName == "M0%Scene1" && ! gameState.GetFlag("Global%FinishedFirstTutorial") && ! introTextShown)
            {
                buttonCanvas.ShowQuestionOverlay(
                    "Before we begin, let us do a quick tutorial.\n\nWe will now calibrate your device. Make sure your camera lens is unobstructed.",
                    "Start calibration",
                    null,
                    delegate(string pressedButton2)
                    {
                        gameState.SetFlag("Global%SceneTimeCounter", 1);  // log tutorial time
                        introTextShown = true;
                        buttonCanvas.HideOverlay();
                    }
                );
            }
            else if (engine.currentlySeenARMarker == null || markerNotNeeded || tsv.Lookup()[1].StartsWith("_BT "))
            {
                if (startWithoutMarker || tsv.Lookup()[1].StartsWith("_BT "))
                {
                    if (tsv.Lookup()[1].StartsWith("_BT "))
                        buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, "MarkerStatuses/" + tsv.Lookup()[1].Substring(4) + "#Take a photo of the [" + tsv.Lookup()[1].Substring(4) + "]!");
                    else
                        buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, "Statuses/FakeMarker#Try taking a photo of the marker now!");
                    buttonCanvas.SetCrosshair(KeepOnScreen(new Vector3(Screen.width * 0.5f - Screen.height / 3, Screen.height * 0.5f - Screen.height / 3)),
                                              KeepOnScreen(new Vector3(Screen.width * 0.5f + Screen.height / 3, Screen.height * 0.5f + Screen.height / 3)));
                    buttonCanvas.SetDynamicGroupOrigin(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0.0f));
                    buttonCanvas.showDynamicGroup = true;
                    buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, dynamicButtonIndex++, "Take photo");
                    objectInFocus = currentObject;  // force clearing the other buttons
                    if (buttonCanvas.pressedButton == "Take photo")
                    {
                        startTime = 0;
                        startWithoutMarker = true;  // logic after here should assume that we startWithoutMarker

                        buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, null);
                        engine.StartTracking(GameObject.Find(tsv.Lookup()[1]));
                    }
                }
                else if (markerNotNeeded)
                {
                    buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, "Statuses/NoMarker#Hold the device up with both hands, directly in front of you.");
                    buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
                    buttonCanvas.showDynamicGroup = false;

                    Vector3 deviceAngles = DeviceInput.attitude.eulerAngles;
                    if (DeviceInput.accelerometerMalfunctioning)
                        deviceAngles.x = deviceAngles.z = 0;
                    startTime += Time.deltaTime;
                    if ((deviceAngles.x < NO_MARKER_ANGLE || deviceAngles.x >= 360.0f - NO_MARKER_ANGLE)
                        && (deviceAngles.z < NO_MARKER_ANGLE || deviceAngles.z >= 360.0f - NO_MARKER_ANGLE)
                        && ! buttonCanvas.overlayShowing)
                    {
                        if (startTime >= NO_MARKER_MESSAGE)
                            buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, "Steady... " + (int) (NO_MARKER_MESSAGE + NO_MARKER_STEADY - startTime + 1) + "...");
                        if (startTime >= NO_MARKER_MESSAGE + NO_MARKER_STEADY)
                        {
                            startTime = 0;
                            startWithoutMarker = true;  // logic after here should assume that we startWithoutMarker

                            buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, null);
                            engine.StartTracking(GameObject.Find(tsv.Lookup()[1]));
                        }
                    }
                    else
                    {
                        if (buttonCanvas.overlayShowing)
                            startTime = 0;
                        else if (startTime >= NO_MARKER_MESSAGE)
                            startTime = NO_MARKER_MESSAGE;
                    }
                }
                else if (startTime == 0)
                {
                    startTime = Time.realtimeSinceStartup;
                    buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
                    buttonCanvas.showDynamicGroup = false;
                }
                else if (Time.realtimeSinceStartup - startTime >= NO_MARKER_TIMEOUT && gameState.GetFlag("Global%GPSValid"))
                {
                    buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, staticButtonIndex++, "Help! The marker won't scan!");
                    buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
                    buttonCanvas.showDynamicGroup = false;

                    if (buttonCanvas.pressedButton == "Help! The marker won't scan!")
                    {
                        startTime = 0;
                        startWithoutMarker = true;
                    }
                }
            }
            else
            {
                startTime = 0;
                startWithoutMarker = false;

                Vector3 center = GameObject.FindWithTag("MainCamera").GetComponentInChildren<Camera>().WorldToScreenPoint(engine.currentlySeenARMarker.transform.GetChild(0).position);
                buttonCanvas.SetCrosshair(KeepOnScreen(new Vector3(center.x - Screen.height / 3, center.y - Screen.height / 3)),
                                          KeepOnScreen(new Vector3(center.x + Screen.height / 3, center.y + Screen.height / 3)));
                buttonCanvas.SetDynamicGroupOrigin(KeepOnScreen(center));
                buttonCanvas.showDynamicGroup = true;
                buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, dynamicButtonIndex++, "Take photo");
                objectInFocus = currentObject;  // force clearing the other buttons
                if (buttonCanvas.pressedButton == "Take photo")
                {
                    buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, null);
                    engine.StartTracking(null);
                }
            }
        }
        else if (engine.markerState != IARMarkerState.STARTING)
        {
            // track Global%SceneTimeCounter for every second that the player is actually playing in the scene
            ProcessSceneTimeCounter();

            if (startTime != -1)
            {
                gameState.selectedItem = "";
                currentObject = "dummy";
                autorun.Clear();
                startTime = -1;
            }
            buttonCanvas.SetStatus(ButtonCanvasStatusType.ERROR, null);
            if (gameState.GetFlag(gameState.sceneName + "%PitchOnly"))
                buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, "Tilt your device up or down to choose an object.");
            else
                buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, null);

            if (engine.isARMarkerActuallyVisible)
                startWithoutMarker = false;  // we found the marker legitimately; set this false in case we started without the marker

            // display tip to resynchronize; do it sparingly if we haven't lost tracking, do it more often if we have lost tracking
            if (engine.markerState == IARMarkerState.LOST && ! gameState.GetFlag(gameState.sceneName + "%PitchOnly"))
            {
                if (trackingInterval != 0)
                {
                    lastTrackingTime = Time.realtimeSinceStartup;
                    trackingInterval = 0;
                }
            }
            else if (engine.markerState == IARMarkerState.TEMPORARY && ! gameState.GetFlag(gameState.sceneName + "%PitchOnly"))
            {
                if (trackingInterval != 30)
                {
                    lastTrackingTime = Time.realtimeSinceStartup;
                    trackingInterval = 30;
                }
            }
            else
            {
                if (trackingInterval != 120)
                {
                    lastTrackingTime = Time.realtimeSinceStartup;
                    trackingInterval = 120;
                }
            }

            long secondsSince = (long) (Time.realtimeSinceStartup - lastTrackingTime);

            if (trackingInterval == 0)
            {
                if (secondsSince >= 2)
                {
                    if (startWithoutMarker)
                        buttonCanvas.SetStatus(ButtonCanvasStatusType.ERROR, "Tracking lost, hold still! (Avoid looking at featureless areas, e.g., blank walls.)");
                    else
                        buttonCanvas.SetStatus(ButtonCanvasStatusType.ERROR, "Tracking lost, hold still! (If this doesn't work, try looking at the marker again.)");
                }
            }
            else
            {
                if (secondsSince % trackingInterval >= trackingInterval - 10 && ! startWithoutMarker)
                    buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, "If the virtual objects seem misaligned/unstable, try looking at the marker again.");
            }

            if (buttonCanvas.dialogueLingering)
                buttonCanvas.SetStatus(ButtonCanvasStatusType.TIP, "Statuses/TapToAdvance" + (gameState.GetFlag("System%SwapButtonGroups") ? "Left" : "Right") + "#Tap the screen to advance the dialogue.");

            if (dialogue != null)
            {
                buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
                if (! fading && buttonCanvas.pressedButton == "Dialogue" || fading && ! buttonCanvas.faderActive)
                {
                    if (waitAfterFading)
                    {
                        buttonCanvas.SetFade(new Color(0, 0, 0, 0), 1.0f);
                        waitAfterFading = false;
                    }
                    else
                    {
                        fading = false;
                        dialogueLine++;
                        DoDialogue();
                    }
                }
                staticButtonIndex = 0;
                dynamicButtonIndex = 0;
                buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
                while (staticButtonIndex != ButtonCanvasBehaviour.NUM_BUTTONS_PER_GROUP)
                    buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, staticButtonIndex++, null);
                while (dynamicButtonIndex != ButtonCanvasBehaviour.NUM_BUTTONS_PER_GROUP)
                    buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, dynamicButtonIndex++, null);
                return;
            }

            objectInFocus = GetObjectInFocus();
            if (! buttonCanvas.showDynamicGroup)
                currentObject = objectInFocus;

            bool crosshairOnMarker = false;

            // set button origin and crosshair to the currently-focused object
            buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
            buttonCanvas.SetDynamicGroupOrigin(
                new Vector3(Screen.width * (buttonCanvas.swapButtonGroups ? 0.75f : 0.25f), Screen.height * 0.4f, 0.0f)
            );
            if (currentObject != null && currentObject != "dummy")
            {
                GameObject obj = gameObjects[currentObject];
                Bounds bounds = obj.GetComponent<SphereCollider>().bounds;
                Vector3 screenPoint = sceneCamera.WorldToScreenPoint(bounds.center);
                Vector3 bottom = sceneCamera.WorldToScreenPoint(bounds.center - new Vector3(0, bounds.extents.y, 0));
                Vector3 top = sceneCamera.WorldToScreenPoint(bounds.center + new Vector3(0, bounds.extents.y, 0));
                float average = ((screenPoint - bottom).magnitude + (screenPoint - top).magnitude) / 2;
                Vector3 lowerLeft = new Vector3(screenPoint.x - average, screenPoint.y - average);
                Vector3 upperRight = new Vector3(screenPoint.x + average, screenPoint.y + average);
                buttonCanvas.SetCrosshair(KeepOnScreen(lowerLeft), KeepOnScreen(upperRight));
                buttonCanvas.SetDynamicGroupOrigin(KeepOnScreen(screenPoint));

                // rotate the selected object when in pitch-only mode
                if (gameState.GetFlag(gameState.sceneName + "%PitchOnly"))
                    obj.transform.RotateAround(bounds.center, new Vector3(0, 1, 0), Time.deltaTime * 90.0f);
            }
            else
            {
#if UNITY_EDITOR
                if (Vuforia.CameraDevice.Instance.GetCameraImage(Vuforia.PIXEL_FORMAT.RGBA8888) != null)
#endif
                {
                    if (engine.isARMarkerActuallyVisible && ! gameState.GetFlag(gameState.sceneName + "%PitchOnly"))
                    {
                        Vector3 center = GameObject.FindWithTag("MainCamera").GetComponentInChildren<Camera>().WorldToScreenPoint(engine.currentlySeenARMarker.transform.GetChild(0).position);
                        if (center.z > 0)
                        {
                            Vector3 lowerLeft = KeepOnScreen(new Vector3(center.x - Screen.height / 3, center.y - Screen.height / 3));
                            Vector3 upperRight = KeepOnScreen(new Vector3(center.x + Screen.height / 3, center.y + Screen.height / 3));
                            if (upperRight.x - lowerLeft.x > 0 && upperRight.y - lowerLeft.y > 0)
                            {
                                buttonCanvas.SetCrosshair(lowerLeft, upperRight);
                                crosshairOnMarker = true;
                            }
                        }
                    }
                }
            }
            buttonCanvas.showDynamicGroup = (objectInFocus == currentObject && (objectInFocus != null || crosshairOnMarker));

            List<string> targets = new List<string>();
            targets.Add("-");
            if (objectInFocus == currentObject && currentObject != null)
                targets.Add(objectInFocus);
            foreach (string target in targets)
            {
                List<string> actions = tsv.Lookup(isSubscene ? "-" : engine.currentlySeenARMarker.name, target);
                foreach (string action in actions)
                {
                    // only use actions with []s if the gamestate selected item is the same as that in the bracket;
                    // only use actions without []s if the gamestate has no selected item
                    if (action.IndexOf('[') != -1)
                    {
                        string item = action.Substring(action.IndexOf('[') + 1, action.IndexOf(']') - action.IndexOf('[') - 1);
                        if (item != gameState.selectedItem && action != "Auto" && action != "Autorun")
                            continue;
                    }
                    else if (gameState.selectedItem != "" && action != "Auto" && action != "Autorun")
                        continue;

                    // check if a statement is satisfied
                    List<string> flagsSet = tsv.Lookup(isSubscene ? "-" : engine.currentlySeenARMarker.name, target, action);
                    foreach (string flags in flagsSet)
                    {
                        if (flags != "-" && ! gameState.EvaluateFlags(flags))
                            continue;
                        if (action == "Autorun" && autorun.ContainsKey(flags))
                            continue;

                        // by default, the button name is just the action itself (with no extension)
                        string buttonName = action;
                        string buttonNameExtension = "";

                        // if the action has a [ or #, trim the button name to that (and put the rest in extension)
                        int terminator = action.IndexOf('[');
                        int hash = action.IndexOf('#');
                        if (terminator == -1 || hash != -1 && hash < terminator)
                            terminator = hash;
                        if (terminator != -1)
                        {
                            buttonNameExtension = action.Substring(terminator + 1).Replace('[', '_').Replace(']', '_').Replace('#', '_');
                            buttonName = action.Substring(0, terminator);
                        }

                        // append target name to button extension (only if there is no #)
                        if (target != "-" && hash == -1)
                            buttonNameExtension += "_" + target;

                        // replace _'s and trim
                        buttonName = buttonName.Replace('_', ' ').Replace("  ", " ").Trim();
                        buttonNameExtension = buttonNameExtension.Replace('_', ' ').Replace("  ", " ").Trim();
                        if (action != "Auto" && action != "Autorun")
                        {
                            if (target == "-")
                            {
                                buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, staticButtonIndex++,
                                                       buttonName, buttonNameExtension, gameState.glowingButton == buttonName);
                            }
                            else
                            {
                                buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, dynamicButtonIndex++,
                                                       buttonName, buttonNameExtension, gameState.glowingButton == buttonName);
                            }
                        }

                        if (action == "Auto" || action == "Autorun" || (buttonName == buttonCanvas.pressedButton && buttonNameExtension == buttonCanvas.pressedButtonExtension))
                        {
                            if (! sameFrame)
                            {
                                sameFrame = true;
                                Debug.Log("=======");
                            }
                            Debug.Log("--- Starting script: " + gameState.sceneName + " " + action + " " + target + " " + flags);
                            if (gameState.glowingButton == buttonName)
                                gameState.glowingButton = null;
                            if (action == "Autorun")
                                autorun[flags] = true;
                            dialogue = tsv.Lookup(isSubscene ? "-" : engine.currentlySeenARMarker.name, target, action, flags).ToArray();
                            dialogueLine = 0;
                            string lastSelectedItem = gameState.selectedItem;
                            if (DoDialogue() || ! isSubscene && gameState.currentlyInSubscene || lastSelectedItem != gameState.selectedItem)
                            {
                                actionExecuted = true;
                                staticButtonIndex = 0;
                                dynamicButtonIndex = 0;
                                break;
                            }
                        }
                    }
                    if (actionExecuted)
                        break;
                }
                if (actionExecuted)
                    break;
            }
        }

        if (actionExecuted)
        {
            staticButtonIndex = 0;
            dynamicButtonIndex = 0;
            buttonCanvas.SetCrosshair(new Vector3(-1f, -1f), new Vector3(-1f, -1f));
            while (staticButtonIndex != ButtonCanvasBehaviour.NUM_BUTTONS_PER_GROUP)
                buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, staticButtonIndex++, null);
            while (dynamicButtonIndex != ButtonCanvasBehaviour.NUM_BUTTONS_PER_GROUP)
                buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, dynamicButtonIndex++, null);
            return;
        }
        else if (sameFrame)
            Debug.Log("--- Ending script on the same frame");

        // clear remaining buttons
        while (staticButtonIndex != ButtonCanvasBehaviour.NUM_BUTTONS_PER_GROUP)
            buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, staticButtonIndex++, null);
        while (dynamicButtonIndex != ButtonCanvasBehaviour.NUM_BUTTONS_PER_GROUP && (currentObject == objectInFocus || ! buttonCanvas.showDynamicGroup))
            buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, dynamicButtonIndex++, null);
    }

    private static void SetLayer(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayer(child.gameObject, layer);
    }

    private void ProcessSceneTimeCounter()
    {
        if (! isSubscene && ! gameState.GetFlag(gameState.sceneName + "%End"))
        {
            if (gameState.GetFlagIntValue("Global%SceneTimeCounter") != 0)
            {
                if ((long) Time.realtimeSinceStartup != lastSceneTimeCounterUpdateTime)
                {
                    if (gameState.GetFlagIntValue("Global%SceneTimeCounter") == 1)
                        gameState.EncodeAnalyticsBeginScene(gameState.sceneName, ! startWithoutMarker);

                    // regardless of actual time passed, only increment per actual gameplay second
                    // (since player may have turned the device off in the interim)
                    lastSceneTimeCounterUpdateTime = (long) Time.realtimeSinceStartup;
                    gameState.SetFlag("Global%SceneTimeCounter", gameState.GetFlagIntValue("Global%SceneTimeCounter") + 1, false);

                    if (gameState.GetFlagIntValue("Global%SceneTimeCounter") == 2)
                        gameState.SaveFlags();  // need to save the fact that the scene was begun successfully
                }
            }
        }
    }

    private void ProcessSceneTimeCounterEnd()
    {
        if (! isSubscene && gameState.GetFlag(gameState.sceneName + "%End"))
        {
            if (gameState.GetFlagIntValue("Global%SceneTimeCounter") != 0)
                gameState.EncodeAnalyticsEndScene(true);
        }
    }
}
