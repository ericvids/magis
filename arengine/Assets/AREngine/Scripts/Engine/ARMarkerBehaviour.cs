/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using Vuforia;

public class ARMarkerBehaviour : MonoBehaviour, ITrackableEventHandler
{
    private bool _visible;

    private void Start()
    {
        TrackableBehaviour trackableBehaviour = GetComponentInChildren<TrackableBehaviour>();
        if (trackableBehaviour != null)
            trackableBehaviour.RegisterTrackableEventHandler(this);
    }

    public void OnTrackableStateChanged(TrackableBehaviour.Status previousStatus,
                                        TrackableBehaviour.Status newStatus)
    {
        if (! gameObject.activeSelf)
            return;  // avoid useless (but jarring) log message

        if (! GetComponentInChildren<ImageTargetBehaviour>().enabled
            && (newStatus == TrackableBehaviour.Status.TRACKED || newStatus == TrackableBehaviour.Status.EXTENDED_TRACKED))
        {
            // if we receive a tracking event even though we have already disabled the marker, something went wrong
            Debug.Log("=== WARNING: Tracking started for disabled marker " + gameObject.name);
        }

        GameObject engine = GameObject.FindWithTag("AREngine");
        if (engine != null)
        {
            AREngineBehaviour engineBehaviour = engine.GetComponent<AREngineBehaviour>();
            if (newStatus == TrackableBehaviour.Status.TRACKED)
                engineBehaviour.SetTarget(gameObject, true);
            else if (newStatus != TrackableBehaviour.Status.EXTENDED_TRACKED || DeviceInput.isAttitudeYawStable)
            {
                // lose the tracking if:
                // 1. new status is neither tracked nor extended-tracked
                // 2. new status is extended-tracked, but the device yaw is valid (so we don't need it)
                engineBehaviour.SetTarget(gameObject, false);
            }

            _visible = (newStatus == TrackableBehaviour.Status.TRACKED);
        }
    }

    public bool visible
    {
        get
        {
            return _visible;
        }
    }

    public void StartTracking()
    {
        Debug.Log("=== STARTING WITH MARKER: " + gameObject.name);
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("ARMarker"))
        {
            // since only one marker can be extended-tracked at a time, we stop it for all markers
            if (obj.GetComponentInChildren<ImageTargetBehaviour>().ImageTarget != null)
            {
                if (! obj.GetComponentInChildren<ImageTargetBehaviour>().ImageTarget.StopExtendedTracking())
                    Debug.Log("=== WARNING: Extended tracking cannot be stopped for marker " + obj.name);
            }

            if (obj == gameObject)
            {
                // if obj is the marker we're starting tracking on, enable obj
                if (! obj.GetComponentInChildren<ImageTargetBehaviour>().enabled)
                    obj.GetComponentInChildren<ImageTargetBehaviour>().enabled = true;
            }
            else if (gameObject.name != "ARTemporaryMarker")
            {
                // otherwise, if the marker we're starting tracking on is not the temporary marker, disable obj
                // (if it were the temporary marker, we would retain the previous enabled state of all markers, that is,
                // only one of the real markers and the temporary marker were enabled)
                if (obj.GetComponentInChildren<ImageTargetBehaviour>().enabled)
                    obj.GetComponentInChildren<ImageTargetBehaviour>().enabled = false;

                // additionally delete the temporary marker's trackable to start fresh
                if (obj.name == "ARTemporaryMarker")
                    obj.GetComponent<ARTemporaryMarkerBehaviour>().DeleteTrackable();
            }
        }

        if (GetComponentInChildren<ImageTargetBehaviour>().ImageTarget != null)
        {
            if (! GetComponentInChildren<ImageTargetBehaviour>().ImageTarget.StartExtendedTracking())
                Debug.Log("=== WARNING: Extended tracking cannot be started for marker " + gameObject.name);
        }
    }

    public static void ResetTracking()
    {
        Debug.Log("=== RESETTING");
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("ARMarker"))
        {
            if (obj.GetComponentInChildren<ImageTargetBehaviour>().ImageTarget != null)
            {
                if (! obj.GetComponentInChildren<ImageTargetBehaviour>().ImageTarget.StopExtendedTracking())
                    Debug.Log("=== WARNING: Extended tracking cannot be stopped for marker " + obj.name);
            }

            // enable everything except the temporary marker
            if (obj.GetComponentInChildren<ImageTargetBehaviour>().enabled != (obj.name != "ARTemporaryMarker"))
                obj.GetComponentInChildren<ImageTargetBehaviour>().enabled = (obj.name != "ARTemporaryMarker");
        }
    }
}
