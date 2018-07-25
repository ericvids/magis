@echo off
setlocal
set /p SURE=Are you sure you want to reset the repository to its pristine state? (y/[n]) 
if /i "%SURE%" neq "y" goto end
git reset --hard
git clean -dffx .
call status.bat

:end
endlocal