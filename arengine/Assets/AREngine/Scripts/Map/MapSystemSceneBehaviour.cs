/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.IO;
# if UNITY_EDITOR
using UnityEditor;
# endif
#endif

public class MapSystemSceneBehaviour : SceneBehaviour
{
    public Color waypointColor            = Color.blue;
    public Color hotspotColor             = Color.red;
    public Color selectedColor            = Color.magenta;
    public Color segmentColor             = Color.gray;
    public Color directionsColor          = Color.cyan;

    public float enterRadiusSquared       = 500.0f;
    public string backgroundMusic         = "BGM6";

    public string selectMarkerStatus      = "Tap a red marker, then navigate to it in the real world!";
    public string navigateMarkerStatus    = "Switch to compass view by tapping Navigate!";
    public string followMarkerStatus      = "Follow the map to $. Stay alert on the road!";
    public string enterMarkerStatus       = "MapStatuses/$#Locate the marker area, as shown below, then tap Enter to play!";
    public string notWithinMapStatus      = "You must physically visit the game's geographical area to play this game.";
    public string selectModuleStatus      = "Tap a red marker to replay a module!";

    public string notWithinMapPrompt      = "WARNING: Your GPS is reporting that you are not inside the game's geographical area.\n\nYou will need to take a photo of the marker at $. Take a photo now?";
    public string notWithinMarkerPrompt   = "WARNING: Your GPS is reporting that you are around $, but not close to the marker.\n\nYou will need to take a photo of the marker at $. Take a photo now?";
    public string finishedGamePrompt      = "Congratulations! You have finished the game!\n\nYou may now select any module of the game to replay it. To choose a module, tap one of the markers on the map.";
    public string normalResetPrompt       = "Replaying $. Do you wish to proceed?";
    public string specialResetPrompt      = "Replaying $. Since this module is marked \"special\", you are seeing this message instead. (Blame your programmer for not changing this string.) Do you wish to proceed?";

    private MapSystemBehaviour mapSystem;
    private TSVLookup tsv;
    private bool hasIncrementedModuleNumber = false;
    private string currentWaypoint = null;
    private string newWaypoint = null;
    private string autoWaypoint = null;
    private bool gameEnded = false;
    private bool gameEndFlag = false;
    private float zoomCountdown = 0.0f;
    private bool playedMusic = false;
    private List<string> activeWaypoints = new List<string>();
    private int numWaypoints;
    private float[,] adjacencyMatrix;
    private float[] distances;
    private int[] gatewayPoints;
    private bool[] visited;
    private int nearestWaypoint = -1;
    private int targetWaypoint = -1;
    private Vector3 lastGPSPosition = Vector3.zero;
    private float timeSinceLastGPSPosition = 0.0f;
    private string nearestSegmentSource;
    private string nearestSegmentDest;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private bool editMode = false;
    private bool join = false;
    private bool split = false;
    private bool delete = false;
    private bool edited = false;
    private float savedScaleForMaxZoom;
    private Vector3 currentWaypointPosition = Vector3.negativeInfinity;
#endif

    private void CreateWaypoint(string name, Vector3 position, string label)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (editMode)
        {
            mapSystem.CreateWaypoint(name, Resources.Load<Sprite>("Map/MapMarker"), position);
            mapSystem.SetWaypointLabel(name, label == "" ? null : label);
            mapSystem.SetWaypointColor(name, label == "" ? waypointColor : hotspotColor);
        }
        else
#endif
        {
            string activeScene = null;
            foreach (string scene in tsv.Lookup(label))
            {
                string flags = tsv.Lookup(label, scene)[0];
                if (scene[0] == 'M')
                {
                    flags = flags.Replace("|", " !" + scene + "%End|");
                    flags = flags + " !" + scene + "%End";
                }
                if (gameState.EvaluateFlags(flags) || gameState.EvaluateFlags("Global%MarkerTest"))
                {
                    activeScene = scene;
                    break;
                }
            }
            if (activeScene != null)
            {
                if (activeScene[0] != 'M')
                    gameEnded = true;
                activeWaypoints.Add(name);
                mapSystem.CreateWaypoint(name, Resources.Load<Sprite>("Map/MapMarker"), position);
                mapSystem.SetWaypointLabel(name, label + ";" + activeScene);
                mapSystem.SetWaypointColor(name, hotspotColor);
            }
            else
            {
                mapSystem.CreateWaypoint(name, null, position);
            }
        }
    }

    private void Start()
    {
        if (! Init())
            return;
        mapSystem = GetComponent<MapSystemBehaviour>();
        tsv = new TSVLookup("MapTSV");

        // track the highest module ever attained
        if (gameState.GetFlagIntValue("Global%Module") > gameState.GetFlagIntValue("Global%HighestModule"))
        {
            gameState.SetFlag("Global%HighestModule", gameState.GetFlagIntValue("Global%Module"));
        }

        // if we are replaying a module but the game has moved past that module,
        // go back to the highest module (which should be the ending screen)
        if (gameState.GetFlagIntValue("Global%Module") != gameState.GetFlagIntValue("Global%ReplayModule"))
        {
            gameState.SetFlag("Global%GameEnd", false);  // Global%GameEnd == true unhides the Replay Ending button
            gameState.SetFlag("Global%Module", gameState.GetFlagIntValue("Global%HighestModule"));
        }

        // reinitialize locals
        currentWaypoint = newWaypoint = autoWaypoint = null;
        gameEnded = false;
        gameEndFlag = gameState.GetFlag("Global%GameEnd");
        zoomCountdown = 2.0f;
        activeWaypoints = new List<string>();

        mapSystem.DeleteAllWaypoints();
        mapSystem.DeleteAllSegments();

        // read map position data
        string mapData = "0\n0\n";
        TextAsset asset = Resources.Load<TextAsset>("MapData");
        if (asset != null)
            mapData = asset.text;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
# if UNITY_EDITOR
        string dir = "Assets/ARGames/" + DeviceInput.GameName() + "/Resources";
# else
        string dir = Application.persistentDataPath;
# endif
        try
        {
            StreamReader reader = new StreamReader(dir + "/MapData.txt");
            mapData = reader.ReadToEnd();
            reader.Close();
# if ! UNITY_EDITOR
            Debug.Log("WARNING: Currently using new MapData.txt in " + Application.persistentDataPath + ". Don't forget to copy this file over to ARGames/" + DeviceInput.GameName() + "/Resources to make this permanent!");
# endif
        }
        catch (Exception) {}
#endif
        string[] mapStrings = mapData.Replace("\r\n", "\n").Split('\n');
        int line = 0;

        // read waypoints
        int num = int.Parse(mapStrings[line++]);
        numWaypoints = 0;
        for (int i = 0; i < num; i++)
        {
            string name = mapStrings[line++];
            string label = mapStrings[line++];
            float x = float.Parse(mapStrings[line++]);
            float z = float.Parse(mapStrings[line++]);
            CreateWaypoint(name, new Vector3(x, 0.0f, z), label);
            if (int.Parse(name) >= numWaypoints)
                numWaypoints = int.Parse(name) + 1;   // track highest index so far...
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (editMode)
        {
        }
        else
#endif
        if (activeWaypoints.Count == 0)
        {
            // if not in edit mode and there are no active waypoints, we have reached the end of a module
            // and should increment the module number and reread the waypoints
            if (! hasIncrementedModuleNumber)
            {
                hasIncrementedModuleNumber = true;
                gameState.SetFlag("Global%Module", gameState.GetFlagIntValue("Global%Module") + 1);
                Start();
                return;
            }
            else
            {
                Debug.Log("WARNING: MapTSV.txt did not contain any activatable scenes for module " + gameState.GetFlagIntValue("Global%Module") + "!");

                // prevent infinite loop by not allowing the module number to increment more than once
                if (gameState.GetFlagIntValue("Global%Module") == gameState.GetFlagIntValue("Global%HighestModule"))
                    gameState.SetFlag("Global%HighestModule", gameState.GetFlagIntValue("Global%HighestModule") - 1);
                gameState.SetFlag("Global%Module", gameState.GetFlagIntValue("Global%Module") - 1);
            }
        }
        hasIncrementedModuleNumber = false;

        // ... to build pathfinding structures
        adjacencyMatrix = new float[numWaypoints, numWaypoints];
        distances = new float[numWaypoints];
        gatewayPoints = new int[numWaypoints];
        visited = new bool[numWaypoints];
        nearestWaypoint = targetWaypoint = -1;
        lastGPSPosition = Vector3.zero;
        timeSinceLastGPSPosition = 0.0f;
        nearestSegmentSource = nearestSegmentDest = null;

        // read segments
        int numSegments = int.Parse(mapStrings[line++]);
        for (int i = 0; i < numSegments; i++)
        {
            string source = mapStrings[line++];
            string dest = mapStrings[line++];
            mapSystem.CreateSegment(source, dest, 3.0f, segmentColor, 1);
            adjacencyMatrix[int.Parse(source), int.Parse(dest)] = Mathf.Sqrt(mapSystem.GetSegmentLengthSquared(source, dest));
            adjacencyMatrix[int.Parse(dest), int.Parse(source)] = Mathf.Sqrt(mapSystem.GetSegmentLengthSquared(source, dest));
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (editMode)
        {
            mapSystem.CameraCenterNudge(Vector2.zero);
            edited = false;
            mapSystem.SetDeviceShowing(true);
            mapSystem.SetWaypointDragging(true);
            buttonCanvas.SetFade(new Color(0, 0, 0, 0), 0);
            buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 0, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 2, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 0, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 1, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 2, null);

            // visit all waypoints once to detect bad ones
            foreach (string waypoint in mapSystem.GetAllWaypoints())
                SetCurrentWaypoint(waypoint);
            SetCurrentWaypoint(null);
        }
        else
#endif
        {
            mapSystem.CameraCenterNudge(new Vector2(0.0f, 25.0f));
            mapSystem.SetDeviceShowing(! gameEnded);
            mapSystem.SetWaypointDragging(false);
            buttonCanvas.SetFade(new Color(0, 0, 0, 0), 0);
            buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 2, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 1, null);
            buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 2, null);

            if (gameState.GetFlag("M0%Scene1%End") && gameEnded)
            {
                playedMusic = true;
                buttonCanvas.StopMusic();
                buttonCanvas.PreloadSound(backgroundMusic);
                buttonCanvas.ShowQuestionOverlay(
                    finishedGamePrompt,
                    "Continue playing",
                    null,
                    delegate(string pressedButton)
                    {
                        buttonCanvas.HideOverlay();
                        buttonCanvas.PlayMusic(backgroundMusic);
                    }
                );
            }
        }
        buttonCanvas.showDynamicGroup = false;

        if (gameState.GetFlag("M0%Scene1%End") && ! playedMusic)
        {
            buttonCanvas.PlayMusic(backgroundMusic);
            playedMusic = true;
        }
    }

    private void Pathfinder()
    {
        int target = -1;
        if (currentWaypoint != null && ! gameEnded && mapSystem.IsDeviceGPSPositionWithinMap())
            target = int.Parse(currentWaypoint);

        float distance, lerp;
        Vector3 currentGPSPosition = mapSystem.GetDeviceGPSPosition();
        if (currentGPSPosition != lastGPSPosition || target != targetWaypoint)
        {
            lastGPSPosition = currentGPSPosition;
            timeSinceLastGPSPosition = Time.realtimeSinceStartup;
            mapSystem.SetSegmentColor(nearestSegmentSource, nearestSegmentDest, segmentColor, 1);
            mapSystem.GetSegmentClosestToPoint(lastGPSPosition, out nearestSegmentSource, out nearestSegmentDest, out distance, out lerp);

            for (int i = targetWaypoint; i != -1 && i != nearestWaypoint; i = gatewayPoints[i])
            {
                mapSystem.SetSegmentColor("" + i, "" + gatewayPoints[i], segmentColor, 1);
                mapSystem.SetSegmentColor("" + gatewayPoints[i], "" + i, segmentColor, 1);
            }

            if (nearestSegmentSource != null && nearestSegmentDest != null)
                nearestWaypoint = int.Parse(lerp < 0.5f ? nearestSegmentSource : nearestSegmentDest);
            else
                nearestWaypoint = -1;
            targetWaypoint = target;

            for (int i = 0; i < numWaypoints; i++)
            {
                if (i == nearestWaypoint)
                    distances[i] = 0.0f;
                else
                    distances[i] = float.PositiveInfinity;
                gatewayPoints[i] = -1;
                visited[i] = false;
            }

            while (true)
            {
                float minimum = float.PositiveInfinity;
                int waypoint = -1;
                for (int i = 0; i < numWaypoints; i++)
                {
                    if (distances[i] < minimum && ! visited[i])
                    {
                        waypoint = i;
                        minimum = distances[i];
                    }
                }

                if (waypoint == -1 || waypoint == targetWaypoint)
                    break;

                visited[waypoint] = true;

                for (int i = 0; i < numWaypoints; i++)
                {
                    if (i == waypoint)
                        continue;

                    if (adjacencyMatrix[waypoint, i] != 0.0f
                        && distances[i] > distances[waypoint] + adjacencyMatrix[waypoint, i])
                    {
                        distances[i] = distances[waypoint] + adjacencyMatrix[waypoint, i];
                        gatewayPoints[i] = waypoint;
                    }
                }
            }

            if (targetWaypoint != -1)
                mapSystem.SetSegmentColor(nearestSegmentSource, nearestSegmentDest, directionsColor, 2);
            else
                mapSystem.SetSegmentColor(nearestSegmentSource, nearestSegmentDest, segmentColor, 1);
            for (int i = targetWaypoint; i != -1 && i != nearestWaypoint; i = gatewayPoints[i])
            {
                mapSystem.SetSegmentColor("" + i, "" + gatewayPoints[i], directionsColor, 2);
                mapSystem.SetSegmentColor("" + gatewayPoints[i], "" + i, directionsColor, 2);
            }
        }
    }

    private void SetCurrentWaypoint(string waypoint)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        string source = "", dest = "";
        float distance = float.PositiveInfinity, lerp = 0.0f;
        if (currentWaypoint != null)
            mapSystem.GetSegmentClosestToPoint(mapSystem.GetWaypointPosition(currentWaypoint), out source, out dest, out distance, out lerp, currentWaypoint);
        if (distance < 25.0f)
        {
            mapSystem.SetWaypointColor(currentWaypoint, Color.yellow);
            Debug.Log("Bad waypoint '" + currentWaypoint + "' found with distance " + distance + " from " + source + "->" + dest + "(" + lerp + ")");
        }
        else if (mapSystem.GetWaypointLabel(currentWaypoint) == null)
            mapSystem.SetWaypointColor(currentWaypoint, waypointColor);
        else
            mapSystem.SetWaypointColor(currentWaypoint, hotspotColor);
#else
        mapSystem.SetWaypointColor(currentWaypoint, hotspotColor);
#endif
        currentWaypoint = waypoint;
        mapSystem.SetWaypointColor(currentWaypoint, selectedColor);
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void SpawnEditorWaypoint(Vector3 position)
    {
        int count = 0;
        while (! mapSystem.GetWaypointPosition("" + count).Equals(Vector3.negativeInfinity))
            count++;
        mapSystem.CreateWaypoint("" + count, Resources.Load<Sprite>("Map/MapMarker"), position);
        if (join)
            mapSystem.CreateSegment(currentWaypoint, "" + count, 3.0f, segmentColor, 1);
        SetCurrentWaypoint("" + count);
        currentWaypointPosition = mapSystem.GetWaypointPosition(currentWaypoint);
        mapSystem.IgnoreTouchesUntilReleased();
        edited = true;
    }
#endif

    private void Update()
    {
        if (! Init())
            return;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (editMode)
        {
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
            {
                join = true;
                split = false;
                delete = false;
                SetCurrentWaypoint(null);
            }
            if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
            {
                join = false;
            }
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
            {
                join = false;
                split = true;
                delete = false;
                SetCurrentWaypoint(null);
            }
            if (Input.GetKeyUp(KeyCode.LeftControl) || Input.GetKeyUp(KeyCode.RightControl))
            {
                split = false;
            }
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                join = false;
                split = false;
                delete = true;
                SetCurrentWaypoint(null);
            }
            if (Input.GetKeyUp(KeyCode.Delete))
            {
                delete = false;
            }

            if (Input.GetMouseButtonDown(2) && ! delete && ! split)
            {
#if UNITY_EDITOR
                SpawnEditorWaypoint(mapSystem.GetTouchedPosition());
#endif
            }
            else if (mapSystem.GetTouchedWaypoint() != null)
            {
                if (delete)
                {
                    if (mapSystem.GetTouchedWaypoint() == currentWaypoint)
                        SetCurrentWaypoint(null);
                    mapSystem.DeleteWaypoint(mapSystem.GetTouchedWaypoint());
                    mapSystem.IgnoreTouchesUntilReleased();
                    edited = true;
                    delete = Input.GetKey(KeyCode.Delete);
                }
                else if ((join || split) && currentWaypoint != null && mapSystem.GetTouchedWaypoint() != currentWaypoint)
                {
                    mapSystem.DeleteSegment(mapSystem.GetTouchedWaypoint(), currentWaypoint);
                    mapSystem.DeleteSegment(currentWaypoint, mapSystem.GetTouchedWaypoint());
                    if (join)
                    {
                        mapSystem.CreateSegment(currentWaypoint, mapSystem.GetTouchedWaypoint(), 3.0f, segmentColor, 1);
                        join = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    }
                    else
                        split = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    SetCurrentWaypoint(mapSystem.GetTouchedWaypoint());
                    currentWaypointPosition = mapSystem.GetWaypointPosition(currentWaypoint);
                    mapSystem.IgnoreTouchesUntilReleased();
                    edited = true;
                }
                else if (mapSystem.GetTouchedWaypoint() != currentWaypoint)
                {
                    SetCurrentWaypoint(mapSystem.GetTouchedWaypoint());
                    currentWaypointPosition = mapSystem.GetWaypointPosition(currentWaypoint);
                    mapSystem.IgnoreTouchesUntilReleased();
                }
            }

            if (currentWaypoint != null && currentWaypointPosition != mapSystem.GetWaypointPosition(currentWaypoint))
            {
                currentWaypointPosition = mapSystem.GetWaypointPosition(currentWaypoint);
                edited = true;
            }
        }
        else
#endif
        {
            if (gameState.GetFlag("Global%GameEnd") != gameEndFlag)
            {
                // restart when the flag changes
                Start();
                return;
            }

            if (! gameState.GetFlag("M0%Scene1%End"))
                gameState.LoadARScene("M0%Scene1");

            if (gameState.loadingNewScene)
            {
                if (currentWaypoint != null)
                {
                    buttonCanvas.SetDynamicGroupOrigin(
                        Camera.main.WorldToScreenPoint(mapSystem.GetWaypointPosition(currentWaypoint))
                    );
                }
                buttonCanvas.showDynamicGroup = false;
                return;
            }

            if (buttonCanvas.overlayShowing)
                mapSystem.IgnoreTouchesUntilReleased();

            // countdown before zooming out
            if (zoomCountdown == 2.0f)
            {
                mapSystem.ShowWaypoints(activeWaypoints);
                mapSystem.IgnoreTouchesUntilReleased();
                if (gameEnded)
                    zoomCountdown = 0.0f;
                else
                    zoomCountdown -= Time.deltaTime;
            }
            else if (zoomCountdown > 0.0f)
            {
                zoomCountdown -= Time.deltaTime;
                if (zoomCountdown <= 0.0f)
                {
                    // once the user's gps position is validated, zoom out to show it
                    if (mapSystem.IsDeviceGPSPositionValidated())
                    {
                        mapSystem.ShowWaypoints(activeWaypoints, true);
                        zoomCountdown = 0.0f;
                    }
                    else
                        zoomCountdown = 0.001f;
                }
            }

            // process touching waypoints
            if (mapSystem.GetTouchedWaypoint() != null && newWaypoint == null)
            {
                if (currentWaypoint != mapSystem.GetTouchedWaypoint())
                {
                    newWaypoint = mapSystem.GetTouchedWaypoint();
                }

                mapSystem.ShowWaypoints(activeWaypoints, true);

                mapSystem.IgnoreTouchesUntilReleased();
            }

            if (newWaypoint != null)
            {
                if (buttonCanvas.showDynamicGroup && autoWaypoint != newWaypoint)
                {
                    buttonCanvas.SetDynamicGroupOrigin(
                        Camera.main.WorldToScreenPoint(mapSystem.GetWaypointPosition(currentWaypoint))
                    );
                    buttonCanvas.showDynamicGroup = false;
                }
                else
                {
                    SetCurrentWaypoint(newWaypoint);
                    newWaypoint = null;
                }
            }

            // show appropriate button for a selected waypoint
            if (currentWaypoint != null && newWaypoint == null)
            {
                buttonCanvas.SetDynamicGroupOrigin(
                    Camera.main.WorldToScreenPoint(mapSystem.GetWaypointPosition(currentWaypoint))
                );
                if (gameEnded)
                {
                    buttonCanvas.showDynamicGroup = true;
                    buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 0, "Replay",
                                           mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[1].Split('_')[1]);
                }
                else
                {
                    bool enter = mapSystem.IsDeviceGPSPositionValidated();
                    float distance = (mapSystem.GetDeviceGPSPosition()
                                      - mapSystem.GetWaypointPosition(currentWaypoint)).sqrMagnitude;
                    if (distance <= enterRadiusSquared)
                    {
                        // since we're within distance, do not allow the warning dialog to display after 30 seconds if player doesn't move
                        timeSinceLastGPSPosition = Time.realtimeSinceStartup;
                    }
                    else if (distance <= enterRadiusSquared * 4)
                    {
                        // only allow entering if player has not moved for at least 30 seconds
                        if (Time.realtimeSinceStartup - timeSinceLastGPSPosition < 30.0f)
                            enter = false;
                    }
                    else
                    {
                        enter = false;
                    }

                    if (enter)
                    {
                        buttonCanvas.showDynamicGroup = true;
                        buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 0, "Enter",
                                            mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[0]);
                        if (mapSystem.IsFollowing())
                        {
                            List<string> waypoints = new List<string>();
                            waypoints.Add(currentWaypoint);
                            mapSystem.ShowWaypoints(waypoints, true);  // this resets top view at max zoom
                            mapSystem.Recenter();
                        }
                        else
                        {
                            buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS,
                                enterMarkerStatus.Replace(
                                    "$",
                                    mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[0]
                                )
                            );
                        }
                    }
                    else
                    {
                        buttonCanvas.showDynamicGroup = ! mapSystem.IsFollowing();
                        if (! mapSystem.IsFollowing())
                        {
                            buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 0, "Navigate to",
                                                mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[0]);
                            buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS,
                                navigateMarkerStatus.Replace(
                                    "$",
                                    mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[0]
                                )
                            );
                        }
                        else
                        {
                            buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS,
                                followMarkerStatus.Replace(
                                    "$",
                                    mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[0]
                                )
                            );
                        }
                    }
                }
            }

            // if very close to a waypoint that's not selected, show Enter anyway
            autoWaypoint = null;
            if (! gameEnded && currentWaypoint == null && mapSystem.IsDeviceGPSPositionValidated())
            {
                buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, selectMarkerStatus);
                float minDistance = float.PositiveInfinity;
                foreach (string waypoint in activeWaypoints)
                {
                    float distance = (mapSystem.GetDeviceGPSPosition()
                                      - mapSystem.GetWaypointPosition(waypoint)).sqrMagnitude;
                    if (minDistance > distance)
                    {
                        autoWaypoint = waypoint;
                        minDistance = distance;
                    }
                }

                if (minDistance <= enterRadiusSquared)
                {
                    // since we're within distance, do not allow the warning dialog to display after 30 seconds if player doesn't move
                    timeSinceLastGPSPosition = Time.realtimeSinceStartup;
                }
                else if (minDistance <= enterRadiusSquared * 4)
                {
                    // only allow autowaypoint to persist if player has not moved for at least 30 seconds
                    if (Time.realtimeSinceStartup - timeSinceLastGPSPosition < 30.0f)
                        autoWaypoint = null;
                }
                else
                {
                    autoWaypoint = null;
                }

                if (autoWaypoint != null)
                {
                    buttonCanvas.SetDynamicGroupOrigin(
                        Camera.main.WorldToScreenPoint(mapSystem.GetWaypointPosition(autoWaypoint))
                    );
                    buttonCanvas.showDynamicGroup = true;
                    buttonCanvas.SetButton(ButtonCanvasGroup.DYNAMIC, 0, "Enter",
                                            mapSystem.GetWaypointLabel(autoWaypoint).Split(';')[0]);
                    buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS,
                        enterMarkerStatus.Replace(
                            "$",
                            mapSystem.GetWaypointLabel(autoWaypoint).Split(';')[0]
                        )
                    );
                }
            }

            if (gameEnded)
                buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, selectModuleStatus);
            else if (! mapSystem.IsDeviceGPSPositionWithinMap())
                buttonCanvas.SetStatus(ButtonCanvasStatusType.PROGRESS, notWithinMapStatus);

            // static buttons are always there
            buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 0, "Options");
            if (! gameEnded && zoomCountdown == 0.0f && ! mapSystem.IsCentered() && mapSystem.IsDeviceGPSPositionWithinMap())
                buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, "Recenter");
            else if (! gameEnded && zoomCountdown == 0.0f && ! mapSystem.IsTopView() && mapSystem.IsDeviceGPSPositionWithinMap())
                buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, "Top view");
            else if (! gameEnded && zoomCountdown == 0.0f && currentWaypoint != null)
                buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, "Cancel", "navigation");
            else
                buttonCanvas.SetButton(ButtonCanvasGroup.STATIC, 1, null);

            // process pressed buttons
            string sceneName;
            if (buttonCanvas.pressedButton == "Options")
                buttonCanvas.ShowOptionsOverlay();
            else if (buttonCanvas.pressedButton == "Recenter")
                mapSystem.Recenter();
            else if (buttonCanvas.pressedButton == "Top view")
            {
                if (mapSystem.IsFollowing())
                    mapSystem.ShowWaypoints(activeWaypoints, true);
                else
                    mapSystem.TopView();
            }
            else if (buttonCanvas.pressedButton == "Cancel")
            {
                mapSystem.ShowWaypoints(activeWaypoints, true);
                SetCurrentWaypoint(null);
            }
            else if (buttonCanvas.pressedButton == "Navigate to")
            {
                if (! mapSystem.IsDeviceGPSPositionWithinMap())
                {
                    buttonCanvas.ShowQuestionOverlay(
                        notWithinMapPrompt.Replace("$", mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[0]),
                        "Proceed",
                        "Don't Proceed",
                        delegate(string pressedButton)
                        {
                            buttonCanvas.HideOverlay();

                            if (pressedButton == "Proceed")
                            {
                                gameState.SetFlag("Global%GPSValid", false);
                                sceneName = mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[1];
                                UnityAnalyticsIntegration.SceneEnter(gameState, sceneName, 0.0f, 0.0f, false);
                                gameState.LoadARScene(sceneName);
                            }
                        }
                    );
                }
                else
                    mapSystem.Follow();
            }
            else if (buttonCanvas.pressedButton == "Enter")
            {
                if (currentWaypoint == null)
                    SetCurrentWaypoint(autoWaypoint);
                if (Time.realtimeSinceStartup - timeSinceLastGPSPosition >= 30.0f)
                {
                    buttonCanvas.ShowQuestionOverlay(
                        notWithinMarkerPrompt.Replace("$", mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[0]),
                        "Proceed",
                        "Don't Proceed",
                        delegate(string pressedButton)
                        {
                            buttonCanvas.HideOverlay();

                            if (pressedButton == "Proceed")
                            {
                                gameState.SetFlag("Global%GPSValid", true);
                                sceneName = mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[1];
                                UnityAnalyticsIntegration.SceneEnter(gameState, sceneName, 0.0f, 0.0f, false);  // report as bad gps
                                gameState.LoadARScene(sceneName);
                            }
                        }
                    );
                }
                else
                {
                    gameState.SetFlag("Global%GPSValid", true);
                    sceneName = mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[1];
                    UnityAnalyticsIntegration.SceneEnter(gameState, sceneName, 0.0f, 0.0f);
                    gameState.LoadARScene(sceneName);
                }
            }
            else if (buttonCanvas.pressedButton == "Replay")
            {
                buttonCanvas.ShowQuestionOverlay(
                    mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[1].Split('_')[0][0] == 'R'
                        ? normalResetPrompt.Replace("$", mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[1].Split('_')[1])
                        : specialResetPrompt.Replace("$", mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[1].Split('_')[1]),
                    "Proceed",
                    "Don't Proceed",
                    delegate(string pressedButton)
                    {
                        buttonCanvas.HideOverlay();

                        if (pressedButton == "Proceed")
                        {
                            char module = mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[1].Split('_')[0][1];
                            gameState.SetFlag("Global%GameEnd", true);
                            gameState.SetFlag("Global%Module", module == 'Z' ? 0 : module - '0');
                            gameState.SetFlag("Global%ReplayModule", module == 'Z' ? 0 : module - '0');
                            gameState.ResetFlags(gameState.GetFlagsStartingWith("M" + module + "%"));
                        }
                    }
                );
            }
            else if (currentWaypoint == null && autoWaypoint == null)
            {
                buttonCanvas.showDynamicGroup = false;
            }

            Pathfinder();
        }
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void OnGUI()
    {
        if (GUIUtility.hotControl != 0)
            mapSystem.IgnoreTouchesUntilReleased();

        if (editMode)
        {
            if (! edited)
            {
                if (GUI.Button(new Rect(Screen.width - 100.0f, 0.0f, 100.0f, 100.0f), "Play Mode"))
                {
                    gameState.SetFlag("Global%MarkerTest", false);
                    editMode = false;
                    mapSystem.scaleForMaxZoom = savedScaleForMaxZoom;
                    Start();
                    return;
                }
                else if (GUI.Button(new Rect(Screen.width - 100.0f, 200.0f, 100.0f, 100.0f), "Marker Test"))
                {
                    gameState.SetFlag("Global%MarkerTest", true);
                    editMode = false;
                    mapSystem.scaleForMaxZoom = savedScaleForMaxZoom;
                    Start();
                    return;
                }
            }
            else
            {
                if (GUI.Button(new Rect(Screen.width - 100.0f, 0.0f, 100.0f, 100.0f), "Revert"))
                {
                    Start();
                    return;
                }
                else if (GUI.Button(new Rect(Screen.width - 100.0f, 200.0f, 100.0f, 100.0f), "Save"))
                {
                    try
                    {
# if UNITY_EDITOR
                        string dir = "Assets/ARGames/" + DeviceInput.GameName() + "/Resources";
# else
                        string dir = Application.persistentDataPath;
# endif
                        StreamWriter writer = new StreamWriter(dir + "/MapData.txt");
                        writer.WriteLine(mapSystem.GetAllWaypoints().Count);
                        foreach (string waypoint in mapSystem.GetAllWaypoints())
                        {
                            writer.WriteLine(waypoint);
                            writer.WriteLine(mapSystem.GetWaypointLabel(waypoint));
                            writer.WriteLine(mapSystem.GetWaypointPosition(waypoint).x);
                            writer.WriteLine(mapSystem.GetWaypointPosition(waypoint).z);
                        }
                        writer.WriteLine(mapSystem.GetAllSegments().Count);
                        foreach (KeyValuePair<string, string> segment in mapSystem.GetAllSegments())
                        {
                            writer.WriteLine(segment.Key);
                            writer.WriteLine(segment.Value);
                        }
                        writer.Close();
                    }
                    catch (Exception) {}

                    Start();
                    return;
                }
            }
            if (GUI.Button(new Rect(0.0f, 0.0f, 100.0f, 100.0f), "Create"))
            {
                SpawnEditorWaypoint(mapSystem.GetDeviceGPSPosition());
            }
            if (GUI.Button(new Rect(100.0f, 0.0f, 100.0f, 100.0f), "Join " + (join ? "ON" : "OFF")))
            {
                join = ! join;
                split = false;
                delete = false;
                SetCurrentWaypoint(null);
            }
            if (GUI.Button(new Rect(200.0f, 0.0f, 100.0f, 100.0f), "Split " + (split ? "ON" : "OFF")))
            {
                join = false;
                split = ! split;
                delete = false;
                SetCurrentWaypoint(null);
            }
            if (GUI.Button(new Rect(300.0f, 0.0f, 100.0f, 100.0f), "Delete " + (delete ? "ON" : "OFF")))
            {
                join = false;
                split = false;
                delete = ! delete;
                SetCurrentWaypoint(null);
            }
            if (currentWaypoint != null)
            {
                string label = mapSystem.GetWaypointLabel(currentWaypoint);
                label = GUI.TextField(new Rect(0.0f, 100.0f, 400.0f, 50.0f), label);
                if (label != mapSystem.GetWaypointLabel(currentWaypoint))
                {
                    edited = true;
                    mapSystem.SetWaypointLabel(currentWaypoint, label == "" ? null : label);
                }
            }
            if (! mapSystem.IsCentered())
            {
                if (GUI.Button(new Rect(0.0f, 200.0f, 100.0f, 100.0f), "Recenter"))
                {
                    mapSystem.Recenter();
                    SetCurrentWaypoint(null);
                }
            }
            else if (! mapSystem.IsTopView())
            {
                if (GUI.Button(new Rect(0.0f, 200.0f, 100.0f, 100.0f), "Top View"))
                {
                    mapSystem.TopView();
                    SetCurrentWaypoint(null);
                }
            }
        }
#if UNITY_EDITOR
        else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
#else
        else
#endif
        {
            if (GUI.Button(new Rect(Screen.width - 100.0f, 0.0f, 100.0f, 100.0f), "Edit Mode"))
            {
                editMode = true;
                savedScaleForMaxZoom = mapSystem.scaleForMaxZoom;
                mapSystem.scaleForMaxZoom = 0.25f;
                Start();
                return;
            }
            if (! gameEnded && ! gameState.GetFlag("Global%MarkerTest"))
            {
                if (GUI.Button(new Rect(0.0f, 0.0f, 200.0f, 100.0f), "Finish Module " + gameState.GetFlagIntValue("Global%Module")))
                {
                    gameState.SetFlag("Global%Module", gameState.GetFlagIntValue("Global%Module") + 1);
                    Start();
                    return;
                }
                if (currentWaypoint != null && GUI.Button(new Rect(0.0f, 100.0f, 200.0f, 100.0f), "Finish " + mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[1]))
                {
                    gameState.SetFlag(mapSystem.GetWaypointLabel(currentWaypoint).Split(';')[1] + "%End", true);
                    Start();
                    return;
                }
            }
        }
    }
#endif
}
