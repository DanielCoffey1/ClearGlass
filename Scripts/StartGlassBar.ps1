# StartGlassBar.ps1 - Launch GlassBar after installation
param(
    [string]$LogPath = "$env:TEMP\ClearGlass_GlassBar.log"
)

function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] $Message"
    Write-Host $logMessage
    Add-Content -Path $LogPath -Value $logMessage -ErrorAction SilentlyContinue
}

Write-Log "Starting GlassBar launch script..."

# Common installation paths for GlassBar
$possiblePaths = @(
    "$env:ProgramFiles\GlassBar\GlassBar.exe",
    "${env:ProgramFiles(x86)}\GlassBar\GlassBar.exe",
    "$env:LOCALAPPDATA\GlassBar\GlassBar.exe",
    "$env:APPDATA\GlassBar\GlassBar.exe",
    "$env:USERPROFILE\AppData\Local\GlassBar\GlassBar.exe",
    "$env:USERPROFILE\AppData\Roaming\GlassBar\GlassBar.exe"
)

$glassBarPath = $null

# Find GlassBar executable
foreach ($path in $possiblePaths) {
    Write-Log "Checking path: $path"
    if (Test-Path $path) {
        $glassBarPath = $path
        Write-Log "Found GlassBar at: $path"
        break
    }
}

if (-not $glassBarPath) {
    # Try to find GlassBar in registry
    Write-Log "GlassBar not found in common paths, checking registry..."
    
    $regPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )
    
    foreach ($regPath in $regPaths) {
        try {
            $programs = Get-ItemProperty $regPath -ErrorAction SilentlyContinue
            foreach ($program in $programs) {
                if ($program.DisplayName -like "*GlassBar*") {
                    Write-Log "Found GlassBar in registry: $($program.DisplayName)"
                    if ($program.InstallLocation) {
                        $regGlassBarPath = Join-Path $program.InstallLocation "GlassBar.exe"
                        if (Test-Path $regGlassBarPath) {
                            $glassBarPath = $regGlassBarPath
                            Write-Log "Found GlassBar executable via registry: $regGlassBarPath"
                            break
                        }
                    }
                }
            }
        }
        catch {
            Write-Log "Error checking registry path $regPath : $($_.Exception.Message)"
        }
        if ($glassBarPath) { break }
    }
}

if (-not $glassBarPath) {
    Write-Log "ERROR: GlassBar executable not found!"
    exit 1
}

# Kill any existing GlassBar processes
Write-Log "Killing any existing GlassBar processes..."
try {
    Get-Process -Name "GlassBar" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Log "Existing GlassBar processes terminated"
}
catch {
    Write-Log "Note: No existing GlassBar processes found or error stopping them: $($_.Exception.Message)"
}

# Start GlassBar
Write-Log "Starting GlassBar from: $glassBarPath"

try {
    # Method 1: Start as a normal user process
    $process = Start-Process -FilePath $glassBarPath -WorkingDirectory (Split-Path $glassBarPath) -PassThru -ErrorAction Stop
    Write-Log "GlassBar started successfully (PID: $($process.Id))"
    
    # Wait a bit to see if it stays running
    Start-Sleep -Seconds 3
    
    if (-not $process.HasExited) {
        Write-Log "GlassBar is running successfully!"
        exit 0
    } else {
        Write-Log "WARNING: GlassBar process exited with code: $($process.ExitCode)"
        
        # Try method 2: Start with shell execute
        Write-Log "Trying alternative launch method..."
        Invoke-Item $glassBarPath
        Start-Sleep -Seconds 2
        
        $runningProcesses = Get-Process -Name "GlassBar" -ErrorAction SilentlyContinue
        if ($runningProcesses) {
            Write-Log "GlassBar started successfully using alternative method!"
            exit 0
        } else {
            Write-Log "ERROR: Failed to start GlassBar using alternative method"
            exit 1
        }
    }
}
catch {
    Write-Log "ERROR: Failed to start GlassBar: $($_.Exception.Message)"
    
    # Try method 3: Direct execution
    try {
        Write-Log "Trying direct execution method..."
        & $glassBarPath
        Start-Sleep -Seconds 2
        
        $runningProcesses = Get-Process -Name "GlassBar" -ErrorAction SilentlyContinue
        if ($runningProcesses) {
            Write-Log "GlassBar started successfully using direct execution!"
            exit 0
        } else {
            Write-Log "ERROR: Direct execution failed"
            exit 1
        }
    }
    catch {
        Write-Log "ERROR: All methods failed to start GlassBar: $($_.Exception.Message)"
        exit 1
    }
}


