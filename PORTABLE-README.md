# ClearGlass Portable Version

## Overview

ClearGlass Portable is a self-destructing Windows optimization tool that completely removes itself after running. It's designed for users who want a one-time optimization without leaving any trace of the application on their system.

## Features

### 🚀 Portable Mode
- **Self-Contained**: Single executable with all dependencies included
- **Self-Destructing**: Automatically removes itself after completion
- **Complete Optimization**: Runs all ClearGlass optimizations automatically
- **Windows AI Removal**: Schedules and executes Windows AI component removal
- **Clean Exit**: Leaves no trace of the application or its files

### 🔄 Process Flow
1. **User runs the portable executable**
2. **All optimizations run automatically:**
   - System tweaks (OneDrive removal, privacy settings, etc.)
   - Windows settings optimization
   - Bloatware removal
   - Clear Glass theme application
3. **Windows AI removal script is scheduled for next startup**
4. **PC restarts automatically**
5. **After restart, AI removal script runs silently in background**
6. **PC restarts again to complete cleanup**
7. **ClearGlass removes itself completely**

## Building the Portable Version

### Prerequisites
- .NET 6.0 SDK or later
- Windows 10/11 development environment

### Build Steps
1. **Clone or download the ClearGlass repository**
2. **Open Command Prompt in the project directory**
3. **Run the build script:**
   ```batch
   build-portable.bat
   ```
4. **The portable executable will be created at:**
   ```
   bin\Release\portable\ClearGlass.exe
   ```

### Manual Build (Alternative)
If you prefer to build manually:
```batch
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output "bin\Release\portable" /p:PublishSingleFile=true /p:PublishTrimmed=true /p:TrimMode=link /p:EnableCompressionInSingleFile=true
```

## Usage

### ⚠️ Important Warnings
- **This process cannot be interrupted once started**
- **Your PC will restart automatically**
- **The application will be completely removed after completion**
- **Make sure to save any open work before running**
- **A full-screen progress window will appear during AI removal - do not close it**

### Running the Portable Version
1. **Right-click the ClearGlass.exe file**
2. **Select "Run as administrator"**
3. **Click the "🚀 RUN CLEAR GLASS 🚀" button**
4. **Wait for the process to complete (no pop-ups)**
5. **Your PC will restart automatically**

### What Happens During Execution
1. **Optimization Phase:**
   - System tweaks are applied (OneDrive removal, privacy settings, etc.)
   - Windows settings are optimized for performance and privacy
   - Unnecessary bloatware is removed
   - Clear Glass theme is applied

2. **Scheduling Phase:**
   - Windows AI removal script is extracted to temporary location
   - Startup task is created to run the script after restart
   - Self-destruct batch file is created and scheduled

3. **Restart Phase:**
   - PC restarts automatically
   - AI removal script runs at startup **silently in the background**
   - Windows AI components are completely removed
   - PC restarts again to finalize cleanup
   - ClearGlass removes itself and all temporary files

## Technical Details

### Self-Destruction Mechanism
- Uses Windows Task Scheduler to create startup tasks
- Extracts embedded PowerShell scripts to temporary locations
- Creates batch files that clean up after execution
- Removes all temporary files and scheduled tasks
- Deletes the application executable and directory

### Silent Operation
- **Background execution** - no user interface during AI removal
- **Console output logging** for troubleshooting if needed
- **Automatic completion** without user intervention
- **Clean restart process** to finalize all changes

### Windows AI Removal
- Kills AI-related processes
- Removes AppX packages for AI components
- Disables registry keys and policies
- Cleans up files and scheduled tasks
- Removes Windows AI Machine Learning DLLs
- Disables AI features in applications (Paint, Notepad, etc.)

### System Requirements
- Windows 10 (version 1903 or later) or Windows 11
- Administrator privileges
- .NET 6.0 Runtime (included in self-contained build)
- At least 2GB free disk space
- Internet connection (for initial optimization)

## Troubleshooting

### Common Issues
1. **"Access Denied" errors:**
   - Make sure to run as administrator
   - Disable antivirus temporarily if needed

2. **Build fails:**
   - Ensure .NET 6.0 SDK is installed
   - Check that all dependencies are restored

3. **Process gets stuck:**
   - Wait at least 10-15 minutes before interrupting
   - Check Task Manager for running processes
   - Restart manually if necessary

### Recovery
If the process fails or gets interrupted:
1. **Restart your PC manually**
2. **Check if ClearGlass is still present**
3. **If present, you can run it again**
4. **If not present, the process may have completed partially**

## Security Notes

- The application requires administrator privileges to function properly
- All operations are logged and can be reviewed
- No data is sent to external servers
- The application is completely self-contained
- All temporary files are cleaned up automatically

## Support

For issues or questions:
1. Check the main ClearGlass README.md for general information
2. Review the troubleshooting section above
3. Check Windows Event Viewer for system logs
4. Ensure all prerequisites are met

---

**Note**: This portable version is designed for one-time use. After completion, you'll need to download a new copy if you want to run ClearGlass again. 