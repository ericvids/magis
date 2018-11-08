MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.


Setup Instructions
==================

1.  Install Unity 2018.2.14 (latest version as of this writing).  When prompted, add iOS, Android and Vuforia support.

2.  Open the arengine/ folder in Unity via the launcher (Projects->Open) or the menu (File->Open Project).

3.  Go to the Asset Store (Ctrl+9) and add the "Google Play OBB Downloader" to your project.  (You will need to create a Unity ID at id.unity.com.)  _This step is essential for Android but may be skipped for iOS._

4.  Go to https://library.vuforia.com/content/vuforia-library/en/articles/Solution/arcore-with-vuforia.html and follow the instructions to download the Google ARCore .aar file for Unity, and to add it to your project under Assets/Plugins/Android.  _This step is essential for Android but may be skipped for iOS.  **Do not forget this step or your APK submission will be rejected by Google Play.**_

5.  On the menu, select GameObject->Vuforia->AR Camera.  This action will import the necessary Vuforia resources.  **Do not forget this step or your device's camera will not work.**

  *  There is no need to save the current (temporary) scene with the newly-created AR Camera.  If you want, just after the Vuforia import, select File->New Scene and click "Don't Save" to forget this temporary scene.

6.  On the menu, select Window->Vuforia Configuration.  On the Inspector pane, add a valid App License Key.  (You will need to create a Vuforia developer account at developer.vuforia.com, then in the License Manager, click Get Development Key.)  **Do not forget this step or your device's camera will not work.**

7.  Click the Play button to play the sample game within the Unity Editor.

  *  Use the mouse to perform touchscreen commands, and use the arrow keys on your keyboard to navigate within an AR scene.
  *  If you connect a webcam to your computer, you may use it to simulate AR scenes in conjunction with printouts of the sample marker images at arengine/Assets/Editor/Vuforia/ImageTargetTextures/magis-default/.  You will need to go to Vuforia Configuration and uncheck "Disable Vuforia Play Mode" near the bottom of the Inspector.
  *  You may also connect a device with Unity Remote 5 installed to use its gyroscope and touchscreen.  Go to Edit->Project Settings->Editor and under Unity Remote->Device, select "Any Android Device" or "Any iOS Device" depending on your device.  Note that due to Vuforia limitations, it is not possible to use the device's back-facing camera for AR tracking in Play Mode, but you may attach your webcam to the back of your device to simulate it.

8.  All assets of the sample game are found in arengine/Assets/ARGames/_SampleGame/ and arengine/Assets/ARPrefabs/_SampleGame/.  Feel free to modify these to your own liking.

9.  Before deploying to a device or publishing to the Play Store or App Store, make sure to do the following:

  *  Acquire all the necessary tools for publishing on your selected platform.  For Android, see https://docs.unity3d.com/Manual/android-sdksetup.html (only the JDK and Android SDK are needed; you may skip the NDK).  For iOS, see https://docs.unity3d.com/Manual/iphone-GettingStarted.html (a macOS system with Xcode 10 or above and an iOS developer account is required).
  *  In Unity's File->Build Settings, turn off Development Build (unless you want this intentionally, e.g., to build a map of the GPS locations of your real-world markers).
  *  In Unity's File->Build Settings->Player Settings, change your Company Name and Product Name to your own preferred names.
  *  In Unity's File->Build Settings->Player Settings->Settings for (Android|iOS)->Other Settings->Identification, change the Package Name (Android) or Bundle Identifier (iOS) to your own preferred id.  _**Do not use edu.ateneo.magis or any name prefixed with edu.ateneo.magis.*; these are reserved for Ateneo's own apps.**_
  *  Rename the arengine/Assets/ARGames/_SampleGame/ folder to your chosen Product Name, but remove any spaces and non-alphanumeric characters (except for underscore).  For example, the Product Name "Igpaw: Loyola 2" requires a folder name of "IgpawLoyola2".
  *  If you change AppIcon.png or LoadingScreen.png, make sure to also update the Default Icon and Settings for Android->Splash Image->Static Splash Image in Player Settings.  (These fields should normally auto-update correctly if you don't change the associated AppIcon.png.meta and LoadingScreen.png.meta files, but double-check anyway.)
  *  If you want to collect your own analytics, close the Unity Editor and edit arengine/ProjectSettings/ProjectSettings.asset in a text editor.  Change the cloudProjectId to your own id.  To generate your own id, create a new project at developer.cloud.unity3d.com, copy its 36-character UPID, and replace the cloudProjectId in ProjectSettings.asset (which is b76c8b2c-b435-4682-b68e-81d5d538c558 by default) with the UPID you just copied.

10.  Some general deployment tips:

  *  **iOS:**  Unity outputs an Xcode project as the result of a build.  After opening this Xcode project, ensure that you have added your iOS developer account's signing credentials to the project before attempting to run it on a device (Play button) or deploying to the App Store (Product->Archive).  First add your developer account in Xcode->Preferences->Accounts, then in the Project Manager on the left pane (Cmd+1), select the Xcode project name at the top of the tree (usually Unity-iPhone), then change the empty Team field to your development account's team.
  *  **iOS:**  As of this writing, the Xcode Product->Archive command takes up a lot of RAM, and 8 GB RAM is recommended if running Xcode 10 or above.  If on a 4 GB RAM system, your OS may become unreponsive and may unexpectedly reboot or shutdown if there are any other applications in the background (including Unity).  Close all other applications before attempting to build the archive.
  *  **Android:**  Unlike regular APKs, MAGIS's APKs require Google's OBB expansion file mechanism in order to accommodate large game assets.  An install.bat batch file is included for your convenience to automate the installation of these APK files to your own devices; it also installs the APK's accompanying OBB file to the correct location on your device.  When building the project in Unity, save the resulting APK file in the default location when prompted (i.e., inside arengine/) in order to use install.bat successfully.  Only the first APK found in arengine/ will be installed.  The OBB file also needs to be uploaded along with the APK when deploying to the Play Store.
  *  **Android:**  The APK needs to be signed in order to deploy to the Play Store.  Unity handles this via Build Settings->Player Settings->Publishing Settings.  Create a keystore first (you will be prompted for a password), then create a new key alias-password pair for your app (which also has its own password).  Use this keystore file for every succeeding non-debug build -- store it in a secure place and don't forget the passwords.
