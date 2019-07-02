/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using UnityEngine.EventSystems;
using Vuforia;

public class ARTemporaryMarkerBehaviour : MonoBehaviour, IUserDefinedTargetEventHandler
{
    private DataSet dataSet;
    private ObjectTracker objectTracker;
    private int scanningDelay;
    private int focusDelay;

    private void Start()
    {
        UserDefinedTargetBuildingBehaviour userDefinedTargetBuildingBehaviour = GetComponent<UserDefinedTargetBuildingBehaviour>();
        if (userDefinedTargetBuildingBehaviour != null)
            userDefinedTargetBuildingBehaviour.RegisterEventHandler(this);

        // disable this marker at the start
        GetComponentInChildren<ImageTargetBehaviour>().enabled = false;
    }

    private void OnApplicationPause(bool paused)
    {
        // when app sleeps, stop any deferred actions
        focusDelay = 0;
        scanningDelay = 0;
    }

    private void FixedUpdate()
    {
        GameObject engine = GameObject.FindWithTag("AREngine");
        if (engine == null)
            return;
        else if (engine.GetComponent<AREngineBehaviour>().markerState != IARMarkerState.SELECTING)
        {
            // stop continuous focus when the marker has been selected
            if (focusDelay == -1)
                CameraDevice.Instance.SetFocusMode(CameraDevice.FocusMode.FOCUS_MODE_NORMAL);
            focusDelay = -3;
        }
        else if (focusDelay == -3)
        {
            if (engine.GetComponent<AREngineBehaviour>().markerState == IARMarkerState.SELECTING)
            {
                // reinitialize when we go back to marker selection mode
                focusDelay = 0;
            }
        }
        else if (focusDelay == -2)
        {
            // manual focus
            if (EventSystem.current.currentSelectedGameObject == null)
            {
                // manually trigger focus via touch if the touch is not captured by the event system
                foreach (Touch t in Input.touches)
                {
                    if (t.phase == TouchPhase.Ended)
                    {
                        CameraDevice.Instance.SetFocusMode(CameraDevice.FocusMode.FOCUS_MODE_TRIGGERAUTO);
                        break;
                    }
                }
            }
        }
        else if (focusDelay == -1)
        {
            // continuous focus
            // do nothing
            return;
        }
        else if (focusDelay < AREngineBehaviour.INITIALIZATION_DELAY)
            focusDelay++;
        else
        {
            if (CameraDevice.Instance.SetFocusMode(CameraDevice.FocusMode.FOCUS_MODE_CONTINUOUSAUTO))
                focusDelay = -1;
            else
                focusDelay = -2;
        }
    }

    public void OnFrameQualityChanged(ImageTargetBuilder.FrameQuality frameQuality)
    {
    }

    public void OnInitialized()
    {
        if (dataSet != null)
            OnDestroy();

        objectTracker = TrackerManager.Instance.GetTracker<ObjectTracker>();
        if (objectTracker != null)
        {
            // this creates an empty data set for the temporary marker
            dataSet = objectTracker.CreateDataSet();
            objectTracker.ActivateDataSet(dataSet);
        }

        // set it so that the AR camera can only see the video feed and nothing else of the scene
        GameObject.FindWithTag("MainCamera").transform.GetChild(0).gameObject.layer = 9;  // ARBackground
    }

    public void OnDestroy()
    {
        if (dataSet != null)
        {
            objectTracker.DeactivateDataSet(dataSet);
            objectTracker.DestroyDataSet(dataSet, true);
            dataSet = null;
        }
    }

    public void DeleteTrackable()
    {
        if (dataSet != null)
        {
            objectTracker.DeactivateDataSet(dataSet);
            dataSet.DestroyAllTrackables(false);
            objectTracker.ActivateDataSet(dataSet);
        }
    }

    public void OnNewTrackableSource(TrackableSource trackableSource)
    {
        if (scanningDelay == -1)
        {
            scanningDelay = AREngineBehaviour.INITIALIZATION_DELAY;

            AREngineBehaviour engine = GameObject.FindWithTag("AREngine").GetComponent<AREngineBehaviour>();
            if (engine.markerState == IARMarkerState.LOST
                || engine.currentlySeenARMarker.name != "ARTemporaryMarker"
                   && ! engine.currentlySeenARMarker.GetComponent<ARMarkerBehaviour>().visible)
            {
                Debug.Log("Saving new marker...");
                if (dataSet != null)
                {
                    objectTracker.DeactivateDataSet(dataSet);
                    dataSet.DestroyAllTrackables(false);
                    dataSet.CreateTrackable(trackableSource, GetComponentInChildren<TrackableBehaviour>().gameObject);
                    objectTracker.ActivateDataSet(dataSet);
                }

                gameObject.GetComponent<ARMarkerBehaviour>().StartTracking();
            }
        }
    }

    public bool MakeNewMarker()
    {
        if (scanningDelay == 0)
        {
            scanningDelay = -1;
            Debug.Log("Making new marker...");
            GetComponent<UserDefinedTargetBuildingBehaviour>().BuildNewTarget("ARTemporaryMarker", 20.0f);
            return true;
        }
        else if (scanningDelay > 0)
            scanningDelay--;
        return false;
    }

    public GameObject originalMarker
    {
        get
        {
            foreach (GameObject obj in GameObject.FindGameObjectsWithTag("ARMarker"))
            {
                if (obj.name != "ARTemporaryMarker" && obj.GetComponentInChildren<ImageTargetBehaviour>().enabled)
                    return obj;
            }
            return null;
        }
    }
}
