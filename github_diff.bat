@echo off
cls
diff --exclude=.git --exclude=*.csproj --exclude=*.sln --exclude=*.apk --exclude=*.obb --exclude=Library --exclude=obj --exclude=Temp --exclude=QCAR --exclude=ARGameList.txt --exclude=ARGameList.txt.meta --exclude=magis.keystore --exclude=Igpaw* ..\magis ..\magis.igpaw -ur %*
