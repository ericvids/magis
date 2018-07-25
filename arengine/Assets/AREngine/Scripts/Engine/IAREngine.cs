/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using System.Collections.Generic;

/// possible marker tracking states
public enum IARMarkerState
{
    SELECTING,  // user is still currently selecting a marker to track
    STARTING,   // user has selected a marker to track and it is currently being searched (or gyro/compass tracking is reset)
    TRACKING,   // user has selected a marker to track and it is found (if gyroscope/magnetometer present, tracking will never be lost!)
    LOST,       // user has selected a marker to track but it was not found
    TEMPORARY   // user has selected a marker to track but it was not found, although the engine is able to track yaw rotation temporarily
}

public interface IAREngine
{
    /// returns the current state of marker tracking
    IARMarkerState markerState
    {
        get;
    }

    /// returns the current ARMarker being seen by the AR system, or null if no ARMarker was yet found;
    /// if this is not null, startTracking(null) is guaranteed to start (i.e., the API user may
    /// check this value to determine whether a camera shutter icon should be shown)
    ///
    /// if tracking is already started via startTracking(), the currently-tracked ARMarker is always returned
    /// whether or not the tracked ARMarker is actually visible in the camera feed
    GameObject currentlySeenARMarker
    {
        get;
    }

    /// returns true if currentlySeenARMarker is actually visible within the camera
    /// (if the marker is not actually visible, the engine can continue tracking, but the currentlySeenARMarker
    /// object will return wrong positions with respect to the AR camera)
    bool isARMarkerActuallyVisible
    {
        get;
    }

    /// signal the game to start tracking, changing the marker state to TRACKING;
    /// - if null is passed, the currentlySeenARMarker is returned (if this returns null, the marker state did not
    ///   change from SELECTING
    /// - if a whichARMarker parameter is passed, tracking is forced to start with that ARMarker even if the system
    ///   cannot find the marker (useful for poor lighting conditions), but note that the yaw orientation will
    ///   be reset as soon as the marker is actually found
    GameObject StartTracking(GameObject whichARMarker = null);

    /// stops tracking and returns the marker state to SELECTING
    void StopTracking();
}
