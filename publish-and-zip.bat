@echo off
echo Publishing BenchmarkingWithOtel applications...
powershell -ExecutionPolicy Bypass -File "%~dp0publish-and-zip.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo Error occurred during publishing. Check the output above for details.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Publishing completed successfully!
echo.
echo The following zip files were created in the root directory:
echo  - server-publish.zip
echo  - client-publish.zip
echo  - revproxy-publish.zip
echo.
pause 