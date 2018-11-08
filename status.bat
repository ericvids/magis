@echo off
git status --ignored
echo.
echo Skipped files:
git ls-files -v | grep "^[^H]"
echo.
pause
