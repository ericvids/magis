@echo off
git status --ignored=matching --untracked-files
echo.
echo Skipped files:
git ls-files -v | grep "^[^H]"
echo.
pause
