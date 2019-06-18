/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

#if MAGIS_BLE && UNITY_IOS

using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class PostBuild
{
    static void AddToArray(PlistElementDict dict, string arrayName, string element)
    {
        if (dict[arrayName] == null)
            dict.CreateArray(arrayName);
        PlistElementArray a = dict[arrayName].AsArray();
        foreach (PlistElement e in a.values)
        {
            if (e.AsString() == element)
                return;
        }
        a.AddString(element);
    }

    [PostProcessBuild]
    public static void ChangeXcodePlist(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget == BuildTarget.iOS)
        {
            PlistDocument plist = new PlistDocument();
            plist.ReadFromString(File.ReadAllText(pathToBuiltProject + "/Info.plist"));
            AddToArray(plist.root, "UIRequiredDeviceCapabilities", "bluetooth-le");
# if MAGIS_NOGPS
            plist.root.SetString("NSLocationWhenInUseUsageDescription", "This app uses your device's Bluetooth LE hardware to verify your real-world location for displaying Augmented Reality.");
# endif
            plist.root.SetString("NSBluetoothPeripheralUsageDescription", "This app uses your device's Bluetooth LE hardware to verify your real-world location for displaying Augmented Reality.");
            File.WriteAllText(pathToBuiltProject + "/Info.plist", plist.WriteToString());
        }
    }
}

#endif
