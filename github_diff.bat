@echo off
cls
diff --exclude=.git --exclude=*.csproj --exclude=*.sln --exclude=*.apk --exclude=*.obb --exclude=Library --exclude=obj --exclude=Temp --exclude=QCAR --exclude=ARGameList.txt* --exclude=Igpaw* --exclude=TheMindMuseumARventure* --exclude=Tuklas* --exclude=BluetoothDeviceScript.cs* --exclude=BluetoothHardwareInterface.cs* --exclude=unityandroidbluetoothlelib* ..\magis . -ur %*
