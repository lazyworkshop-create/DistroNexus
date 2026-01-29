@echo off
echo Testing new DistroNexus startup flow...
echo.
echo Expected behavior:
echo 1. Main window should appear immediately (within 1-2 seconds)
echo 2. Loading overlay should be visible with status message
echo 3. Toolbar should be hidden during loading
echo 4. After 10-15 seconds, loading should complete and data should appear
echo.

echo Starting DistroNexus...
start "" "D:/wsl/DistroNexus/src/Client/DistroNexus.Desktop/bin/Debug/net10.0-windows/DistroNexus.Desktop.exe"

echo.
echo Application launched. Check for the expected behavior above.
echo Press any key to continue...
pause > nul