# InstallGlassBarWithErrorSuppression.ps1 - Install GlassBar and automatically dismiss error dialogs
param(
    [string]$InstallerPath,
    [string]$LogPath = "$env:TEMP\ClearGlass_GlassBar_Complete.log"
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] $Message"
    Write-Host $logMessage
    Add-Content -Path $LogPath -Value $logMessage -ErrorAction SilentlyContinue
}

# Function to automatically close GlassBar error dialogs
function Start-ErrorDialogKiller {
    $job = Start-Job -ScriptBlock {
        param($logPath)
        
        function Write-JobLog {
            param([string]$Message)
            $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            $logMessage = "[$timestamp] [ErrorKiller] $Message"
            Add-Content -Path $logPath -Value $logMessage -ErrorAction SilentlyContinue
        }
        
        Write-JobLog "Error dialog killer started"
        
        # Load Windows API functions
        Add-Type @"
            using System;
            using System.Runtime.InteropServices;
            using System.Text;
            
            public class Win32 {
                [DllImport("user32.dll", CharSet = CharSet.Auto)]
                public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
                
                [DllImport("user32.dll")]
                public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
                
                [DllImport("user32.dll")]
                public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
                
                [DllImport("user32.dll", CharSet = CharSet.Auto)]
                public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
                
                [DllImport("user32.dll")]
                public static extern bool IsWindowVisible(IntPtr hWnd);
                
                public const uint WM_CLOSE = 0x0010;
                public const uint WM_COMMAND = 0x0111;
                public const uint BN_CLICKED = 0;
            }
"@
        
        $startTime = Get-Date
        $maxRunTime = [TimeSpan]::FromMinutes(3) # Run for max 3 minutes
        
        while ((Get-Date) - $startTime -lt $maxRunTime) {
            try {
                # Look for GlassBar error windows
                $errorWindow = [Win32]::FindWindow($null, "GlassBar 1.0.0.1 - Error")
                
                if ($errorWindow -ne [IntPtr]::Zero -and [Win32]::IsWindowVisible($errorWindow)) {
                    Write-JobLog "Found GlassBar error dialog, attempting to close..."
                    
                    # Try to find and click OK button
                    $okButton = [Win32]::FindWindowEx($errorWindow, [IntPtr]::Zero, "Button", "OK")
                    if ($okButton -ne [IntPtr]::Zero) {
                        Write-JobLog "Clicking OK button"
                        [Win32]::PostMessage($okButton, [Win32]::WM_COMMAND, [IntPtr]::Zero, [IntPtr]::Zero)
                    } else {
                        Write-JobLog "OK button not found, sending WM_CLOSE"
                        [Win32]::PostMessage($errorWindow, [Win32]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
                    }
                    
                    Start-Sleep -Milliseconds 100
                }
                
                # Also look for any window with "XAML" or "Fatal error" in the title
                $processes = Get-Process | Where-Object { $_.MainWindowTitle -like "*Error*" -or $_.MainWindowTitle -like "*XAML*" -or $_.MainWindowTitle -like "*Fatal*" }
                foreach ($proc in $processes) {
                    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
                        Write-JobLog "Found potential error window: $($proc.MainWindowTitle)"
                        [Win32]::PostMessage($proc.MainWindowHandle, [Win32]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
                    }
                }
                
                Start-Sleep -Milliseconds 200
            }
            catch {
                Write-JobLog "Error in dialog killer: $($_.Exception.Message)"
                Start-Sleep -Milliseconds 500
            }
        }
        
        Write-JobLog "Error dialog killer finished"
    } -ArgumentList $LogPath
    
    return $job
}

Write-Log "Starting GlassBar installation with error suppression..."
Write-Log "Installer path: $InstallerPath"

if (-not (Test-Path $InstallerPath)) {
    Write-Log "ERROR: Installer not found at $InstallerPath"
    exit 1
}

# Start the error dialog killer job
Write-Log "Starting error dialog killer..."
$errorKillerJob = Start-ErrorDialogKiller

# Kill any existing GlassBar processes
Write-Log "Killing any existing GlassBar processes..."
try {
    Get-Process -Name "GlassBar*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Log "Existing GlassBar processes terminated"
}
catch {
    Write-Log "Note: No existing GlassBar processes found"
}

# Install GlassBar
Write-Log "Starting GlassBar installation..."
try {
    $process = Start-Process -FilePath $InstallerPath -ArgumentList "/S" -Wait -PassThru -NoNewWindow -ErrorAction Stop
    Write-Log "Installer completed with exit code: $($process.ExitCode)"
    $installSuccess = ($process.ExitCode -eq 0)
}
catch {
    Write-Log "Error during installation: $($_.Exception.Message)"
    $installSuccess = $false
}

if (-not $installSuccess) {
    Write-Log "ERROR: Installation failed"
    Stop-Job $errorKillerJob -ErrorAction SilentlyContinue
    Remove-Job $errorKillerJob -ErrorAction SilentlyContinue
    exit 1
}

# Wait a bit for any auto-launched processes and let the error killer handle them
Write-Log "Waiting for any auto-launched processes..."
Start-Sleep -Seconds 5

# Kill any problematic auto-launched processes
Write-Log "Cleaning up any problematic auto-launched processes..."
for ($i = 0; $i -lt 3; $i++) {
    try {
        $processes = Get-Process -Name "GlassBar*" -ErrorAction SilentlyContinue
        if ($processes) {
            Write-Log "Found $($processes.Count) GlassBar process(es), terminating..."
            $processes | Stop-Process -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Seconds 1
    }
    catch {
        Write-Log "Error cleaning up processes: $($_.Exception.Message)"
    }
}

# Stop the error dialog killer
Write-Log "Stopping error dialog killer..."
Stop-Job $errorKillerJob -ErrorAction SilentlyContinue
Remove-Job $errorKillerJob -ErrorAction SilentlyContinue

# Now find and start GlassBar properly
Write-Log "Finding GlassBar installation..."

$possiblePaths = @(
    "$env:ProgramFiles\GlassBar\GlassBar.exe",
    "${env:ProgramFiles(x86)}\GlassBar\GlassBar.exe",
    "$env:LOCALAPPDATA\GlassBar\GlassBar.exe",
    "$env:APPDATA\GlassBar\GlassBar.exe",
    "$env:USERPROFILE\AppData\Local\GlassBar\GlassBar.exe",
    "$env:USERPROFILE\AppData\Roaming\GlassBar\GlassBar.exe"
)

$glassBarPath = $null
foreach ($path in $possiblePaths) {
    Write-Log "Checking path: $path"
    if (Test-Path $path) {
        $glassBarPath = $path
        Write-Log "Found GlassBar at: $path"
        break
    }
}

if (-not $glassBarPath) {
    Write-Log "ERROR: GlassBar executable not found after installation!"
    exit 1
}

# Wait a bit more before starting GlassBar
Write-Log "Waiting before starting GlassBar..."
Start-Sleep -Seconds 3

# Start GlassBar properly in user context to avoid XAML Diagnostics errors
Write-Log "Starting GlassBar from: $glassBarPath"

# First, let's try starting it as the current user (non-elevated) to avoid XAML diagnostics issues
try {
    Write-Log "Attempting to start GlassBar in user context..."
    
    # Method 1: Use shell execute without elevation
    $process = Start-Process -FilePath $glassBarPath -WorkingDirectory (Split-Path $glassBarPath) -UseShellExecute $true -PassThru -ErrorAction Stop
    Write-Log "GlassBar started in user context (PID: $($process.Id))"
    
    Start-Sleep -Seconds 5  # Give it more time to initialize
    
    if (-not $process.HasExited) {
        Write-Log "GlassBar is running successfully in user context!"
        exit 0
    } else {
        Write-Log "WARNING: GlassBar process exited with code: $($process.ExitCode)"
    }
}
catch {
    Write-Log "Error starting GlassBar in user context: $($_.Exception.Message)"
}

# Method 2: Try using explorer.exe to launch it (this ensures user context)
try {
    Write-Log "Trying to start GlassBar via explorer.exe..."
    Start-Process -FilePath "explorer.exe" -ArgumentList $glassBarPath -ErrorAction Stop
    Start-Sleep -Seconds 5
    
    $runningProcesses = Get-Process -Name "GlassBar" -ErrorAction SilentlyContinue
    if ($runningProcesses) {
        Write-Log "GlassBar started successfully via explorer.exe!"
        exit 0
    }
}
catch {
    Write-Log "Error starting GlassBar via explorer: $($_.Exception.Message)"
}

# Method 3: Try direct invoke
try {
    Write-Log "Trying direct invoke method..."
    Invoke-Item $glassBarPath
    Start-Sleep -Seconds 5
    
    $runningProcesses = Get-Process -Name "GlassBar" -ErrorAction SilentlyContinue
    if ($runningProcesses) {
        Write-Log "GlassBar started successfully using invoke method!"
        exit 0
    }
}
catch {
    Write-Log "Error with invoke method: $($_.Exception.Message)"
}

Write-Log "ERROR: Failed to start GlassBar properly"
exit 1

