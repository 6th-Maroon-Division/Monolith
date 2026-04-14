@echo off
setlocal

set "REMOTE_NAME=upstream"
set "UPSTREAM_URL=git@github.com:Monolith-Station/Monolith.git"

git rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo error: run this script inside a git repository
    exit /b 1
)

for /f "delims=" %%i in ('git rev-parse --show-toplevel') do set "REPO_ROOT=%%i"
cd /d "%REPO_ROOT%"

git remote get-url "%REMOTE_NAME%" >nul 2>&1
if errorlevel 1 (
    echo Adding remote %REMOTE_NAME% -^> %UPSTREAM_URL%
    git remote add "%REMOTE_NAME%" "%UPSTREAM_URL%"
) else (
    for /f "delims=" %%i in ('git remote get-url "%REMOTE_NAME%"') do set "CURRENT_URL=%%i"
    if /I not "%CURRENT_URL%"=="%UPSTREAM_URL%" (
        echo Updating %REMOTE_NAME% URL
        echo   from: %CURRENT_URL%
        echo     to: %UPSTREAM_URL%
        git remote set-url "%REMOTE_NAME%" "%UPSTREAM_URL%"
    ) else (
        echo Remote %REMOTE_NAME% already configured
    )
)

echo Done. Current remotes:
git remote -v

exit /b 0