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
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

public class MapSystemBehaviour : MonoBehaviour
{
    public float  gpsTimeout               = 30.0f;       // seconds before GPS location is considered old
    public int    tileZoomLevel            = 17;          // must be from 1 - 18
    public float  centerTileLongitude      = 121.0778f;   // default world center is ADMU
    public float  centerTileLatitude       = 14.63889f;
    public int    additionalTilesWest      = 2;           // grab enough tiles around the center tile
    public int    additionalTilesNorth     = 3;           // to cover the whole campus
    public int    additionalTilesEast      = 2;
    public int    additionalTilesSouth     = 2;

    public float  angleForFollowElevation  = 60.0f;       // elevation angle during follow mode
    public float  angleForMaxElevation     = 90.0f;       // least-angled elevation view (90 degrees = flat)
    public float  angleForMinElevation     = 50.0f;       // most-angled elevation view
    public float  scaleForCenterZoom       = 2.0f;        // number of visible tiles vertically (when flat)
    public float  scaleForFollowZoom       = 1.0f;        // scale during follow mode
    public float  scaleForMaxZoom          = 1.0f;        // scale when most-zoomed-in
    public float  scaleForMinZoom          = 16.0f;       // scale when most-zoomed-out
    public float  numScreenLengthDivisions = 24.0f;       // pinch must travel 1/24th of screen to register
    public float  minDivisionsForRotation  = 3.0f;        // to rotate, pinch radius must >= 1/8th of screen
    public float  rotationAngleThreshold   = 0.9993908f;  // 2 degrees leeway for pinch to change rotation
    public float  elevationAngleThreshold  = 0.8660254f;  // 30 degrees leeway for pinch to change elevation
    public float  followDistanceFromCenter = 64.0f;       // 0.0f is center, 64.0f is halfway from bottom

    public float  distanceScalePower       = 0.5f;   // power function for sprite resizing due to distance
    public float  zoomScalePower           = 0.05f;  // power function for sprite resizing due to camera zoom
    public float  overallScaleFactor       = 1.5f;   // additional factor to apply to overall sprite resize

    public string tileServerURL            = "https://b.tile.openstreetmap.org/";
    public string attributionMessage       = "Map data by OpenStreetMap, under CC BY SA.";
    public string attributionURL           = "https://www.openstreetmap.org/copyright";
    public float  tilePixelsPerUnit        = 1.0f;   // normally 1.0f for OSM's 256x256 tiles

    public Color  backgroundColor          = new Color(0.25f, 0.5f, 0.75f, 1.0f);
    public Color  compassColor             = new Color(0.25f, 0.5f, 0.875f, 1.0f);
    public Color  gpsColor                 = new Color(0.25f, 0.5f, 0.875f, 0.25f);
    public Color  compassBadColor          = new Color(0.5f, 0.5f, 0.5f, 1.0f);
    public Color  gpsBadColor              = new Color(0.5f, 0.5f, 0.5f, 0.25f);

    private Transform cameraDolly;
    private Transform cameraMount;
    private Transform mapTiles;
    private Transform waypoints;
    private Transform segments;

    private int       delayedLerp      = 3;  // while > 0, app will not interpolate camera movement
    private bool      deviceShowing    = true;
    private bool      ignoreTouch      = false;
    private Vector2   previousTouch0   = Vector2.negativeInfinity;
    private Vector2   previousTouch1   = Vector2.negativeInfinity;
    private Vector2   currentTouch0    = Vector2.negativeInfinity;
    private Vector2   currentTouch1    = Vector2.negativeInfinity;
    private Vector2   touchDelta0      = Vector2.negativeInfinity;
    private Vector2   touchDelta1      = Vector2.negativeInfinity;
    private float     touchDeltaDot    = float.NegativeInfinity;
    private float     touchDot         = float.NegativeInfinity;

    private bool                       waypointDragging     = false;
    private Dictionary<string, string> waypointLabels       = new Dictionary<string, string>();
    private Dictionary<string, float>  waypointOrientations = new Dictionary<string, float>();
    private Dictionary<string, float>  waypointDiameters    = new Dictionary<string, float>();
    private string                     draggingWaypoint     = null;

    private double lastLocationTimestamp    = 0.0;
    private float  lastRealTime             = 0.0f;
    private bool   validated                = false;
    private bool   withinMap                = false;
    private bool   centered                 = true;
    private bool   following                = false;
    private float  filteredCompass          = 0.0f;
    private bool   processSegmentsThisFrame = false;

    public Vector2 GetGUIScale()
    {
        Vector2 guiScale;
        guiScale.x = 1280.0f;
        guiScale.y = Screen.height / (Screen.width / guiScale.x);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * (Screen.width / guiScale.x));
        return guiScale;
    }

    public void SetDeviceShowing(bool setting)
    {
        deviceShowing = setting;
    }

    public void IgnoreTouchesUntilReleased()
    {
        ignoreTouch = true;
    }

    public void SetWaypointDragging(bool setting)
    {
        waypointDragging = setting;
        if (! waypointDragging)
            draggingWaypoint = null;
    }

    public List<string> GetAllWaypoints()
    {
        List<string> result = new List<string>();
        foreach (Transform child in waypoints.GetComponentsInChildren<Transform>(true))
        {
            if (child != waypoints && ! child.gameObject.name.StartsWith("#mapsystem#"))
                result.Add(child.gameObject.name);
        }
        return result;
    }

    public List<KeyValuePair<string, string>> GetAllSegments()
    {
        List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
        foreach (Transform child in segments.GetComponentsInChildren<Transform>(true))
        {
            if (child != segments && ! child.gameObject.name.StartsWith("#mapsystem#"))
            {
                string source = child.gameObject.name.Split('\\')[0];
                string dest = child.gameObject.name.Split('\\')[1];

                result.Add(new KeyValuePair<string, string>(source, dest));
            }
        }
        return result;
    }

    public void DeleteAllWaypoints()
    {
        _DeleteAllChildren(waypoints);
        waypointLabels.Clear();
        waypointOrientations.Clear();
        waypointDiameters.Clear();
        SetWaypointOrientation("#mapsystem#compass", 0.0f);
        SetWaypointDiameter("#mapsystem#gps", 1.0f);
    }

    public void DeleteAllSegments()
    {
        _DeleteAllChildren(segments);
    }

    public void CreateWaypoint(string name, Sprite sprite, Vector3 position, bool clickable = true)
    {
        DeleteWaypoint(name);  // delete existing

        GameObject obj = new GameObject(name);
        obj.transform.parent = waypoints;
        obj.transform.position = ClampToMapExtremes(position);
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>() as SpriteRenderer;
        sr.sprite = sprite;
        if (! name.StartsWith("#mapsystem#") && sprite != null && clickable)
            obj.AddComponent<BoxCollider>();

        processSegmentsThisFrame = true;
    }

    public void DeleteWaypoint(string name)
    {
        if (name == null)
            return;
        Transform waypoint = waypoints.Find(name);
        if (waypoint == null || waypoint == waypoints)
            return;
        Destroy(waypoint.gameObject);
        waypointLabels.Remove(name);
        waypointOrientations.Remove(name);
        waypointDiameters.Remove(name);

        processSegmentsThisFrame = true;
    }

    public void SetWaypointColor(string name, Color color)
    {
        if (name == null)
            return;
        Transform waypoint = waypoints.Find(name);
        if (waypoint == null || waypoint == waypoints)
            return;
        waypoint.gameObject.GetComponent<SpriteRenderer>().color = color;
    }

    public void SetWaypointPosition(string name, Vector3 position)
    {
        if (name == null)
            return;
        Transform waypoint = waypoints.Find(name);
        if (waypoint == null || waypoint == waypoints)
            return;
        Vector3 realPosition = ClampToMapExtremes(position);
        if (waypoint.position.Equals(realPosition))
            return;
        waypoint.position = realPosition;

        processSegmentsThisFrame = true;
    }

    public Vector3 GetWaypointPosition(string name)
    {
        if (name == null)
            return Vector3.negativeInfinity;
        Transform waypoint = waypoints.Find(name);
        if (waypoint == null || waypoint == waypoints)
            return Vector3.negativeInfinity;
        return waypoint.position;
    }

    public Vector3 GetDeviceGPSPosition()
    {
        return GetWaypointPosition("#mapsystem#gps");
    }

    public bool IsDeviceGPSPositionValidated()
    {
        return validated;
    }

    public bool IsDeviceGPSPositionWithinMap()
    {
        return withinMap;
    }

    public bool IsTouchInitiated()
    {
        return NumTouches() == 1 && IsTouchStarting();
    }

    public Vector3 GetTouchedPosition()
    {
        if (currentTouch0.Equals(Vector2.negativeInfinity))
            return Vector3.negativeInfinity;
        return ScreenPointToWorld(currentTouch0);
    }

    public void SetWaypointLabel(string name, string label = null)
    {
        if (name == null)
            return;
        Transform waypoint = waypoints.Find(name);
        if (waypoint == null || waypoint == waypoints)
            return;
        if (label == null)
            waypointLabels.Remove(name);
        else
            waypointLabels[name] = label;
    }

    public string GetWaypointLabel(string name)
    {
        if (name == null)
            return null;
        Transform waypoint = waypoints.Find(name);
        if (waypoint == null || waypoint == waypoints || ! waypointLabels.ContainsKey(name))
            return null;
        return waypointLabels[name];
    }

    public void SetWaypointOrientation(string name, float orientation = float.NegativeInfinity)
    {
        if (name == null)
            return;
        Transform waypoint = waypoints.Find(name);
        if (waypoint == null || waypoint == waypoints)
            return;
        if (orientation == float.NegativeInfinity)
            waypointOrientations.Remove(name);
        else
            waypointOrientations[name] = orientation;
    }

    public float GetWaypointOrientation(string name)
    {
        if (name == null)
            return float.NegativeInfinity;
        Transform waypoint = waypoints.Find(name);
        if (waypoint == null || waypoint == waypoints || ! waypointOrientations.ContainsKey(name))
            return float.NegativeInfinity;
        return waypointOrientations[name];
    }

    public float GetDeviceCompassOrientation()
    {
        return GetWaypointOrientation("#mapsystem#compass");
    }

    public void SetWaypointDiameter(string name, float diameter = float.NegativeInfinity)
    {
        if (name == null)
            return;
        Transform waypoint = waypoints.Find(name);
        if (waypoint == null || waypoint == waypoints)
            return;
        if (diameter == float.NegativeInfinity)
            waypointDiameters.Remove(name);
        else
            waypointDiameters[name] = diameter;
    }

    public float GetWaypointDiameter(string name)
    {
        if (name == null)
            return float.NegativeInfinity;
        Transform waypoint = waypoints.Find(name);
        if (waypoint == null || waypoint == waypoints || ! waypointDiameters.ContainsKey(name))
            return float.NegativeInfinity;
        return waypointDiameters[name];
    }

    public float GetDeviceGPSErrorRadius()
    {
        return GetWaypointDiameter("#mapsystem#gps") / 2.0f;
    }

    public string GetTouchedWaypoint()
    {
        if (NumTouches() != 1 || ! IsTouchStarting() || currentTouch0.Equals(Vector2.negativeInfinity))
            return null;

        Ray ray = Camera.main.ScreenPointToRay(new Vector3(currentTouch0.x, currentTouch0.y, 0.0f));

        float closestDistance = float.PositiveInfinity;
        string name = null;
        foreach (RaycastHit hit in Physics.RaycastAll(ray))
        {
            if (hit.transform.parent == waypoints)
            {
                float distance = (hit.point - hit.transform.position).magnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    name = hit.transform.gameObject.name;
                }
            }
        }
        return name;
    }

    public void CreateSegment(string sourceWaypoint, string destWaypoint, float width, Color color, int sort)
    {
        DeleteSegment(sourceWaypoint, destWaypoint);  // delete existing

        string name = sourceWaypoint + "\\" + destWaypoint;

        GameObject obj = new GameObject(name);
        obj.transform.parent = segments;
        obj.transform.eulerAngles = new Vector3(90.0f, 0.0f, 0.0f);
        LineRenderer lr = obj.AddComponent<LineRenderer>() as LineRenderer;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;
        lr.startWidth = lr.endWidth = width;
        lr.alignment = LineAlignment.TransformZ;
        lr.numCapVertices = 5;
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.sortingOrder = sort;

        processSegmentsThisFrame = true;
    }

    public void SetSegmentColor(string sourceWaypoint, string destWaypoint, Color color, int sort)
    {
        string name = sourceWaypoint + "\\" + destWaypoint;
        Transform segment = segments.Find(name);
        if (segment == null)
            return;
        LineRenderer lr = segment.gameObject.GetComponent<LineRenderer>();
        lr.startColor = lr.endColor = color;
        lr.sortingOrder = sort;
    }

    public void DeleteSegment(string sourceWaypoint, string destWaypoint)
    {
        string name = sourceWaypoint + "\\" + destWaypoint;
        Transform segment = segments.Find(name);
        if (segment == null)
            return;
        Destroy(segment.gameObject);
    }

    public float GetSegmentLengthSquared(string sourceWaypoint, string destWaypoint, bool ignoreExistence = false)
    {
        string name = sourceWaypoint + "\\" + destWaypoint;
        Transform segment = segments.Find(name);
        if (segment == null && ! ignoreExistence)
            return float.PositiveInfinity;

        Vector3 distance = GetWaypointPosition(destWaypoint) - GetWaypointPosition(sourceWaypoint);
        distance.x /= MetersToWorld(1.0f, centerTileLatitude);
        distance.z /= MetersToWorld(1.0f);
        return distance.x * distance.x + distance.z * distance.z;
    }

    public void GetSegmentClosestToPoint(Vector3 point, out string closestSource, out string closestDest, out float minDistance, out float lerp, string original = null)
    {
        closestSource = null;
        closestDest = null;
        minDistance = float.PositiveInfinity;
        lerp = 0.0f;
        foreach (Transform child in segments.GetComponentsInChildren<Transform>(true))
        {
            if (child != segments && ! child.gameObject.name.StartsWith("#mapsystem#"))
            {
                string source = child.gameObject.name.Split('\\')[0];
                string dest = child.gameObject.name.Split('\\')[1];

                // ignore connected original points
                if (source == original || dest == original)
                    continue;

                // project point onto segment
                Vector3 sourceToDest = GetWaypointPosition(dest) - GetWaypointPosition(source);
                Vector3 sourceToPoint = point - GetWaypointPosition(source);
                float t = (sourceToPoint.x * sourceToDest.x + sourceToPoint.z * sourceToDest.z) /
                          (sourceToDest.x * sourceToDest.x + sourceToDest.z * sourceToDest.z);
                if (t < 0.0f)
                    t = 0.0f;
                if (t > 1.0f)
                    t = 1.0f;

                Vector3 projection = GetWaypointPosition(source) + t * sourceToDest;
                Vector3 pointToProjection = projection - point;
                pointToProjection.x /= MetersToWorld(1.0f, centerTileLatitude);
                pointToProjection.z /= MetersToWorld(1.0f);
                float distance = pointToProjection.x * pointToProjection.x + pointToProjection.z * pointToProjection.z;

                if (distance < minDistance)
                {
                    closestSource = source;
                    closestDest = dest;
                    minDistance = distance;
                    lerp = t;
                }
            }
        }
    }

    public bool IsCentered()
    {
        return centered;
    }

    public bool IsFollowing()
    {
        return following;
    }

    public bool IsNorthUp()
    {
        return ! following && cameraDolly.eulerAngles.y == 0.0f;
    }

    public bool IsTopView()
    {
        return IsNorthUp() && cameraDolly.eulerAngles.x == angleForMaxElevation;
    }

    public float GetNorthAngle()
    {
        return cameraDolly.eulerAngles.y;
    }

    public void CenterAt(Vector3 position)
    {
        centered = false;
        following = false;
        cameraDolly.localPosition = position;
    }

    public void Recenter()
    {
        centered = true;
        if (following)
        {
            cameraDolly.localScale = Vector3.one * scaleForCenterZoom;
            NorthUp();
        }
    }

    public void Follow()
    {
        centered = true;
        following = true;
        cameraDolly.localScale = Vector3.one * scaleForFollowZoom;
        cameraDolly.eulerAngles = new Vector3(angleForFollowElevation,
                                              GetWaypointOrientation("#mapsystem#compass"),
                                              0.0f);
    }

    public void NorthUp()
    {
        following = false;
        cameraDolly.eulerAngles = new Vector3(angleForMaxElevation,
                                              0.0f,
                                              0.0f);
    }

    public void TopView()
    {
        following = true;
        Recenter();
    }

    public void ShowWaypoints(List<string> waypoints, bool includeGps = false)
    {
        float minX = float.PositiveInfinity;
        float minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxZ = float.NegativeInfinity;

        float divisor = 1.0f;
        if (Screen.height > Screen.width)
            divisor = (float) Screen.height / Screen.width;

        Vector3 position;

        foreach (string waypoint in waypoints)
        {
            position = GetWaypointPosition(waypoint);
            if (! position.Equals(Vector3.negativeInfinity))
            {
                if (position.x < minX)
                    minX = position.x;
                if (position.z < minZ)
                    minZ = position.z;
                if (position.x > maxX)
                    maxX = position.x;
                if (position.z > maxZ)
                    maxZ = position.z;
            }
        }

        centered = false;
        following = false;

        if (minX == float.PositiveInfinity || includeGps && validated && deviceShowing)  // fallback when there are no waypoints
        {
            position = GetDeviceGPSPosition();
            if (position.x < minX)
                minX = position.x;
            if (position.z < minZ)
                minZ = position.z;
            if (position.x > maxX)
                maxX = position.x;
            if (position.z > maxZ)
                maxZ = position.z;
        }

        float greater = maxX - minX;
        if (maxZ - minZ > greater)
            greater = maxZ - minZ;

        cameraDolly.position = new Vector3((minX + maxX) / 2.0f, 0.0f, (minZ + maxZ) / 2.0f);
        float scale = greater / 256.0f * 1.5f * divisor;
        if (scale < scaleForMaxZoom)  // constrain scaling
            scale = scaleForMaxZoom;
        if (scale > scaleForMinZoom)
            scale = scaleForMinZoom;
        cameraDolly.localScale = Vector3.one * scale;
        cameraDolly.eulerAngles = new Vector3(angleForMaxElevation, 0.0f, 0.0f);
    }

    public Vector3 LongitudeLatitudeToWorld(float longitude, float latitude)
    {
        double x = LongitudeToX(longitude) - ((int) LongitudeToX(centerTileLongitude));
        double y = LatitudeToY(latitude) - ((int) LatitudeToY(centerTileLatitude));
        return new Vector3((float) (x - 0.5) * 256.0f, 0.0f, (float) (y - 0.5) * -256.0f);
    }

    public float MetersToWorld(float meters, float latitude = 0.0f)
    {
        double radians = latitude * Math.PI / 180.0;
        double metersPerUnit = 40075016.686 / 256.0 * Math.Cos(radians) / (1 << tileZoomLevel);
        return (float) (meters / metersPerUnit);
    }

    public Vector3 ClampToMapExtremes(Vector3 position)
    {
        float minX = (additionalTilesWest + 0.5f) * -256.0f;
        float minZ = (additionalTilesSouth + 0.5f) * -256.0f;
        float maxX = (additionalTilesEast + 0.5f) * 256.0f;
        float maxZ = (additionalTilesNorth + 0.5f) * 256.0f;
        if (position.x < minX)
            position.x = minX;
        if (position.x > maxX)
            position.x = maxX;
        if (position.z < minZ)
            position.z = minZ;
        if (position.z > maxZ)
            position.z = maxZ;
        return position;
    }

    public void CameraCenterNudge(Vector2 nudge)
    {
        cameraMount.localPosition = new Vector3(nudge.x, nudge.y, -128.0f);
    }

    private IEnumerator Start()
    {
        // ensure that the map system's game object has the default transform
        this.gameObject.transform.position = Vector3.zero;
        this.gameObject.transform.rotation = Quaternion.identity;
        this.gameObject.transform.localScale = Vector3.one;

        // create camera helper objects
        cameraDolly = (new GameObject("CameraDolly")).transform;
        cameraDolly.parent = this.gameObject.transform;
        cameraDolly.eulerAngles = new Vector3(angleForMaxElevation, 0.0f, 0.0f);
        cameraDolly.localScale = Vector3.one * scaleForCenterZoom;
        cameraMount = (new GameObject("CameraMount")).transform;
        cameraMount.parent = cameraDolly;
        cameraMount.localPosition = new Vector3(0.0f, 0.0f, -128.0f);
        cameraMount.localRotation = Quaternion.identity;
        cameraMount.localScale = Vector3.one;
        Transform camera = (new GameObject("Camera")).transform;
        camera.parent = this.gameObject.transform;
        camera.position = Vector3.zero;  // need to reposition
        camera.eulerAngles = cameraMount.eulerAngles;
        camera.tag = "MainCamera";
        Camera c = camera.gameObject.AddComponent<Camera>() as Camera;
        c.backgroundColor = backgroundColor;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.fieldOfView = 90.0f;
        c.nearClipPlane = 9999.0f;
        c.farClipPlane = 10000.0f;

        // create containers for embellishments
        waypoints = (new GameObject("Waypoints")).transform;
        waypoints.parent = this.gameObject.transform;
        segments = (new GameObject("Segments")).transform;
        segments.parent = this.gameObject.transform;

        // create avatar
        CreateWaypoint("#mapsystem#compass", Resources.Load<Sprite>("Map/Compass"),
                       Vector3.zero);
        CreateWaypoint("#mapsystem#gps", Resources.Load<Sprite>("Map/Circle"),
                       Vector3.zero);
        SetWaypointOrientation("#mapsystem#compass", 0.0f);
        SetWaypointDiameter("#mapsystem#gps", 1.0f);

        // generate map sprites
        mapTiles = (new GameObject("MapTiles")).transform;
        mapTiles.parent = this.gameObject.transform;
        int centerX = ((int) LongitudeToX(centerTileLongitude));
        int centerY = ((int) LatitudeToY(centerTileLatitude));
        for (int j = centerY - additionalTilesNorth; j <= centerY + additionalTilesSouth; j++)
        {
            for (int i = centerX - additionalTilesWest; i <= centerX + additionalTilesEast; i++)
            {
                string filename =
                    (j - centerY + additionalTilesNorth) + "-" +
                    (i - centerX + additionalTilesWest);
                Texture2D texture = Resources.Load("MapTiles/" + filename) as Texture2D;
#if UNITY_EDITOR
                if (texture == null)
                {
                    using (WWW www = new WWW(tileServerURL + tileZoomLevel + "/" + i + "/" + j + ".png"))
                    {
                        yield return www;
                        texture = www.texture;
                        if (! AssetDatabase.IsValidFolder("Assets/ARGames/" + DeviceInput.GameName()))
                            AssetDatabase.CreateFolder("Assets/ARGames/", DeviceInput.GameName());
                        if (! AssetDatabase.IsValidFolder("Assets/ARGames/" + DeviceInput.GameName() + "/Resources"))
                            AssetDatabase.CreateFolder("Assets/ARGames/" + DeviceInput.GameName(), "Resources");
                        if (! AssetDatabase.IsValidFolder("Assets/ARGames/" + DeviceInput.GameName() + "/Resources/MapTiles"))
                            AssetDatabase.CreateFolder("Assets/ARGames/" + DeviceInput.GameName() + "/Resources", "MapTiles");
                        File.WriteAllBytes("Assets/ARGames/" + DeviceInput.GameName() + "/Resources/MapTiles/" + filename + ".png",
                                           ImageConversion.EncodeToPNG(texture));
                        AssetDatabase.Refresh();
                    }
                }
                if (texture.wrapMode != TextureWrapMode.Clamp)
                    Debug.Log("WARNING: Map tiles should all be manually set to Clamp wrap mode");
#endif
                GameObject tile = new GameObject(filename);
                tile.transform.parent = mapTiles;
                tile.transform.position = new Vector3((i - centerX) * 256.0f, -1.0f, (j - centerY) * -256.0f);
                tile.transform.eulerAngles = new Vector3(90.0f, 0.0f, 0.0f);
                SpriteRenderer sr = tile.AddComponent<SpriteRenderer>() as SpriteRenderer;
                sr.color = Color.white;
                sr.sortingOrder = -1;  // show below everything
                sr.sprite = Sprite.Create(texture,
                                          new Rect(0.0f, 0.0f, texture.width, texture.height),
                                          new Vector2(0.5f, 0.5f),
                                          tilePixelsPerUnit);
            }
        }
        yield return new WaitUntil(() => true);
    }

    private void _DeleteAllChildren(Transform transform)
    {
        foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && ! child.gameObject.name.StartsWith("#mapsystem#"))
            {
                child.gameObject.name = "#mapsystem#deleted\\\\";
                Destroy(child.gameObject);
            }
        }
    }

    private double LongitudeToX(float longitude)
    {
        double radians = longitude * Math.PI / 180.0;
        return (Math.PI + radians) * (1 << (tileZoomLevel - 1)) / Math.PI;
    }

    private double LatitudeToY(float latitude)
    {
        double radians = latitude * Math.PI / 180.0;
        return (Math.PI - Math.Log(Math.Tan(Math.PI / 4.0 + radians / 2.0)))
               * (1 << (tileZoomLevel - 1)) / Math.PI;
    }

    private Vector3 ScreenPointToWorld(Vector2 point)
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(point.x, point.y, 0.0f));
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        float enter;
        if (plane.Raycast(ray, out enter))
            return ray.GetPoint(enter);
        else
            return Vector3.negativeInfinity;
    }

    private void Pan(Vector2 origin, Vector2 destination)
    {
        delayedLerp = 1;  // do not interpolate user actions

        Vector3 mapOrigin = ScreenPointToWorld(origin);
        Vector3 mapDestination = ScreenPointToWorld(destination);
        cameraDolly.Translate(mapOrigin - mapDestination, Space.World);
    }

    private void Elevate(float difference)
    {
        delayedLerp = 1;  // do not interpolate user actions

        Vector3 rotation = cameraDolly.eulerAngles;
        rotation.x += difference;
        if (rotation.x > angleForMaxElevation)
            rotation.x = angleForMaxElevation;
        else if (rotation.x < angleForMinElevation)
            rotation.x = angleForMinElevation;
        cameraDolly.eulerAngles = rotation;
    }

    private void RotateAndZoom(Vector2 origin, Vector2 previous, Vector2 current, bool enableRotate)
    {
        delayedLerp = 1;  // do not interpolate user actions

        // do not do anything if the two pinch points are equal (causing division by zero errors)
        if (origin.Equals(previous) || origin.Equals(current))
            return;

        Vector3 mapOrigin = ScreenPointToWorld(origin);
        Vector3 mapPrevious = ScreenPointToWorld(previous) - mapOrigin;
        Vector3 mapCurrent = ScreenPointToWorld(current) - mapOrigin;

        // do the rotation
        if (enableRotate)
        {
            float rotationAngle = Vector3.SignedAngle(mapCurrent, mapPrevious, Vector3.up);
            cameraDolly.RotateAround(mapOrigin, Vector3.up, rotationAngle);
        }

        // do the scaling
        Vector3 cameraRelativeToOrigin = cameraDolly.position - mapOrigin;
        float scale = mapPrevious.magnitude / mapCurrent.magnitude;
        if (cameraDolly.localScale.x * scale < scaleForMaxZoom)  // constrain scaling
            scale = scaleForMaxZoom / cameraDolly.localScale.x;
        if (cameraDolly.localScale.x * scale > scaleForMinZoom)
            scale = scaleForMinZoom / cameraDolly.localScale.x;
        cameraDolly.localScale *= scale;
        cameraDolly.position = cameraRelativeToOrigin * scale + mapOrigin;
    }

    private bool _TouchEnding(int touch)
    {
        return Input.GetTouch(touch).phase == TouchPhase.Ended
               || Input.GetTouch(touch).phase == TouchPhase.Canceled;
    }

    private int NumTouches()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (! UnityEditor.EditorApplication.isRemoteConnected)
        {
            if (Input.GetMouseButton(1) && ! Input.GetMouseButtonUp(1))
            {
                if (ignoreTouch)
                    return 0;
                return 2;
            }
            else if (Input.GetMouseButton(0) && ! Input.GetMouseButtonUp(0))
            {
                if (ignoreTouch)
                    return 0;
                return 1;
            }
            else
            {
                ignoreTouch = false;  // any touch that was meant for some other component has ended
                return 0;
            }
        }
#endif
        if (Input.touchCount == 2)
        {
            if (ignoreTouch)
                return 0;
            if (_TouchEnding(0) && _TouchEnding(1))
                return 0;
            if (_TouchEnding(0) || _TouchEnding(1))
                return 1;
            return 2;
        }
        else if (Input.touchCount == 1)
        {
            if (ignoreTouch)
                return 0;
            if (_TouchEnding(0))
                return 0;
            return 1;
        }
        else if (Input.touchCount == 0)
        {
            ignoreTouch = false;  // any touch that was meant for some other component has ended
            return 0;
        }
        else
        {
            ignoreTouch = true;  // if more than two touches, ignore input until all touches are released
            return 0;
        }
    }

    private bool IsTouchStarting()
    {
        if (NumTouches() == 2)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (! UnityEditor.EditorApplication.isRemoteConnected)
            {
                return Input.GetMouseButtonDown(1);
            }
#endif
            // there is apparently no guarantee in the order of touches
            return Input.GetTouch(0).phase == TouchPhase.Began
                   || Input.GetTouch(1).phase == TouchPhase.Began;
        }
        else if (NumTouches() == 1)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (! UnityEditor.EditorApplication.isRemoteConnected)
            {
                return Input.GetMouseButtonUp(1) || Input.GetMouseButtonDown(0);
            }
#endif
            // if NumTouches returns 1 when there are actually 2, one has ended (must reinitialize anyway)
            return Input.touchCount == 2 || Input.GetTouch(0).phase == TouchPhase.Began;
        }
        else
            return false;
    }

    private Vector2 GetTouch(int index)
    {
        Vector2 result = Vector2.negativeInfinity;
#if UNITY_EDITOR || UNITY_STANDALONE
        if (! UnityEditor.EditorApplication.isRemoteConnected)
        {
            if (index == 0 && NumTouches() == 2)
            {
                // for mouse emulation, the first touch never moves unless left button is down
                // (in which case, it takes on the value of the second touch)
                if (Input.GetMouseButton(0) && ! IsTouchStarting())
                {
                    previousTouch0 = previousTouch1;
                    result = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                }
                else
                {
                    result = previousTouch0;
                    if (result.Equals(Vector2.negativeInfinity))
                        result = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                }
            }
            else
                result = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        }
        else
#endif
        {
            if (index >= NumTouches())
                return result;  // cannot access a touch that doesn't exist
            else if (NumTouches() == 1 && Input.touchCount == 2)
            {
                // return the touch that has not ended
                if (_TouchEnding(0))
                    result = Input.GetTouch(1).position;
                else
                    result = Input.GetTouch(0).position;
            }
            else
                result = Input.GetTouch(index).position;
        }

        if (result.x < 0)
            result.x = 0;
        if (result.x > Screen.width - 1)
            result.x = Screen.width - 1;
        if (result.y < 0)
            result.y = 0;
        if (result.y > Screen.height - 1)
            result.y = Screen.height - 1;
        return result;
    }

    private void Update()
    {
        // automatically ignore touch if the event system is getting focus
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            ignoreTouch = true;

        float largerDimension = (Screen.width > Screen.height ? Screen.width : Screen.height);
        float divisionSize = largerDimension / numScreenLengthDivisions;

        if (NumTouches() == 2)
        {
            draggingWaypoint = null;

            // if two-finger touch (or right-click) just started this frame, reinitialize
            if (IsTouchStarting())
            {
                previousTouch0 = GetTouch(0);
                previousTouch1 = GetTouch(1);
                touchDelta0 = touchDelta1 = Vector2.zero;
                touchDeltaDot = float.NegativeInfinity;
            }
            currentTouch0 = GetTouch(0);
            currentTouch1 = GetTouch(1);

            // if we haven't accumulated enough delta to determine the pinch mode, accumulate it first
            if (touchDeltaDot == float.NegativeInfinity)
            {
                touchDelta0 += currentTouch0 - previousTouch0;
                touchDelta1 += currentTouch1 - previousTouch1;
                previousTouch0 = currentTouch0;
                previousTouch1 = currentTouch1;
                if (touchDelta0.sqrMagnitude >= divisionSize * divisionSize
                    || touchDelta1.sqrMagnitude >= divisionSize * divisionSize)
                {
                    // once enough delta is accumulated, factor it back to the previous touches
                    previousTouch0 -= touchDelta0;
                    previousTouch1 -= touchDelta1;

                    // get angle between the two deltas
                    touchDeltaDot = Vector2.Dot(touchDelta0.normalized, touchDelta1.normalized);

                    // get angle between the two touches (except if the distance between their initial
                    // positions are too short; in which case, do not enable rotation)
                    touchDot = 1.0f;
                    if ((previousTouch0 - previousTouch1).magnitude
                        >= divisionSize * minDivisionsForRotation)
                    {
                        touchDot = Vector2.Dot((previousTouch0 - previousTouch1).normalized,
                                               (currentTouch0 - currentTouch1).normalized);
                    }
                    /*
                    Debug.Log(touchDelta0 + " " +
                              touchDelta1 + " " +
                              (previousTouch0 - previousTouch1) + " " +
                              (currentTouch0 - currentTouch1) + "\n" +
                              touchDeltaDot + " " +
                              touchDot);
                    */
                }
            }

            if (! currentTouch0.Equals(previousTouch0) || ! currentTouch1.Equals(previousTouch1))
            {
                // if the two touch deltas are parallel enough, use elevate pinch mode;
                // otherwise, use rotate and zoom pinch mode
                if (touchDeltaDot >= elevationAngleThreshold)
                    Elevate((previousTouch1.y - currentTouch1.y) / Screen.height * 160.0f);
                else
                {
                    // only enable rotation if the two touch vectors are perpendicular enough
                    bool enableRotate = (touchDot >= -rotationAngleThreshold
                                         && touchDot <= rotationAngleThreshold);
                    RotateAndZoom(previousTouch1, previousTouch0, currentTouch0, enableRotate);
                    RotateAndZoom(currentTouch0, previousTouch1, currentTouch1, enableRotate);
                }
                previousTouch0 = currentTouch0;
                previousTouch1 = currentTouch1;
            }
        }
        else if (NumTouches() == 1)
        {
            // if one-finger touch (or left-click) just started this frame, reinitialize
            currentTouch0 = GetTouch(0);
            if (IsTouchStarting())
            {
                touchDelta0 = Vector2.zero;
                previousTouch0 = GetTouch(0);
                if (waypointDragging)
                    draggingWaypoint = GetTouchedWaypoint();
            }

            // do not allow touch to easily get out of centered/following mode
            // (following mode is even harder to get out of)
            if ((centered || following) && draggingWaypoint == null)
            {
                touchDelta0 += currentTouch0 - previousTouch0;
                previousTouch0 = currentTouch0;
                if (touchDelta0.sqrMagnitude >= (divisionSize * divisionSize) *
                                                (following ? divisionSize : 1))
                {
                    // once enough delta is accumulated, factor it back to the previous touches
                    previousTouch0 -= touchDelta0;
                }
            }

            if (! currentTouch0.Equals(previousTouch0))
            {
                if (draggingWaypoint != null)
                {
                    Vector3 diff = ScreenPointToWorld(currentTouch0) - ScreenPointToWorld(previousTouch0);
                    SetWaypointPosition(draggingWaypoint, GetWaypointPosition(draggingWaypoint) + diff);
                }
                else
                {
                    Pan(previousTouch0, currentTouch0);
                    centered = false;
                    following = false;
                }
                previousTouch0 = currentTouch0;
            }
        }
        else
        {
            draggingWaypoint = null;
            currentTouch0 = GetTouch(0);
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        if (! UnityEditor.EditorApplication.isRemoteConnected)
        {
            // draw special segment for pinch
            DeleteSegment("#mapsystem#1", "#mapsystem#2");
            DeleteWaypoint("#mapsystem#1");
            DeleteWaypoint("#mapsystem#2");
            if (NumTouches() == 2)
            {
                CreateWaypoint("#mapsystem#1", null, ScreenPointToWorld(currentTouch0));
                CreateWaypoint("#mapsystem#2", null, ScreenPointToWorld(currentTouch1));
                CreateSegment("#mapsystem#1", "#mapsystem#2", 2.0f, Color.red, 99);
            }
        }
#endif

        // update the device position
        if (Input.location.status == LocationServiceStatus.Running)
        {
            // do not move the following code outside of the if statement checking for location!
            // getting the true heading when location services are not available causes device malfunction
            float compassLerp = Time.deltaTime;  // reaction time of 1 second
            Quaternion newCompass = Quaternion.Slerp(
                                        Quaternion.Euler(0.0f, filteredCompass, 0.0f),
                                        Quaternion.Euler(0.0f, Input.compass.trueHeading, 0.0f),
                                        compassLerp
                                    );
            Vector3 axis;
            newCompass.ToAngleAxis(out filteredCompass, out axis);
            if (axis.y < 0.0f)  // handle singularity
                filteredCompass = -filteredCompass;
            SetWaypointOrientation("#mapsystem#compass", filteredCompass);

            if (Input.location.lastData.timestamp > lastLocationTimestamp)
            {
                lastLocationTimestamp = Input.location.lastData.timestamp;
                lastRealTime = Time.realtimeSinceStartup;
                Vector3 userPosition = LongitudeLatitudeToWorld(Input.location.lastData.longitude,
                                                                Input.location.lastData.latitude);
                withinMap = (userPosition == ClampToMapExtremes(userPosition));
                if (withinMap)
                {
                    validated = true;
                    SetWaypointDiameter("#mapsystem#gps",
                                        MetersToWorld(Input.location.lastData.horizontalAccuracy,
                                                      centerTileLatitude) * 2.0f);
                    SetWaypointPosition("#mapsystem#gps", userPosition);
                }
            }
        }
        else
        {
            // if not yet done, initialize device location
            if (Input.location.status == LocationServiceStatus.Stopped
                || Input.location.status == LocationServiceStatus.Failed)
            {
                Input.location.Start(0.1f, 0.1f);
                lastLocationTimestamp = 0.0;
                lastRealTime = Time.realtimeSinceStartup;
                Input.compass.enabled = true;
            }
            withinMap = false;
        }
        if (withinMap && Time.realtimeSinceStartup - lastRealTime < gpsTimeout)
        {
            SetWaypointColor("#mapsystem#compass", compassColor);
            SetWaypointColor("#mapsystem#gps", gpsColor);
        }
        else
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                validated = true;
                SetWaypointOrientation("#mapsystem#compass",
                    GetWaypointOrientation("#mapsystem#compass") - Time.deltaTime * 90.0f);
            }
            if (Input.GetKey(KeyCode.RightArrow))
            {
                validated = true;
                SetWaypointOrientation("#mapsystem#compass",
                    GetWaypointOrientation("#mapsystem#compass") + Time.deltaTime * 90.0f);
            }
            if (Input.GetKey(KeyCode.UpArrow))
            {
                validated = true;
                SetWaypointPosition("#mapsystem#gps",
                    GetWaypointPosition("#mapsystem#gps")
                    + Quaternion.Euler(0.0f,
                                       GetWaypointOrientation("#mapsystem#compass"),
                                       0.0f)
                      * new Vector3(0.0f,
                                    0.0f,
                                    Time.deltaTime
                                    * ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                                       ? 256.0f : 64.0f))
                );
            }
            if (Input.GetKey(KeyCode.DownArrow))
            {
                validated = true;
                SetWaypointPosition("#mapsystem#gps",
                    GetWaypointPosition("#mapsystem#gps")
                    + Quaternion.Euler(0.0f,
                                       GetWaypointOrientation("#mapsystem#compass"),
                                       0.0f)
                      * new Vector3(0.0f,
                                    0.0f,
                                    -Time.deltaTime
                                    * ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                                       ? 256.0f : 64.0f))
                );
            }
            withinMap = validated && (! (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)));
            SetWaypointColor("#mapsystem#compass", withinMap ? compassColor : compassBadColor);
            SetWaypointColor("#mapsystem#gps", withinMap ? gpsColor : gpsBadColor);
            SetWaypointDiameter("#mapsystem#gps", 50.0f);
#else
            SetWaypointColor("#mapsystem#compass", compassBadColor);
            SetWaypointColor("#mapsystem#gps", gpsBadColor);
#endif
        }
        SetWaypointPosition("#mapsystem#compass", GetWaypointPosition("#mapsystem#gps"));

        if (! deviceShowing || ! validated)
        {
            SetWaypointColor("#mapsystem#compass", new Color(0.0f, 0.0f, 0.0f, 0.0f));
            SetWaypointColor("#mapsystem#gps", new Color(0.0f, 0.0f, 0.0f, 0.0f));
        }

        // center and follow modes
        if (following)
        {
            cameraDolly.position =
                GetWaypointPosition("#mapsystem#gps")
                + Quaternion.Euler(0.0f,
                                   GetWaypointOrientation("#mapsystem#compass"),
                                   0.0f) * new Vector3(0.0f, 0.0f,
                                                       cameraDolly.localScale.x * followDistanceFromCenter);
            cameraDolly.eulerAngles = new Vector3(cameraDolly.eulerAngles.x,
                                                  GetWaypointOrientation("#mapsystem#compass"),
                                                  0.0f);
        }
        else if (centered)
        {
            cameraDolly.position = GetWaypointPosition("#mapsystem#gps");
        }

        // clamp camera position to map extremes
        cameraDolly.position = ClampToMapExtremes(cameraDolly.position);

        // interpolate the real camera
        float lerp = (delayedLerp == 0) ? 5.0f * Time.deltaTime : 1.0f;  // reaction time of 1/5th of a second
        if (delayedLerp > 0)
        {
            delayedLerp--;
            if (delayedLerp == 0)
                Camera.main.nearClipPlane = 0.3f;  // start displaying
        }
        Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position,
                                                      cameraMount.position,
                                                      lerp);
        Camera.main.transform.rotation = Quaternion.Slerp(Camera.main.transform.rotation,
                                                          cameraMount.rotation,
                                                          lerp);

        // scale and rotate all waypoints
        foreach (Transform child in waypoints.GetComponentsInChildren<Transform>())
        {
            if (child != waypoints)
            {
                if (child.gameObject.GetComponent<SpriteRenderer>().sprite == null)
                    continue;
                if (! waypointOrientations.ContainsKey(child.gameObject.name)
                    && ! waypointDiameters.ContainsKey(child.gameObject.name))
                {
                    child.gameObject.GetComponent<SpriteRenderer>().sortingOrder = 32767;  // standing
                    child.rotation = Camera.main.transform.rotation;  // always face the camera
                }
                else
                {
                    if (waypointDiameters.ContainsKey(child.gameObject.name))
                        child.gameObject.GetComponent<SpriteRenderer>().sortingOrder = 32765;  // scaled flat
                    else
                        child.gameObject.GetComponent<SpriteRenderer>().sortingOrder = 32766;  // flat
                    if (waypointOrientations.ContainsKey(child.gameObject.name))
                    {
                        child.eulerAngles = new Vector3(90.0f,
                                                        waypointOrientations[child.gameObject.name],
                                                        0.0f);
                    }
                    else
                        child.eulerAngles = new Vector3(90.0f, 0.0f, 0.0f);
                }
                if (! waypointDiameters.ContainsKey(child.gameObject.name))
                {
                    float scale =
                        Mathf.Pow(Camera.main.WorldToScreenPoint(child.transform.position).z,
                                  distanceScalePower)
                        / Mathf.Pow(cameraDolly.localScale.z, zoomScalePower)
                        * overallScaleFactor;
                    if (scale > 0.0f)
                        child.localScale = Vector3.one * scale;
                }
                else
                    child.localScale = Vector3.one * waypointDiameters[child.gameObject.name];
            }
        }

        // position all segments
        if (processSegmentsThisFrame)
        {
            processSegmentsThisFrame = false;
            List<KeyValuePair<string, string>> toDelete = new List<KeyValuePair<string, string>>();
            foreach (Transform child in segments.GetComponentsInChildren<Transform>())
            {
                if (child != segments)
                {
                    string source = child.gameObject.name.Split('\\')[0];
                    string dest = child.gameObject.name.Split('\\')[1];
                    Vector3 sourcePos = GetWaypointPosition(source);
                    Vector3 destPos = GetWaypointPosition(dest);
                    if (sourcePos.Equals(Vector3.negativeInfinity) || destPos.Equals(Vector3.negativeInfinity))
                    {
                        toDelete.Add(new KeyValuePair<string, string>(source, dest));
                    }
                    else
                    {
                        LineRenderer lr = child.gameObject.GetComponent<LineRenderer>();
                        lr.SetPosition(0, new Vector3(sourcePos.x, -0.5f, sourcePos.z));
                        lr.SetPosition(1, new Vector3(destPos.x, -0.5f, destPos.z));
                    }
                }
            }
            foreach (KeyValuePair<string, string> pair in toDelete)
            {
                // delete newly-orphaned segments
                DeleteSegment(pair.Key, pair.Value);
            }
        }
    }

    private void OnGUI()
    {
        // automatically ignore touch if some other IMGUI control is active
        if (GUIUtility.hotControl != 0)
            ignoreTouch = true;

        Vector2 guiScale = GetGUIScale();
        GUIContent content = new GUIContent(attributionMessage);
        GUIStyle style = new GUIStyle();
        style.fontSize = 11;
        style.normal.background = Texture2D.whiteTexture;
        style.alignment = TextAnchor.LowerRight;
        Vector2 size = style.CalcSize(content);
        GUI.backgroundColor = new Color(1.0f, 1.0f, 1.0f, 0.67f);

        // Unity currently underestimates the safe area of the iPhone X screen
        // so we just hardcode the offset here to be roughly equal that of other UI elements
        if (GUI.Button(new Rect(55.0f,
                                guiScale.y - size.y,
                                size.x,
                                size.y),
                       attributionMessage,
                       style))
        {
            Application.OpenURL(attributionURL);
        }
        GUI.backgroundColor = Color.white;
    }
}
