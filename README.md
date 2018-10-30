MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.


Setup Instructions
==================

1.  Install Unity 2018.2.14.  When prompted, add iOS, Android and Vuforia support.

2.  Open the arengine/ folder in Unity via the launcher (Projects->Open) or the menu (File->Open Project).

3.  Go to the Asset Store (Ctrl+9) and add the "Google Play OBB Downloader" to your project.  (You will need to create a Unity ID at id.unity.com.)

4.  In the menu, select Window->Vuforia Configuration.  Add a valid App License Key.  (You will need to create a Vuforia developer account at developer.vuforia.com, then in the License Manager, click Get Development Key.)

5.  In the menu, select GameObject->Vuforia->AR Camera.  This action will import the necessary Vuforia resources.  (There is no need to save the current (temporary) scene with the newly-created AR Camera.  If you want, just after the Vuforia import, select File->New Scene and click Don't Save to forget this temporary scene.)

6.  Click the Play button to play the sample game within the Unity Editor.

7.  Feel free to modify arengine/Assets/ARGames/_SampleGame to your own liking.

8.  Before deploying to a device or publishing to the Play Store or App Store, make sure to do the following:

  *  In Unity's File->Build Settings, turn off Development Build (unless you are debugging intentionally).
  *  In Unity's File->Build Settings->Player Settings, change your Company Name and Product Name to your own preferred names.
  *  In Unity's File->Build Settings->Player Settings->Settings for (Android|iOS)->Other Settings->Identification, change the Package Name (Android) or Bundle Identifier (iOS) to your own preferred id.  _**Do not use edu.ateneo.magis or any name prefixed with edu.ateneo.magis.*; these are reserved for Ateneo's own apps.**_
  *  Rename the arengine/Assets/ARGames/_SampleGame folder to your chosen Product Name, but remove any spaces and non-alphanumeric characters (except for underscore).  For example, the Product Name "Igpaw: Loyola 2" requires a folder name of "IgpawLoyola2".
  *  If you change AppIcon.png or LoadingScreen.png, make sure to also update the Default Icon and Settings for Android->Splash Image->Static Splash Image in Player Settings.  (These fields should normally auto-update correctly if you don't change the associated AppIcon.png.meta and LoadingScreen.png.meta files, but double-check anyway.)
  *  If you want to collect your own analytics, close the Unity Editor and edit arengine/ProjectSettings/ProjectSettings.asset in a text editor.  Change the cloudProjectId to your own id.  To generate your own id, create a new project at developer.cloud.unity3d.com, copy its 36-character UPID, and replace the cloudProjectId in ProjectSettings.asset (which is b76c8b2c-b435-4682-b68e-81d5d538c558 by default) with the UPID you just copied.
