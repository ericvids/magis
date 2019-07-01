/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

[InitializeOnLoad]
public class Autorun
{
    static string loadedLevel;
    static Dictionary<string, string> productNameToCloudProjectId = new Dictionary<string, string>();
    static bool restarting = false;

    public static bool buildNotReady = true;
    public static bool singleGameProject = false;

    enum ARGameList
    {
        GPS_SUPPORT = 0,
        BLE_SUPPORT,
        PRODUCT_NAME,
        IOS_ID,
        ANDROID_ID,
        CLOUD_PROJECT_ID,
        CLOUD_PROJECT_NAME,
        ANDROID_KEYALIAS_NAME,
        VUFORIA_LICENSE_KEY
    };

    static Autorun()
    {
        EditorApplication.update += Update;

        try
        {
            StreamReader reader = new StreamReader("Assets/ARGames/ARGameList.txt");
            string[] rows = reader.ReadToEnd().Split('\n');
            reader.Close();
            for (int i = 0; i < rows.Length; i++)
            {
                // add product name to cloud id lookup
                if (rows[i].EndsWith("\r"))
                    rows[i] = rows[i].Substring(0, rows[i].Length - 1);
                string[] cols = rows[i].Split(',');
                if (cols.Length > (int) ARGameList.CLOUD_PROJECT_ID)
                    productNameToCloudProjectId[cols[(int) ARGameList.PRODUCT_NAME]] = cols[(int) ARGameList.CLOUD_PROJECT_ID];
            }
        }
        catch (Exception)
        {
            singleGameProject = true;
        }

        foreach (KeyValuePair<string, string> entry in productNameToCloudProjectId)
        {
            // unhide resources folder of current game, hide resources folders of other games
            bool currentGame = (Application.productName == entry.Key);
            string folderName = Application.dataPath + "/ARGames/" + DeviceInput.NameToFolderName(entry.Key) + "/Resources";
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            if ((int) System.Environment.OSVersion.Platform == 4 || (int) System.Environment.OSVersion.Platform == 6)
            {
                process.StartInfo.FileName = "chflags";
                process.StartInfo.Arguments = (currentGame ? "nohidden \"" : "hidden \"") + folderName + "\"";
            }
            else
            {
                process.StartInfo.FileName = "attrib.exe";
                process.StartInfo.Arguments = (currentGame ? "-h \"" : "+h \"") + folderName.Replace('/', '\\') + "\"";
            }
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.WaitForExit();
        }

        // force recompile when switching from a different game
        if (AssetDatabase.IsValidFolder("Assets/_DO NOT COMMIT RIGHT NOW - If Unity crashed, restart it now"))
        {
            AssetDatabase.DeleteAsset("Assets/_DO NOT COMMIT RIGHT NOW - If Unity crashed, restart it now");
            AssetDatabase.Refresh();

            foreach (string asset in AssetDatabase.FindAssets("t:Script"))
            {
                string path = AssetDatabase.GUIDToAssetPath(asset);
                if (path.StartsWith("Assets/") && path != "Assets/AREngine/Editor/Autorun.cs")
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
        }
        AssetDatabase.Refresh();

        CleanUp();
    }

    static string Quotify(string s)
    {
        string r = s.Replace("'", "''");
        if (! Regex.IsMatch(r, @"^[ _A-Za-z0-9]*$"))
            r = "'" + r + "'";
        return r;
    }

    static void Update()
    {
        if (CheckIfProjectSwitchNeeded())
            return;

        if (EditorApplication.isPlaying)
        {
            if (! AssetDatabase.IsValidFolder("Assets/_DO NOT COMMIT RIGHT NOW - Unity is using the project"))
            {
                if (buildNotReady)
                {
                    // do not allow running at an inconsistent state
                    Debug.LogError("Cannot play because MAGIS project is in an inconsistent state. Please fix any issues that weren't resolved by Autorun.CleanUp() and reload the project.");
                    EditorApplication.isPlaying = false;
                    return;
                }
                buildNotReady = true;

                // upon running, hide unnecessary resources
                AssetDatabase.CreateFolder("Assets", "_DO NOT COMMIT RIGHT NOW - Unity is using the project");
                AssetDatabase.Refresh();

                // force editor to play at 1x scale or lower
                Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                EditorWindow w = EditorWindow.GetWindow(type);
                var areaField = type.GetField("m_ZoomArea", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var areaObj = areaField.GetValue(w);
                var scaleField = areaObj.GetType().GetField("m_Scale", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Vector2 value = (Vector2) scaleField.GetValue(areaObj);
                if (value.x > 1.0f)
                    scaleField.SetValue(areaObj, new Vector2(1.0f, 1.0f));

                loadedLevel = null;
                if (GameObject.FindWithTag("ARMarker") != null && GameObject.FindWithTag("AREngine") == null)
                {
                    // temporarily halt the loading of an AR editor level to load CommonScene
                    loadedLevel = SceneManager.GetActiveScene().name;
                    SceneManager.LoadScene("CommonScene");
                    Debug.Log("Starting ARScene");
                }
                else if (GameObject.Find("GameState") == null)
                {
                    // for other levels that come with arengine, always run from the beginning
                    if (SceneManager.GetActiveScene().name == "ARScene"
                        || SceneManager.GetActiveScene().name == "ARSubscene"
                        || SceneManager.GetActiveScene().name == "CommonScene"
                        || SceneManager.GetActiveScene().name == "TitleScene"
                        || SceneManager.GetActiveScene().name == "MapScene"
                        || SceneManager.GetActiveScene().name == "")
                    {
                        SceneManager.LoadScene("CommonScene");
                        Debug.Log("Starting MAGIS from the title screen");
                    }
                }
            }
            else if (buildNotReady && loadedLevel != null)
            {
                // actually load the current editor level, and also load ARScene automatically if needed
                SceneManager.LoadScene(loadedLevel);
                SceneManager.LoadScene("ARScene", LoadSceneMode.Additive);
                loadedLevel = null;
            }
        }
        else if (! EditorApplication.isPlaying && ! EditorApplication.isCompiling && ! EditorApplication.isUpdating)
        {
            // automatically switch target to iOS or Android if the current target is Windows, macOS, etc.
            // (doing it here intentionally because we don't want to do it during Autorun constructor)
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS
                && EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                buildNotReady = true;
                if ((int) System.Environment.OSVersion.Platform == 4 || (int) System.Environment.OSVersion.Platform == 6)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
                else
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                return;
            }
            else
            {
                if (EditorUserBuildSettings.GetBuildLocation(BuildTarget.iOS) != "ios_" + DeviceInput.GameName())
                    EditorUserBuildSettings.development = true;
                EditorUserBuildSettings.buildAppBundle = ! EditorUserBuildSettings.development;
                EditorUserBuildSettings.SetBuildLocation(BuildTarget.iOS, "ios_" + DeviceInput.GameName());
                if (EditorUserBuildSettings.buildAppBundle)
                    EditorUserBuildSettings.SetBuildLocation(BuildTarget.Android, "aab_" + DeviceInput.GameName() + ".aab");
                else
                    EditorUserBuildSettings.SetBuildLocation(BuildTarget.Android, "apk_" + DeviceInput.GameName() + ".apk");
            }

            // fix to remove empty .fbm folders that create spurious meta files
            // (doing it here intentionally because we don't want to do it during Autorun constructor)
            foreach (string asset in AssetDatabase.FindAssets(".fbm"))
            {
                string folder = AssetDatabase.GUIDToAssetPath(asset);
                if (AssetDatabase.IsValidFolder(folder))
                {
                    if (AssetDatabase.FindAssets("t:Object", new[]{ folder }).Length == 0)
                    {
                        buildNotReady = true;
                        Debug.Log("Deleting empty folder " + folder);
                        AssetDatabase.DeleteAsset(folder);
                    }
                }
            }

            // fix to remove extraneous _TerrainAutoUpgrade
            // (doing it here intentionally because we don't want to do it during Autorun constructor)
            if (AssetDatabase.IsValidFolder("Assets/_TerrainAutoUpgrade"))
            {
                buildNotReady = true;
                Debug.Log("Deleting migration folder _TerrainAutoUpgrade");
                AssetDatabase.DeleteAsset("Assets/_TerrainAutoUpgrade");
            }

            CleanUp();
        }
        else
            buildNotReady = true;
    }

    static void RestoreFile(string file)
    {
        if (AssetDatabase.AssetPathToGUID(file + ".bak") != "")
        {
            buildNotReady = true;
            Debug.Log("Restoring " + file);
            AssetDatabase.MoveAsset(file + ".bak", file);
        }
    }

    static void CleanUp()
    {
        RestoreFile("Assets/AREngine/Plugins/iOS/Location.mm");
        RestoreFile("Assets/Plugins/Android/AndroidManifest.xml");
        RestoreFile("Assets/Plugins/Android/unityandroidbluetoothlelib.jar");
        RestoreFile("Assets/Plugins/iOS/UnityBluetoothLE.mm");

        if (AssetDatabase.IsValidFolder("Assets/StreamingAssetsBackup"))
        {
            buildNotReady = true;
            Debug.Log("Restoring StreamingAssetsBackup");

            // do this after build success/fail/cancel

            // move the markers from the temporary folder to their original locations
            foreach (string guid in AssetDatabase.FindAssets("t:Object", new[]{ "Assets/StreamingAssets/Vuforia" }))
            {
                string asset = AssetDatabase.GUIDToAssetPath(guid);
                if (asset.EndsWith(".xml"))  // move xml first before dat to avoid warnings
                    AssetDatabase.MoveAsset(asset, asset.Replace("/StreamingAssets/", "/StreamingAssetsBackup/"));
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Object", new[]{ "Assets/StreamingAssets/Vuforia" }))
            {
                string asset = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.MoveAsset(asset, asset.Replace("/StreamingAssets/", "/StreamingAssetsBackup/"));
            }
            AssetDatabase.DeleteAsset("Assets/StreamingAssets/Vuforia");
            AssetDatabase.DeleteAsset("Assets/StreamingAssets");
            AssetDatabase.MoveAsset("Assets/StreamingAssetsBackup", "Assets/StreamingAssets");
        }

        // lastly, remove do-not-commit marker
        if (AssetDatabase.IsValidFolder("Assets/_DO NOT COMMIT RIGHT NOW - Unity is using the project"))
        {
            buildNotReady = true;
            AssetDatabase.DeleteAsset("Assets/_DO NOT COMMIT RIGHT NOW - Unity is using the project");
            Debug.Log("Restore finished, project assets ready to play");
        }

        if (buildNotReady)
        {
            AssetDatabase.Refresh();
            buildNotReady = false;
        }
    }

    static bool CheckIfProjectSwitchNeeded()
    {
        if (singleGameProject || EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            // do not switch game projects if no ARGameList.txt is found or when Unity is busy doing stuff
            return false;
        }

        if (restarting || ! productNameToCloudProjectId.ContainsKey(Application.productName))
        {
            // do not waste time in Update() when we are in an invalid state
            return true;
        }

        if (CloudProjectSettings.projectId != "" && productNameToCloudProjectId[Application.productName] != CloudProjectSettings.projectId)
        {
            restarting = true;

            EditorUtility.DisplayDialog("MAGIS", "The current MAGIS game has switched to '" + Application.productName + "'.\n\nRestart of Unity is required.\n\n" + "Current project id = " + CloudProjectSettings.projectId + "\nNew project id = " + productNameToCloudProjectId[Application.productName], "Restart Unity");

            AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            StreamReader reader = new StreamReader("Assets/ARGames/ARGameList.txt");
            string[] rows = reader.ReadToEnd().Split('\n');
            reader.Close();
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].EndsWith("\r"))
                    rows[i] = rows[i].Substring(0, rows[i].Length - 1);
                string[] cols = rows[i].Split(',');
                if (cols[(int) ARGameList.PRODUCT_NAME] == Application.productName)
                {
                    reader = new StreamReader("ProjectSettings/ProjectSettings.asset");
                    rows = reader.ReadToEnd().Split('\n');
                    reader.Close();

                    for (i = 0; i < rows.Length; i++)
                    {
                        if (rows[i].EndsWith("\r"))
                            rows[i] = rows[i].Substring(0, rows[i].Length - 1);

                        int versionMajor = System.DateTime.Now.Year - 2015;  // Igpaw's first release was in 2015
                        int versionMinor = System.DateTime.Now.Month;
                        int versionRevision = System.DateTime.Now.Day;
                        string versionString = versionMajor + "." + versionMinor + "." + versionRevision;
                        int versionCode = versionMajor * 10000 + versionMinor * 100 + versionRevision;

                        if (rows[i].StartsWith("  bundleVersion: "))
                            rows[i] = "  bundleVersion: " + versionString;
                        if (rows[i].StartsWith("    iPhone: ") && rows[i].Length > 13 && rows[i][12] >= '0' && rows[i][12] <= '9' && rows[i][13] >= '0' && rows[i][13] <= '9')
                            rows[i] = "    iPhone: " + versionCode;
                        if (rows[i].StartsWith("  AndroidBundleVersionCode: "))
                            rows[i] = "  AndroidBundleVersionCode: " + versionCode;
                        if (rows[i].StartsWith("  productName: "))
                            rows[i] = "  productName: " + Quotify(cols[(int) ARGameList.PRODUCT_NAME]);
                        if (rows[i].StartsWith("    iPhone: ") && rows[i][12] >= 'a' && rows[i][12] <= 'z')
                            rows[i] = "    iPhone: " + cols[(int) ARGameList.IOS_ID];
                        if (rows[i].StartsWith("    Android: ") && rows[i][13] >= 'a' && rows[i][13] <= 'z')
                            rows[i] = "    Android: " + cols[(int) ARGameList.ANDROID_ID];
                        if (rows[i].StartsWith("  cloudProjectId: "))
                            rows[i] = "  cloudProjectId: " + cols[(int) ARGameList.CLOUD_PROJECT_ID];
                        if (rows[i].StartsWith("  projectName: "))
                            rows[i] = "  projectName: " + Quotify(cols[(int) ARGameList.CLOUD_PROJECT_NAME]);
                        if (rows[i].StartsWith("  AndroidKeystoreName: "))
                        {
                            if (cols[(int) ARGameList.ANDROID_KEYALIAS_NAME] == "")
                                rows[i] = "  AndroidKeystoreName: ";
                            else
                                rows[i] = "  AndroidKeystoreName: '{inproject}: " + DeviceInput.GameName() + ".keystore'";
                        }
                        if (rows[i].StartsWith("  AndroidKeyaliasName: "))
                        {
                            if (cols[(int) ARGameList.ANDROID_KEYALIAS_NAME] == "")
                                rows[i] = "  AndroidKeyaliasName: ";
                            else
                                rows[i] = "  AndroidKeyaliasName: " + Quotify(cols[(int) ARGameList.ANDROID_KEYALIAS_NAME]);
                        }
                        if (rows[i].StartsWith("  androidUseCustomKeystore: "))
                        {
                            if (cols[(int) ARGameList.ANDROID_KEYALIAS_NAME] == "")
                                rows[i] = "  androidUseCustomKeystore: 0";
                            else
                                rows[i] = "  androidUseCustomKeystore: 1";
                        }
                        if (rows[i].StartsWith("  androidSplashScreen: {fileID: 2800000, guid: "))
                        {
                            reader = new StreamReader("Assets/ARGames/" + DeviceInput.GameName() + "/Resources/LoadingScreen.png.meta");
                            string text = reader.ReadToEnd();
                            reader.Close();
                            string guid = text.Substring(text.IndexOf("guid: ") + 6, 32);
                            rows[i] = "  androidSplashScreen: {fileID: 2800000, guid: " + guid + ", type: 3}";
                        }
                        if (rows[i].StartsWith("      m_Icon: {fileID: 2800000, guid: "))
                        {
                            reader = new StreamReader("Assets/ARGames/" + DeviceInput.GameName() + "/Resources/AppIcon.png.meta");
                            string text = reader.ReadToEnd();
                            reader.Close();
                            string guid = text.Substring(text.IndexOf("guid: ") + 6, 32);
                            rows[i] = "      m_Icon: {fileID: 2800000, guid: " + guid + ", type: 3}";
                        }
                        if (rows[i].StartsWith("    4: VUFORIA_IOS_SETTINGS"))
                            rows[i] = "    4: VUFORIA_IOS_SETTINGS;MAGIS_" + DeviceInput.GameName() + (int.Parse(cols[(int) ARGameList.GPS_SUPPORT]) == 0 ? ";MAGIS_NOGPS" : "") + (int.Parse(cols[(int) ARGameList.BLE_SUPPORT]) == 0 ? "" : ";MAGIS_BLE");
                        if (rows[i].StartsWith("    7: VUFORIA_ANDROID_SETTINGS"))
                            rows[i] = "    7: VUFORIA_ANDROID_SETTINGS;MAGIS_" + DeviceInput.GameName() + (int.Parse(cols[(int) ARGameList.GPS_SUPPORT]) == 0 ? ";MAGIS_NOGPS" : "") + (int.Parse(cols[(int) ARGameList.BLE_SUPPORT]) == 0 ? "" : ";MAGIS_BLE");
                    }

                    while (rows[rows.Length - 1] == "")
                        Array.Resize(ref rows, rows.Length - 1);
                    StreamWriter writer = new StreamWriter("ProjectSettings/ProjectSettings.asset");
                    foreach (string row in rows)
                    {
                        writer.WriteLine(row);
                    }
                    writer.Close();

                    reader = new StreamReader("Assets/Resources/VuforiaConfiguration.asset");
                    rows = reader.ReadToEnd().Split('\n');
                    reader.Close();

                    for (i = 0; i < rows.Length; i++)
                    {
                        if (rows[i].EndsWith("\r"))
                            rows[i] = rows[i].Substring(0, rows[i].Length - 1);

                        if (rows[i].StartsWith("    vuforiaLicenseKey: "))
                            rows[i] = "    vuforiaLicenseKey: " + cols[(int) ARGameList.VUFORIA_LICENSE_KEY];
                        else if (rows[i].StartsWith("    ufoLicenseKey: "))
                            rows[i] = "    ufoLicenseKey: ";
                        else if (rows[i].StartsWith("    deviceNameSetInEditor: "))
                            rows[i] = "    deviceNameSetInEditor: ";
                        else if (rows[i].StartsWith("    turnOffWebCam: "))
                            rows[i] = "    turnOffWebCam: 1";
                    }

                    while (rows[rows.Length - 1] == "")
                        Array.Resize(ref rows, rows.Length - 1);
                    writer = new StreamWriter("Assets/Resources/VuforiaConfiguration.asset");
                    foreach (string row in rows)
                    {
                        writer.WriteLine(row);
                    }
                    writer.Close();

                    writer = new StreamWriter("ProjectSettings/EditorBuildSettings.asset");
                    writer.WriteLine("%YAML 1.1");
                    writer.WriteLine("%TAG !u! tag:unity3d.com,2011:");
                    writer.WriteLine("--- !u!1045 &1");
                    writer.WriteLine("EditorBuildSettings:");
                    writer.WriteLine("  m_ObjectHideFlags: 0");
                    writer.WriteLine("  serializedVersion: 2");
                    writer.WriteLine("  m_Scenes:");
                    writer.WriteLine("  - enabled: 1");
                    writer.WriteLine("    path: Assets/AREngine/Scenes/CommonScene.unity");
                    writer.WriteLine("  - enabled: 1");
                    writer.WriteLine("    path: Assets/AREngine/Scenes/ARScene.unity");
                    writer.WriteLine("  - enabled: 1");
                    writer.WriteLine("    path: Assets/AREngine/Scenes/ARSubscene.unity");
                    writer.WriteLine("  - enabled: 1");
                    writer.WriteLine("    path: Assets/AREngine/Scenes/TitleScene.unity");
                    foreach (string scene in Directory.GetFiles("Assets/ARGames/" + DeviceInput.GameName() + "/Scenes"))
                    {
                        if (scene.EndsWith(".unity"))
                        {
                            writer.WriteLine("  - enabled: 1");
                            writer.WriteLine("    path: Assets/ARGames/" + DeviceInput.GameName() + "/Scenes/" + Path.GetFileName(scene));
                        }
                    }
                    writer.Close();

                    AssetDatabase.CreateFolder("Assets", "_DO NOT COMMIT RIGHT NOW - If Unity crashed, restart it now");
                    EditorApplication.OpenProject(Application.dataPath + "/..");
                    return true;
                }
            }

            // if we get here, the project is not defined (but this should normally not happen!)
            EditorUtility.DisplayDialog("MAGIS", "The project name '" + Application.productName + "' is not defined in Assets/ARGames/ARGameList.txt.\n\nPlease add an entry for this project name and restart Unity.", "Close Unity");
            EditorApplication.Exit(1);
            return true;
        }

        return false;
    }
}
