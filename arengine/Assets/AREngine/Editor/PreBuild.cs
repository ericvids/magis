/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class PreBuild : IPreprocessBuildWithReport
{
    public int callbackOrder
    {
        get
        {
            return 0;
        }
    }

    void ReplaceFile(string oldFile, string newFile)
    {
        BackupFile(oldFile);
        if (AssetDatabase.AssetPathToGUID(newFile) != "")
        {
            Debug.Log("Replacing " + oldFile + " with " + newFile);
            AssetDatabase.CopyAsset(newFile, oldFile);
        }
    }

    void BackupFile(string file)
    {
        if (AssetDatabase.AssetPathToGUID(file) != "")
        {
            Debug.Log("Backing up " + file);
            AssetDatabase.MoveAsset(file, file + ".bak");
        }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (Autorun.buildNotReady || AssetDatabase.IsValidFolder("Assets/_DO NOT COMMIT RIGHT NOW - Unity is using the project"))
        {
            EditorUtility.DisplayDialog("MAGIS", "MAGIS has detected that you are attempting to perform a build while it is not in a ready state. This will lead to a corrupted build.\n\nDue to Unity missing the necessary callback, it is not possible for MAGIS to cancel the build. Please manually cancel it by pressing OK in this dialog and pressing Cancel in the progress window that appears.", "OK");
            return;
        }

        // do this before build

        // if we don't call this before creating the dummy folder, unity will be in a non-compiling state
        // and the dummy folder gets removed prematurely
        AssetDatabase.SaveAssets();

        // upon building, hide unnecessary resources
        AssetDatabase.CreateFolder("Assets", "_DO NOT COMMIT RIGHT NOW - Unity is using the project");

        // to ensure that the only markers included in the built package are that of the current game,
        // we move all other markers to a temporary folder
        if (! Autorun.singleGameProject && ! AssetDatabase.IsValidFolder("Assets/StreamingAssetsBackup"))
        {
            Debug.Log("Backing up StreamingAssets that are not used for this project");
            AssetDatabase.MoveAsset("Assets/StreamingAssets", "Assets/StreamingAssetsBackup");
            AssetDatabase.CreateFolder("Assets", "StreamingAssets");
            AssetDatabase.CreateFolder("Assets/StreamingAssets", "Vuforia");
            AssetDatabase.MoveAsset("Assets/StreamingAssetsBackup/Vuforia/magis-default.dat",
                                    "Assets/StreamingAssets/Vuforia/magis-default.dat");
            AssetDatabase.MoveAsset("Assets/StreamingAssetsBackup/Vuforia/magis-default.xml",
                                    "Assets/StreamingAssets/Vuforia/magis-default.xml");
            AssetDatabase.MoveAsset("Assets/StreamingAssetsBackup/Vuforia/" + DeviceInput.GameName() + ".dat",
                                    "Assets/StreamingAssets/Vuforia/" + DeviceInput.GameName() + ".dat");
            AssetDatabase.MoveAsset("Assets/StreamingAssetsBackup/Vuforia/" + DeviceInput.GameName() + ".xml",
                                    "Assets/StreamingAssets/Vuforia/" + DeviceInput.GameName() + ".xml");
        }

#if MAGIS_NOGPS && ! MAGIS_BLE
        BackupFile("Assets/AREngine/Plugins/iOS/Location.mm");
#endif

#if MAGIS_BLE
        ReplaceFile("Assets/Plugins/Android/AndroidManifest.xml", "Assets/AREngine/Plugins/Android/AndroidManifestBT.xml");
#else
        BackupFile("Assets/Plugins/Android/unityandroidbluetoothlelib.jar");
        BackupFile("Assets/Plugins/iOS/UnityBluetoothLE.mm");
#endif

        AssetDatabase.Refresh();
        Debug.Log("Writing " + report.summary.platform + " build to " + report.summary.outputPath);
    }
}
