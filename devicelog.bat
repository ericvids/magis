@echo off
start cmd /k adb logcat -s Unity ActivityManager PackageManager dalvikvm DEBUG
