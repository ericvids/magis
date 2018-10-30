/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

public class DeviceInput
{
#if UNITY_IOS && ! UNITY_EDITOR
    [DllImport ("__Internal")]
    static extern void StartBattery();
    [DllImport ("__Internal")]
    static extern int GetBatteryLevel();
    [DllImport ("__Internal")]
    static extern void LaunchPrivacy();
#endif

    public static string GameName()
    {
        return Regex.Replace(Application.productName, @"[^_A-Za-z0-9]", "");
    }

    // if accelerometer becomes unstable for more than this number of seconds, disable device angle pitch and roll readings
    public const float ACCELEROMETER_INSTABILITY_TIMEOUT = 2.0f;

    // if the magnitude of the accelerometer is beyond these values, it is currently unstable
    public const float ACCELEROMETER_INSTABILITY_MINIMUM = 0.85f;
    public const float ACCELEROMETER_INSTABILITY_MAXIMUM = 1.1f;

    private static bool _gyroPresent = false;
    private static bool _compassPresent = false;
    private static float accelerometerInstabilityCounter = 0.0f;
    private static QuaternionMovingAverage average = new QuaternionMovingAverage();

#if UNITY_ANDROID && ! UNITY_EDITOR
    // android-only state
    private static AndroidJavaObject androidActivity;
    private static AndroidJavaObject androidPlugin;
    private static bool androidCompass;
#endif

    public static void Init()
    {
#if UNITY_ANDROID && ! UNITY_EDITOR
        // get the rotation sensor implemented in java
        androidActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
        androidPlugin = new AndroidJavaObject("AndroidPlugin", androidActivity, (int) (Time.fixedDeltaTime * 1000000));

        _compassPresent = androidPlugin.Call<bool>("isRotationSensorAvailable");
        if (! _compassPresent)
            androidCompass = false;
#elif UNITY_IOS && ! UNITY_EDITOR
        StartBattery();
#endif

#if UNITY_EDITOR
        // in editor, gyro functionality can be provided by Unity remote but cannot be toggled
        _gyroPresent = false;
        Input.gyro.enabled = true;
#else
        Input.gyro.updateInterval = Time.fixedDeltaTime;
        bool oldGyro = Input.gyro.enabled;
        Input.gyro.enabled = true;
        _gyroPresent = Input.gyro.enabled;
        Input.gyro.enabled = oldGyro;
#endif
    }

    public static void Destroy()
    {
#if UNITY_ANDROID && ! UNITY_EDITOR
        androidPlugin.Call("destroy");
#endif
    }

    public static bool gyroPresent
    {
        get
        {
            return _gyroPresent;
        }
    }

    public static bool compassPresent
    {
        get
        {
            return _compassPresent;
        }
    }

    public static bool gyro
    {
        get
        {
            return Input.gyro.enabled;
        }
        set
        {
            if (! gyroPresent)
                return;

            if (Input.gyro.enabled == value)
                return;
            Input.gyro.enabled = value;
            average.Reset();

            GameObject engine = GameObject.FindWithTag("AREngine");
            if (engine != null)
                engine.GetComponent<AREngineBehaviour>().ResetTracking();
        }
    }

    public static bool compass
    {
        get
        {
#if UNITY_ANDROID && ! UNITY_EDITOR
            return androidCompass;
#else
            return false;
#endif
        }
        set
        {
            if (! compassPresent)
                return;

#if UNITY_ANDROID && ! UNITY_EDITOR
            if (androidCompass == value)
                return;
            androidCompass = value;
#endif
            average.Reset();

            GameObject engine = GameObject.FindWithTag("AREngine");
            if (engine != null)
                engine.GetComponent<AREngineBehaviour>().ResetTracking();
        }
    }

    public static Quaternion attitude
    {
        get
        {
            Quaternion deviceRotation;
            if (gyro)
            {
                // convert this quaternion to Unity's coordinate system
                deviceRotation = Quaternion.Euler(-90, 0, 0) * Input.gyro.attitude;
                deviceRotation.z = -deviceRotation.z;
                deviceRotation.w = -deviceRotation.w;
            }
            else
            {
#if UNITY_ANDROID && ! UNITY_EDITOR
                if (compass)
                {
                    // if on Android, use rotation sensor
                    deviceRotation = new Quaternion(androidPlugin.Call<float>("getX"),
                                                    androidPlugin.Call<float>("getY"),
                                                    androidPlugin.Call<float>("getZ"),
                                                    androidPlugin.Call<float>("getW"));

                    // apply a compensating roll rotation depending on the orientation of the device's display
                    // (we're not using Unity's ScreenOrientation because of a bug that incorrectly returns upside-down portrait
                    // on an upside-down-oriented phone that does not actually support displaying in upside-down portrait)
                    int rotation = androidActivity.Call<AndroidJavaObject>("getSystemService", "window")
                                                   .Call<AndroidJavaObject>("getDefaultDisplay").Call<int>("getRotation");
                    if (rotation == 1)
                        deviceRotation *= Quaternion.Euler(0, 0, -90);
                    else if (rotation == 2)
                        deviceRotation *= Quaternion.Euler(0, 0, 180);
                    else if (rotation == 3)
                        deviceRotation *= Quaternion.Euler(0, 0, 90);

                    // convert this quaternion to Unity's coordinate system
                    deviceRotation = Quaternion.Euler(-90, 0, 0) * deviceRotation;
                    deviceRotation.z = -deviceRotation.z;
                    deviceRotation.w = -deviceRotation.w;
                }
                else
#endif
                {
                    Vector3 accel = Input.acceleration;
                    deviceRotation = Quaternion.Euler(new Vector3(
                        Mathf.Atan2(-accel.z,
                                    Mathf.Sqrt(accel.y * accel.y + accel.x * accel.x)
                                   ) / Mathf.PI * 180,
                        0.0f,
                        Mathf.Atan2(-accel.x,
                                    (accel.y > 0 ? -1 : 1)
                                    * Mathf.Sqrt(accel.y * accel.y + 0.1f * accel.z * accel.z)
                                   ) / Mathf.PI * 180
                    ));
                }

                average.AddSample(deviceRotation);
                deviceRotation = average.GetAverage();
            }

#if UNITY_EDITOR
            if (! UnityEditor.EditorApplication.isRemoteConnected)
                deviceRotation = Quaternion.identity;
#endif
            return deviceRotation;
        }
    }

    public static bool isAttitudeYawStable
    {
        get
        {
#if UNITY_EDITOR
            return true;
#else
            return compass || gyro;
#endif
        }
    }

    public static void CheckMalfunction()
    {
        if (accelerometerInstabilityCounter < ACCELEROMETER_INSTABILITY_TIMEOUT)
        {
            if (Input.acceleration.magnitude < ACCELEROMETER_INSTABILITY_MINIMUM
                || Input.acceleration.magnitude > ACCELEROMETER_INSTABILITY_MAXIMUM)
            {
                accelerometerInstabilityCounter += Time.deltaTime;
            }
            else
            {
                accelerometerInstabilityCounter = 0.0f;
            }
        }
    }

    public static bool accelerometerMalfunctioning
    {
        get
        {
            return accelerometerInstabilityCounter >= ACCELEROMETER_INSTABILITY_TIMEOUT;
        }
    }

    public static bool locationEnabled
    {
        get
        {
            if (GameObject.FindWithTag("AREngine") != null)
            {
                if (Input.location.status == LocationServiceStatus.Running)
                    Input.location.Stop();
                return true;
            }
            else if (GameObject.Find("TitleScene") != null)
                return true;  // don't warn about turning on location in title screen
            else
                return Input.location.status == LocationServiceStatus.Running;
        }
    }

    public static bool gpsEnabled
    {
        get
        {
#if UNITY_ANDROID && ! UNITY_EDITOR
            return Input.location.status != LocationServiceStatus.Running
                   || androidActivity.Call<AndroidJavaObject>("getSystemService", "location").Call<bool>("isProviderEnabled", "gps");
#else
            return true;
#endif
        }
    }

    public static void ShowLocationAccess()
    {
        if (locationEnabled && gpsEnabled)
            return;

#if UNITY_ANDROID && ! UNITY_EDITOR
        AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", "android.settings.LOCATION_SOURCE_SETTINGS");
        androidActivity.Call("startActivity", intent);
#elif UNITY_IOS && ! UNITY_EDITOR
        LaunchPrivacy();
#endif
    }

    public static int batteryLevel
    {
        get
        {
#if UNITY_ANDROID && ! UNITY_EDITOR
            return androidPlugin.Call<int>("getBatteryLevel");
#elif UNITY_IOS && ! UNITY_EDITOR
            return GetBatteryLevel();
#else
            return 100 - ((int) (Time.realtimeSinceStartup * 2)) % 101;
#endif
        }
    }
}
