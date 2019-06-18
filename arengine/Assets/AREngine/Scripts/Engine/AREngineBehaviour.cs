/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using System.Collections.Generic;

public class AREngineBehaviour : MonoBehaviour, IAREngine
{
    // do not grab the status of just-initialized devices for the specified number of frames
    // (note: FixedUpdate is set at 30fps)
    public const int INITIALIZATION_DELAY = 15;

    // horizontal field of view to maintain
    public const float FOV = 45.0f;

    // maximum pitch allowed by the game in the editor
    public const float MAX_PITCH = 30.0f;

    // global state
    private static bool directionGuides;

    // camera state
    private GameObject arCameraTarget;
    private bool arCameraTargetFound;
    private Vector3 arCameraAngles = new Vector3();
    private Quaternion currentRotation = Quaternion.identity;
    private float baseYaw;
    private float biasYaw;
    private float biasPitch;
    private int trackingStartedCounter = -1;

    private void OnApplicationPause(bool paused)
    {
        // when the app is paused, we go back to marker registration
        // (to prevent the user from continuing the scene at a different physical location)
        if (paused)
            StopTracking();
    }

    private void FixedUpdate()
    {
        // delay initialization when a device has just been activated
        if (trackingStartedCounter > 0)
        {
            trackingStartedCounter--;
            if (trackingStartedCounter == 0)
            {
                // disable the main camera (if it was already enabled) to signal that we need to reinitialize the base yaw
                GameObject.Find("ARCanvas").GetComponent<UnityEngine.UI.RawImage>().color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
            }
        }

        if (arCameraTarget != null)
        {
            arCameraAngles = Vector3.zero;

            // find the ARCamera's relative position with the AR-system-dependent GameObject inside the ARMarker object,
            // determine the angle it makes on the XZ plane, then subtract it from the ARCamera's rotation; this will make
            // the marker's position the "forward direction" regardless of the viewing angle of the marker
            Vector3 arCameraPosition = (-arCameraTarget.transform.GetChild(0).position).normalized;
            arCameraAngles.y = -Mathf.Atan2(-arCameraPosition.x, -arCameraPosition.z) / Mathf.PI * 180;
            arCameraAngles.x = Mathf.Atan2(-arCameraPosition.y, -arCameraPosition.z) / Mathf.PI * 180;

            // add the saved pitch from the last tracking-lost event, if any
            arCameraAngles.x += arCameraTarget.transform.localRotation.eulerAngles.x;

            // find the device's rotation according to the device sensors
            Vector3 deviceAngles = DeviceInput.attitude.eulerAngles;

            if (! DeviceInput.isAttitudeYawStable && DeviceInput.accelerometerMalfunctioning)
            {
                // if gyro/compass is unavailable and accelerometer is unstable,
                // use AR camera's angles instead of the device's
                deviceAngles = arCameraTargetFound ? arCameraAngles : currentRotation.eulerAngles;
            }

            // when initializing for the first time, or whenever the ar camera is valid, use the current device rotation as the base yaw
            if (arCameraTargetFound)
                baseYaw = deviceAngles.y - arCameraAngles.y;
            else if (GameObject.Find("ARCanvas").GetComponent<UnityEngine.UI.RawImage>().color.a == 0.0f)
                baseYaw = deviceAngles.y - currentRotation.eulerAngles.y;

            GameObject temporaryMarker = GameObject.Find("AREngine/ARTemporaryMarker");
            if (! DeviceInput.isAttitudeYawStable && (! arCameraTargetFound
                                                      || arCameraTarget.name != "ARTemporaryMarker"
                                                         && ! arCameraTarget.GetComponent<ARMarkerBehaviour>().visible))
            {
                // create a temporary marker whenever the marker is not found
                if (temporaryMarker.GetComponent<ARTemporaryMarkerBehaviour>().MakeNewMarker())
                {
                    // save the pitch and roll when tracking is lost; we'll continue from that
                    temporaryMarker.transform.localRotation = Quaternion.Euler(new Vector3(deviceAngles.x, 0.0f, deviceAngles.z));
                    Debug.Log("Adjusting new marker... " + temporaryMarker.transform.localRotation.eulerAngles);
                }
            }
            else if (arCameraTarget.name != "ARTemporaryMarker")
            {
                // if we're currently tracking the real target, reset the temporary marker transform
                // (since vuforia's world centering is determined by that marker)
                temporaryMarker.transform.localRotation = Quaternion.identity;
            }

            if (trackingStartedCounter == 0)
            {
                // determine the final viewing angle;
                // if ar camera is valid, yaw is determined from the marker's position; otherwise use the device angles
                Vector3 finalAngles;
                finalAngles.x = deviceAngles.x;
                finalAngles.z = deviceAngles.z;
                if (arCameraTargetFound)
                    finalAngles.y = arCameraAngles.y;
                else
                    finalAngles.y = deviceAngles.y - baseYaw;

                // set the final viewing angle
                currentRotation = Quaternion.Euler(finalAngles);
                Vector3 biasedRotation = currentRotation.eulerAngles;
                biasedRotation.y += biasYaw;
                biasedRotation.x += biasPitch;
                GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().fieldOfView = Mathf.Atan(Mathf.Tan(FOV / 360.0f * Mathf.PI) * Screen.height / Screen.width) / Mathf.PI * 360.0f;
                GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.localRotation = Quaternion.Euler(biasedRotation);

                // change the render texture alpha according to the current marker state
                if (markerState == IARMarkerState.LOST)
                    GameObject.Find("ARCanvas").GetComponent<UnityEngine.UI.RawImage>().color = new Color(1.0f, 1.0f, 1.0f, 0.33f);
                else
                    GameObject.Find("ARCanvas").GetComponent<UnityEngine.UI.RawImage>().color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
            }

            // show/hide direction guides
            foreach (GameObject obj in GameObject.FindGameObjectsWithTag("ARDebug"))
                obj.GetComponent<Renderer>().enabled = directionGuides;
        }
    }

    private void OnGUI()
    {
        if (LogCanvasBehaviour.showing)
        {
            GUILayout.TextField("AR: " + arCameraAngles + " " + GameObject.FindWithTag("MainCamera").transform.position);
            GUILayout.TextField("Device: " + DeviceInput.attitude.eulerAngles);
            GUILayout.TextField("Current: " + currentRotation.eulerAngles);
            GUILayout.TextField("Final: " + GameObject.FindWithTag("SceneCamera").GetComponent<Camera>().transform.localRotation.eulerAngles);
            if (arCameraTarget != null)
            {
                GUILayout.TextField(arCameraTarget.name + ":"
                                    + (arCameraTarget.GetComponent<ARMarkerBehaviour>().visible ? " visible" : "")
                                    + (arCameraTargetFound ? " found" : "")
                                    + " " + arCameraTarget.transform.localRotation.eulerAngles
                                    + " " + arCameraTarget.transform.position);
            }
            GUILayout.TextField(markerState
                                + " " + trackingStartedCounter
                                + " " + baseYaw
                                + " " + biasYaw
                                + " " + biasPitch
                                + " " + Input.acceleration.magnitude
                                + " " + (DeviceInput.accelerometerMalfunctioning ? "malfunction" : ""));

            // draw debug panel
            if (GUILayout.Button("Gyroscope: " + (DeviceInput.gyro ? "on" : "off"), GUILayout.Height(50)))
                DeviceInput.gyro = ! DeviceInput.gyro;
            if (GUILayout.Button("Compass: " + (DeviceInput.compass ? "on" : "off"), GUILayout.Height(50)))
                DeviceInput.compass = ! DeviceInput.compass;
            if (GUILayout.Button("Direction guides: " + (directionGuides ? "on" : "off"), GUILayout.Height(50)))
                directionGuides = ! directionGuides;
            GUILayout.BeginArea(new Rect(Screen.width - 450, 0, 300, Screen.height));
            if (trackingStartedCounter == -1)
            {
                if (currentlySeenARMarker == null)
                    GUILayout.Button("No marker", GUILayout.Height(50));
                else if (GUILayout.Button("Start " + currentlySeenARMarker.name, GUILayout.Height(50)))
                    StartTracking(null);
            }
            else
            {
                if (GUILayout.Button("Reset scene", GUILayout.Height(50)))
                    StopTracking();
            }
            foreach (GameObject obj in GameObject.FindGameObjectsWithTag("ARMarker"))
            {
                if (obj.name != "ARTemporaryMarker" && GUILayout.Button("Fake " + obj.name, GUILayout.Height(50)))
                    StartTracking(obj);
            }
            GUILayout.EndArea();
        }

        // draw movement panel
        if (trackingStartedCounter != -1)
        {
            GUILayout.BeginArea(new Rect(Screen.width - 100, 0, 50, 50));
            if (LogCanvasBehaviour.showing && GUILayout.RepeatButton("Up", GUILayout.Height(50)) || Input.GetKey(KeyCode.UpArrow))
                biasPitch -= Time.deltaTime / Time.fixedDeltaTime;
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(Screen.width - 150, 50, 50, 50));
            if (LogCanvasBehaviour.showing && GUILayout.RepeatButton("Left", GUILayout.Height(50)) || Input.GetKey(KeyCode.LeftArrow))
                biasYaw -= Time.deltaTime / Time.fixedDeltaTime;
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(Screen.width - 100, 50, 50, 50));
            if (LogCanvasBehaviour.showing && GUILayout.RepeatButton("Zero", GUILayout.Height(50)) || Input.GetKey(KeyCode.Insert))
            {
                biasYaw = 0;
                biasPitch = 0;
            }
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(Screen.width - 50, 50, 50, 50));
            if (LogCanvasBehaviour.showing && GUILayout.RepeatButton("Right", GUILayout.Height(50)) || Input.GetKey(KeyCode.RightArrow))
                biasYaw += Time.deltaTime / Time.fixedDeltaTime;
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(Screen.width - 100, 100, 50, 50));
            if (LogCanvasBehaviour.showing && GUILayout.RepeatButton("Down", GUILayout.Height(50)) || Input.GetKey(KeyCode.DownArrow))
                biasPitch += Time.deltaTime / Time.fixedDeltaTime;
            GUILayout.EndArea();

            // adjust biasPitch so that it never goes beyond the device's pitch bounds
            float currentPitch = currentRotation.eulerAngles.x;
            if (currentPitch > 180)
                currentPitch -= 360;
            if (biasPitch > 0 && currentPitch + biasPitch > MAX_PITCH)
                biasPitch = MAX_PITCH - currentPitch;
            if (biasPitch < 0 && currentPitch + biasPitch < -MAX_PITCH)
                biasPitch = -MAX_PITCH - currentPitch;
        }
    }

    public void SetTarget(GameObject target, bool found)
    {
        if (markerState != IARMarkerState.SELECTING)
        {
            if (found)
            {
                if (target.name != "ARTemporaryMarker")
                {
                    // reset the debugging bias (to enable a quick way to reset without tapping a button -- shake and focus on a marker!)
                    biasYaw = 0;
                    biasPitch = 0;

                    // if we are re-focusing on a real marker, re-enable its tracking
                    if (target != arCameraTarget)
                        target.GetComponentInChildren<ARMarkerBehaviour>().StartTracking();
                }
                else if (arCameraTarget.name != "ARTemporaryMarker" || ! arCameraTargetFound) // if temporary marker was previously not found
                {                               // (SetTarget may be called again with found == true even if the same temp marker was found)
                    // if we are focusing on a (new) temporary marker, add the current yaw to the bias
                    // so it appears to the user as if the continuation of the tracking is seamless
                    biasYaw += currentRotation.eulerAngles.y;
                    currentRotation = Quaternion.identity;
                }
            }
            else
            {
                // do not keep the "not found" status of the new target if the current target is found
                if (arCameraTargetFound && arCameraTarget != target)
                    return;
            }
        }
        else if (! found && (arCameraTarget == target || arCameraTarget == null))
            target = null;  // reset to null if object is lost when we have not started tracking yet

        arCameraTarget = target;
        arCameraTargetFound = found;
    }

    public void ResetTracking()
    {
        if (trackingStartedCounter != -1)
        {
            arCameraTargetFound = false;
            ARMarkerBehaviour.ResetTracking();
            trackingStartedCounter = INITIALIZATION_DELAY;
        }
    }

    public IARMarkerState markerState
    {
        get
        {
            if (trackingStartedCounter == -1)
                return IARMarkerState.SELECTING;
            else if (trackingStartedCounter > 0)
                return IARMarkerState.STARTING;
            else if (! arCameraTargetFound && ! DeviceInput.isAttitudeYawStable)
                return IARMarkerState.LOST;
            else if (arCameraTarget.name == "ARTemporaryMarker")
                return IARMarkerState.TEMPORARY;
            else
                return IARMarkerState.TRACKING;
        }
    }

    public GameObject currentlySeenARMarker
    {
        get
        {
            if (arCameraTarget != null && arCameraTarget.name == "ARTemporaryMarker")
                return arCameraTarget.GetComponent<ARTemporaryMarkerBehaviour>().originalMarker;
            else
                return arCameraTarget;
        }
    }

    public bool isARMarkerActuallyVisible
    {
        get
        {
            return arCameraTarget != null
                   && arCameraTarget.name != "ARTemporaryMarker"
                   && arCameraTarget.GetComponent<ARMarkerBehaviour>().visible;
        }
    }

    public GameObject StartTracking(GameObject whichARMarker)
    {
        if (whichARMarker == null)
        {
            if (arCameraTarget == null)
                return null;
            if (trackingStartedCounter == -1)
                trackingStartedCounter = 0;
#if UNITY_EDITOR
            arCameraTargetFound = false;   // allows Unity Remote gyroscope to function
#endif
        }
        else
        {
            arCameraTarget = whichARMarker;
            arCameraTargetFound = false;
            trackingStartedCounter = INITIALIZATION_DELAY;
        }

        // move the AREngine (and the main camera) to the marker's default position
        GameObject.FindWithTag("AREngine").transform.position = arCameraTarget.transform.position;
        Vector3 euler = GameObject.FindWithTag("AREngine").transform.rotation.eulerAngles;
        euler.y = arCameraTarget.transform.rotation.eulerAngles.y;  // only get the yaw, since the pitch may have been modified
        GameObject.FindWithTag("AREngine").transform.eulerAngles = euler;

        // temporarily set the ARCamera to the marker's default position (this code is only needed in the Unity editor)
        GameObject.FindWithTag("MainCamera").transform.position = arCameraTarget.transform.position;
        GameObject.FindWithTag("MainCamera").transform.rotation = arCameraTarget.transform.rotation;

        currentRotation = Quaternion.identity;
        biasYaw = 0;
        biasPitch = 0;
        GameObject.Find("ARCanvas").GetComponent<UnityEngine.UI.RawImage>().color = new Color(1.0f, 1.0f, 1.0f, 0.0f);

        arCameraTarget.GetComponentInChildren<ARMarkerBehaviour>().StartTracking();

        return arCameraTarget;
    }

    public void StopTracking()
    {
        arCameraTarget = null;
        arCameraTargetFound = false;
        currentRotation = Quaternion.identity;
        trackingStartedCounter = -1;
        biasYaw = 0;
        biasPitch = 0;

        // reset tracking for all markers
        ARMarkerBehaviour.ResetTracking();

        GameObject.Find("ARCanvas").GetComponent<UnityEngine.UI.RawImage>().color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
    }
}
