@echo off
if "%ANDROID_HOME%"=="" (
    echo Please set the ANDROID_HOME environment variable to your Android SDK path, e.g., C:\Android\android-sdk
    goto :end
)

start cmd /k "%ANDROID_HOME%\platform-tools\adb" logcat -s Unity ActivityManager PackageManager dalvikvm DEBUG

:end