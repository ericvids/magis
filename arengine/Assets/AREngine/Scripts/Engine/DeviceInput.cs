/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

public class DeviceInput
{
#if UNITY_IOS && ! UNITY_EDITOR
    [DllImport ("__Internal")]
    static extern string GetApplicationSettingsURL();
    [DllImport ("__Internal")]
    static extern int IsCameraPermitted();
# if ! MAGIS_NOGPS || MAGIS_BLE
    [DllImport ("__Internal")]
    static extern int IsLocationPermitted();
    [DllImport ("__Internal")]
    static extern int IsLocationDialogUnanswered();
# endif
#endif

    public static string NameToFolderName(string name)
    {
        return Regex.Replace(name, @"[^_A-Za-z0-9]", "");
    }

    public static string GameName()
    {
        return NameToFolderName(Application.productName);
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
    private static bool _gyro = false;

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
        androidPlugin = new AndroidJavaObject("AndroidPlugin", androidActivity, (int) (1.0f / 60.0f * 1000000));  // 60Hz

        _compassPresent = androidPlugin.Call<bool>("isRotationSensorAvailable");
        if (! _compassPresent)
            androidCompass = false;
#endif

        Input.gyro.updateInterval = 1.0f / 60.0f;
        Input.gyro.enabled = true;
#if ! UNITY_EDITOR
        // when in editor, gyro functionality can be provided by Unity Remote but cannot
        // (and should not) be toggled by the user; never set _gyroPresent to true there
        _gyroPresent = Input.gyro.enabled;
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
            return _gyro;
        }
        set
        {
            if (! gyroPresent)
                return;

            if (_gyro == value)
                return;
            _gyro = value;
            Input.gyro.enabled = _gyro;
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

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isRemoteConnected)
#else
            if (gyro)
#endif
            {
                // convert this quaternion to Unity's coordinate system
                deviceRotation = Quaternion.Euler(-90, 0, 0) * Input.gyro.attitude;
                deviceRotation.z = -deviceRotation.z;
                deviceRotation.w = -deviceRotation.w;

                // sanity check to prevent the rest of the engine from propagating malfunctioning gyroscope errors
                Vector3 e = deviceRotation.eulerAngles;
                if (! float.IsNaN(e.x) && ! float.IsNaN(e.y) && ! float.IsNaN(e.z))
                    return deviceRotation;
            }

#if UNITY_EDITOR
            return Quaternion.identity;
#else

# if UNITY_ANDROID
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
# endif
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

            return deviceRotation;
#endif
        }
    }

    public static bool isAttitudeYawStable
    {
        get
        {
#if UNITY_EDITOR
            // Vuforia play mode will return an image if a webcam is connected;
            // if we get this image, report that the yaw is "unstable" so the engine
            // will use the image feed to determine pose (otherwise the engine will
            // depend solely on the arrow keys)
            return UnityEditor.EditorApplication.isRemoteConnected
                || Vuforia.CameraDevice.Instance.GetCameraImage(Vuforia.PIXEL_FORMAT.RGBA8888) == null;
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
#if UNITY_EDITOR
            // if there is no device with gyro connected,
            // report malfunction so the PC webcam can be used on its own
            return ! UnityEditor.EditorApplication.isRemoteConnected;
#else
            return accelerometerInstabilityCounter >= ACCELEROMETER_INSTABILITY_TIMEOUT;
#endif
        }
    }

    public static bool cameraPermissionEnabled
    {
        get
        {
            if (GameObject.FindWithTag("AREngine") == null)
                return true;
#if UNITY_ANDROID && ! UNITY_EDITOR
	        return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
#elif UNITY_IOS && ! UNITY_EDITOR
            return IsCameraPermitted() != 0;
#else
            return true;
#endif
        }
    }

    public static bool locationDialogUnanswered
    {
        get
        {
            if (GameObject.FindWithTag("AREngine") != null || GameObject.Find("TitleScene") != null)
                return false;
#if UNITY_IOS && ! UNITY_EDITOR
# if ! MAGIS_NOGPS || MAGIS_BLE
            return IsLocationDialogUnanswered() != 0;
# else
            return true;
# endif
#else
            return false;
#endif
        }
    }

    public static bool locationPermissionEnabled
    {
        get
        {
            if (GameObject.FindWithTag("AREngine") != null || GameObject.Find("TitleScene") != null)
                return true;
#if UNITY_ANDROID && ! UNITY_EDITOR
# if MAGIS_NOGPS && MAGIS_BLE
            return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.CoarseLocation);
# elif ! MAGIS_NOGPS
	        return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation);
# else
            return true;
# endif
#elif UNITY_IOS && ! UNITY_EDITOR
# if ! MAGIS_NOGPS || MAGIS_BLE
            return IsLocationPermitted() != 0;
# else
            return true;
# endif
#else
            return true;
#endif
        }
    }

    public static void RequestCameraPermission()
    {
#if UNITY_ANDROID && ! UNITY_EDITOR
        if (! UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
#endif
        // iOS does not need separate request (it is requested during vuforia initialization)
    }

    public static void RequestLocationPermission()
    {
#if UNITY_ANDROID && ! UNITY_EDITOR
# if MAGIS_NOGPS && MAGIS_BLE
        if (! UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.CoarseLocation))
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.CoarseLocation);
# elif ! MAGIS_NOGPS
        if (! UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);
# endif
#endif
        // iOS does not need separate request (it is requested during location initialization)
    }

    public static bool locationHardwareEnabled
    {
        get
        {
            if (GameObject.FindWithTag("AREngine") != null || GameObject.Find("TitleScene") != null)
                return true;
            bool result = true;
#if UNITY_ANDROID && ! UNITY_EDITOR
# if ! MAGIS_NOGPS || MAGIS_BLE
            // if gps provider is enabled, location hardware is enabled for both magis modes
            result = androidActivity.Call<AndroidJavaObject>("getSystemService", "location").Call<bool>("isProviderEnabled", "gps");
# endif
# if MAGIS_BLE
            // if network provider is enabled, location hardware is enabled for magis ble mode only (require gps provider otherwise)
            result = result || androidActivity.Call<AndroidJavaObject>("getSystemService", "location").Call<bool>("isProviderEnabled", "network");
# endif
#endif
            return result;
        }
    }

    public static void RestartGame()
    {
#if UNITY_ANDROID && ! UNITY_EDITOR
        AndroidJavaObject intent = androidActivity.Call<AndroidJavaObject>("getPackageManager").Call<AndroidJavaObject>("getLaunchIntentForPackage", androidActivity.Call<AndroidJavaObject>("getPackageName"));
        androidActivity.Call("startActivity", intent.CallStatic<AndroidJavaObject>("makeRestartActivityTask", intent.Call<AndroidJavaObject>("getComponent")));
        AndroidJavaClass process = new AndroidJavaClass("android.os.Process");
        process.CallStatic("killProcess", process.CallStatic<int>("myPid"));
        Application.Quit();  // in case killing the process was blocked somehow (this is not as reliable though)
#endif
    }

    public static void ShowDeviceSettings()
    {
#if UNITY_ANDROID && ! UNITY_EDITOR
        AndroidJavaObject intent = null;
        if (! cameraPermissionEnabled || ! locationPermissionEnabled)
        {
            intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS");
            AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri");
            intent.Call<AndroidJavaObject>("setData", uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", Application.identifier, null));
        }
        else if (! locationHardwareEnabled)
            intent = new AndroidJavaObject("android.content.Intent", "android.settings.LOCATION_SOURCE_SETTINGS");
        if (intent != null)
            androidActivity.Call("startActivity", intent);
#elif UNITY_IOS && ! UNITY_EDITOR
        if (! cameraPermissionEnabled || ! locationPermissionEnabled)
            Application.OpenURL(GetApplicationSettingsURL());
#endif
    }

    public static bool batteryCharging
    {
        get
        {
#if UNITY_EDITOR
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
            return SystemInfo.batteryStatus == BatteryStatus.Charging;
#endif
        }
    }

    public static int batteryLevel
    {
        get
        {
#if UNITY_EDITOR
            return 100 - ((int) (Time.realtimeSinceStartup * 5)) % 101;
#else
            return (int) ((SystemInfo.batteryLevel + 0.005f) * 100);
#endif
        }
    }

    public static string Base64ToHex(string s)
    {
        string sub;
        if (s.Length == 1)
            sub = s + "A";
        else
            sub = s;

        int value =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".IndexOf(sub[0]) * 64 +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".IndexOf(sub[1]);

        return
            "0123456789ABCDEF".Substring(value / 256, 1) +
            "0123456789ABCDEF".Substring((value % 256) / 16, 1) +
            "0123456789ABCDEF".Substring(value % 16, 1) +
            (s.Length > 2 ? Base64ToHex(s.Substring(2)) : "");
    }

    public static string HexToBase64(string s)
    {
        string sub;
        if (s.Length == 1)
            sub = s + "00";
        else if (s.Length == 2)
            sub = s + "0";
        else
            sub = s;

        int value =
            "0123456789ABCDEF".IndexOf(sub[0]) * 256 +
            "0123456789ABCDEF".IndexOf(sub[1]) * 16 +
            "0123456789ABCDEF".IndexOf(sub[2]);

        return
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".Substring(value / 64, 1) +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".Substring(value % 64, 1) +
            (s.Length > 3 ? HexToBase64(s.Substring(3)) : "");
    }

    public static string HumanReadableEncoding(string s)
    {
        return s.Replace("l", "!").Replace("0", "*");
    }

    public static string deviceSerial
    {
        get
        {
#if UNITY_ANDROID
            int lastHex = 0;
#else
            int lastHex = 1;
#endif
            lastHex += gyroPresent ? 8 : 0;
            lastHex += compassPresent ? 4 : 0;
            lastHex += accelerometerMalfunctioning ? 0 : 2;
            string lastHexChar = "0123456789ABCDEF".Substring(lastHex, 1);
            return HexToBase64(SystemInfo.deviceUniqueIdentifier.Replace("-", "").Replace('a', 'A').Replace('b', 'B').Replace('c', 'C').Replace('d', 'D').Replace('e', 'E').Replace('f', 'F') + lastHexChar);
        }
    }

    public static void ExitGame(ButtonCanvasBehaviour buttonCanvas)
    {
#if ! UNITY_EDITOR
        if (Input.touchCount > 1)
#endif
        {
            string serial = HumanReadableEncoding(deviceSerial) + "\u00a0(" + Application.version + ")";
            string analytics = HumanReadableEncoding(PlayerPrefs.GetString("AnalyticsCode"));
            buttonCanvas.ShowQuestionOverlay("Serial:\u00a0" + serial + "\nAnalytics:\u00a0\u00a0" + analytics,
                                             "Exit game",
                                             null,
                                             delegate(string pressedButton)
            {
                buttonCanvas.HideOverlay();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
#if ! UNITY_EDITOR
        else
            Application.Quit();
#endif
    }
}
