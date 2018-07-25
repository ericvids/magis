@echo off
if "%ANDROID_HOME%"=="" (
    echo Please set the ANDROID_HOME environment variable to your Android SDK path, e.g., C:\Android\android-sdk
    goto :end
)
if "%JAVA_HOME%"=="" (
    echo Please set the JAVA_HOME environment variable to your JDK path, e.g., C:\Program Files\Java\jdk1.8.x_xxx
    goto :end
)

for %%f in (arengine\*.apk) do (
    for /f "tokens=2 delims= " %%b in ('findstr /r /c:"Android: [^0-9]" arengine\ProjectSettings\ProjectSettings.asset') do (
        for /f "tokens=2 delims= " %%o in ('findstr "AndroidBundleVersionCode:" arengine\ProjectSettings\ProjectSettings.asset') do (
            echo Installing %%b from %%~dpnf.apk with obb version %%o...
            "%ANDROID_HOME%\platform-tools\adb" uninstall %%b
            "%ANDROID_HOME%\platform-tools\adb" shell rm /sdcard/Android/obb/%%b/*
            "%ANDROID_HOME%\platform-tools\adb" install -r "%%~dpnf.apk"
            "%ANDROID_HOME%\platform-tools\adb" push "%%~dpnf.main.obb" /sdcard/main.%%o.%%b.obb"
            rem DO NOT PUSH FILES DIRECTLY TO AN ANDROID OBB DIRECTORY. Samsung Galaxy Tab S has a problem with this.
            "%ANDROID_HOME%\platform-tools\adb" shell mkdir /sdcard/Android/obb/%%b
            "%ANDROID_HOME%\platform-tools\adb" shell mv /sdcard/main.%%o.%%b.obb /sdcard/Android/obb/%%b/
        )
    )
)

:end
pause
