/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

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

    public static bool readyToBuild = false;
    public static bool singleGameProject = false;

    enum ARGameList
    {
        GPS_SUPPORT = 0,
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

        // force reimport when switching from a different game
        if (AssetDatabase.IsValidFolder("Assets/_DO NOT COMMIT RIGHT NOW - If Unity crashed, restart it now"))
        {
            AssetDatabase.DeleteAsset("Assets/_DO NOT COMMIT RIGHT NOW - If Unity crashed, restart it now");

            // force-reimporting one script at the same execution level will force recompile of the whole execution level
            AssetDatabase.ImportAsset("Assets/AREngine/Scripts/Utils/TSVLookup.cs", ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
        }
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
        readyToBuild = false;

        // automatically switch target to iOS or Android at startup
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS
            && EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            if ((int) System.Environment.OSVersion.Platform == 4 || (int) System.Environment.OSVersion.Platform == 6)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            }
            else
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }
            EditorUserBuildSettings.development = true;
        }

        if (CheckIfProjectSwitchNeeded())
            return;

        if (EditorApplication.isPlaying)
        {
            if (! AssetDatabase.IsValidFolder("Assets/_DO NOT COMMIT RIGHT NOW - Unity is using the project"))
            {
                // upon running, hide unnecessary resources
                AssetDatabase.CreateFolder("Assets", "_DO NOT COMMIT RIGHT NOW - Unity is using the project");
                Autorun.BackupResources();

                AssetDatabase.Refresh();

                if (GameObject.FindWithTag("ARMarker") != null && GameObject.FindWithTag("AREngine") == null)
                {
                    // temporarily halt the loading of an AR editor level to load CommonScene
                    loadedLevel = SceneManager.GetActiveScene().name;
                    SceneManager.LoadScene("CommonScene");
                }
                else
                {
                    // for other levels that come with arengine, always run from the beginning
                    if (SceneManager.GetActiveScene().name == "ARScene"
                        || SceneManager.GetActiveScene().name == "ARSubscene"
                        || SceneManager.GetActiveScene().name == "CommonScene"
                        || SceneManager.GetActiveScene().name == "TitleScene"
                        || SceneManager.GetActiveScene().name == "MapScene"
                        || SceneManager.GetActiveScene().name == "")
                    {
                        ClearLogWindow();
                        SceneManager.LoadScene("CommonScene");
                    }
                }
            }
            else if (loadedLevel != null)
            {
                ClearLogWindow();

                // actually load the current editor level, and also load ARScene automatically if needed
                SceneManager.LoadScene(loadedLevel);
                SceneManager.LoadScene("ARScene", LoadSceneMode.Additive);
                loadedLevel = null;
            }
        }

        if (! EditorApplication.isPlaying && ! EditorApplication.isCompiling && ! EditorApplication.isUpdating)
        {
            bool modified = false;

            // fix to remove empty .fbm folders that create spurious meta files
            foreach (string asset in AssetDatabase.FindAssets(".fbm"))
            {
                string folder = AssetDatabase.GUIDToAssetPath(asset);
                if (AssetDatabase.IsValidFolder(folder))
                {
                    if (AssetDatabase.FindAssets("t:Object", new[]{ folder }).Length == 0)
                    {
                        Debug.Log("Empty folder " + folder + " deleted.");
                        AssetDatabase.DeleteAsset(folder);
                        modified = true;
                    }
                }
            }

            // fix to remove extraneous _TerrainAutoUpgrade
            if (AssetDatabase.IsValidFolder("Assets/_TerrainAutoUpgrade"))
            {
                AssetDatabase.DeleteAsset("Assets/_TerrainAutoUpgrade");
                modified = true;
            }

            if (AssetDatabase.IsValidFolder("Assets/StreamingAssetsBackup"))
            {
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
                modified = true;
            }

            // move ResourcesBackup back to Resources
            if (AssetDatabase.IsValidFolder("Assets/_DO NOT COMMIT RIGHT NOW - Unity is using the project"))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Folder ResourcesBackup"))
                {
                    string folder = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.MoveAsset(folder, folder.Substring(0, folder.Length - 6));
                }
                AssetDatabase.DeleteAsset("Assets/_DO NOT COMMIT RIGHT NOW - Unity is using the project");
                modified = true;
            }

            if (modified)
                AssetDatabase.Refresh();
            else
                readyToBuild = true;
        }
    }

    public static void BackupResources()
    {
        if (singleGameProject)
            return;

        // to ensure that only the resources of the current game are visible,
        // rename all other Resources to ResourcesBackup
        foreach (string guid in AssetDatabase.FindAssets("t:Folder Resources"))
        {
            string folder = AssetDatabase.GUIDToAssetPath(guid);
            if (folder != "Assets/AREngine/Resources"
                && folder != "Assets/AREngine/Editor/Resources"
                && folder != "Assets/ARGames/" + DeviceInput.GameName() + "/Resources"
                && folder != "Assets/Resources")
            {
                AssetDatabase.MoveAsset(folder, folder + "Backup");
            }
        }
    }

    public static void ClearLogWindow()
    {
        System.Type logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
        System.Reflection.MethodInfo clear
            = logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        clear.Invoke(null, null);
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
                        if (rows[i].StartsWith("    iOS: ") && rows[i][9] >= '0' && rows[i][9] <= '9' && rows[i].Length > 10 && rows[i][10] >= '0' && rows[i][10] <= '9')
                            rows[i] = "    iOS: " + versionCode;
                        if (rows[i].StartsWith("  AndroidBundleVersionCode: "))
                            rows[i] = "  AndroidBundleVersionCode: " + versionCode;
                        if (rows[i].StartsWith("  productName: "))
                            rows[i] = "  productName: " + Quotify(cols[(int) ARGameList.PRODUCT_NAME]);
                        if (rows[i].StartsWith("    iOS: ") && rows[i][9] >= 'a' && rows[i][9] <= 'z')
                            rows[i] = "    iOS: " + cols[(int) ARGameList.IOS_ID];
                        if (rows[i].StartsWith("    Android: ") && rows[i][13] >= 'a' && rows[i][13] <= 'z')
                            rows[i] = "    Android: " + cols[(int) ARGameList.ANDROID_ID];
                        if (rows[i].StartsWith("  cloudProjectId: "))
                            rows[i] = "  cloudProjectId: " + cols[(int) ARGameList.CLOUD_PROJECT_ID];
                        if (rows[i].StartsWith("  projectName: "))
                            rows[i] = "  projectName: " + Quotify(cols[(int) ARGameList.CLOUD_PROJECT_NAME]);
                        if (rows[i].StartsWith("  AndroidKeystoreName: "))
                        {
                            if (DeviceInput.GameName() == "_SampleGame")
                                rows[i] = "  AndroidKeystoreName: ";
                            else
                                rows[i] = "  AndroidKeystoreName: " + DeviceInput.GameName() + ".keystore";
                        }
                        if (rows[i].StartsWith("  AndroidKeyaliasName: "))
                        {
                            if (DeviceInput.GameName() == "_SampleGame")
                                rows[i] = "  AndroidKeyaliasName: ";
                            else
                                rows[i] = "  AndroidKeyaliasName: " + Quotify(cols[(int) ARGameList.ANDROID_KEYALIAS_NAME]);
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
                            rows[i] = "    4: VUFORIA_IOS_SETTINGS;MAGIS_" + DeviceInput.GameName() + (int.Parse(cols[(int) ARGameList.GPS_SUPPORT]) == 0 ? ";MAGIS_NOGPS" : "");
                        if (rows[i].StartsWith("    7: VUFORIA_ANDROID_SETTINGS"))
                            rows[i] = "    7: VUFORIA_ANDROID_SETTINGS;MAGIS_" + DeviceInput.GameName() + (int.Parse(cols[(int) ARGameList.GPS_SUPPORT]) == 0 ? ";MAGIS_NOGPS" : "");
                    }
                    if (rows[rows.Length - 1] == "")
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
