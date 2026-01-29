@echo off
echo Starting DistroNexus application...
echo Current directory: %CD%
echo.

echo Looking for executable...
dir /b "bin\Release\net10.0-windows\DistroNexus.Desktop.exe" 2>nul
if %ERRORLEVEL% neq 0 (
    echo Executable not found, trying debug build...
    dir /b "bin\Debug\net10.0-windows\DistroNexus.Desktop.exe" 2>nul
    if %ERRORLEVEL% neq 0 (
        echo ERROR: Executable not found!
        pause
        exit /b 1
    )
    echo Running debug build...
    "bin\Debug\net10.0-windows\DistroNexus.Desktop.exe"
) else (
    echo Running release build...
    "bin\Release\net10.0-windows\DistroNexus.Desktop.exe"
)

echo.
echo Application exited with code: %ERRORLEVEL%
pause