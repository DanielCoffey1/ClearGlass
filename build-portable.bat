@echo off
echo Building ClearGlass Portable Version...
echo.

REM Clean previous builds
echo Cleaning previous builds...
dotnet clean --configuration Release

REM Build the main ClearGlass application
echo Building main ClearGlass application...
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output "bin\Release\portable" /p:PublishSingleFile=true /p:PublishTrimmed=false /p:EnableCompressionInSingleFile=true

if %ERRORLEVEL% neq 0 (
    echo Error building main application!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build completed successfully!
echo.
echo Portable executable location:
echo bin\Release\portable\ClearGlass.exe
echo.
echo The portable version includes:
echo - All ClearGlass optimizations
echo - Windows AI removal script
echo - Self-destruct functionality
echo ========================================
echo.
pause 