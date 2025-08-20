# InstallGlassBarSilently.ps1 - Install and launch GlassBar with complete error suppression
param(
    [string]$InstallerPath,
    [string]$LogPath = "$env:TEMP\ClearGlass_GlassBar_Install.log"
)

function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] $Message"
    Write-Host $logMessage
    Add-Content -Path $LogPath -Value $logMessage -ErrorAction SilentlyContinue
}

Write-Log "Starting GlassBar silent installation..."
Write-Log "Installer path: $InstallerPath"

if (-not (Test-Path $InstallerPath)) {
    Write-Log "ERROR: Installer not found at $InstallerPath"
    exit 1
}

# Kill any existing GlassBar processes before installation
Write-Log "Killing any existing GlassBar processes..."
try {
    Get-Process -Name "GlassBar*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Log "Existing GlassBar processes terminated"
}
catch {
    Write-Log "Note: No existing GlassBar processes found"
}

# Try different silent installation flags
$silentFlags = @(
    "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /NOCANCEL",
    "/S /NOCANCEL",
    "/silent",
    "/quiet",
    "/q"
)

$installSuccess = $false

foreach ($flags in $silentFlags) {
    Write-Log "Trying installation with flags: $flags"
    
    try {
        # Start the installer with current flags
        $process = Start-Process -FilePath $InstallerPath -ArgumentList $flags -Wait -PassThru -NoNewWindow -ErrorAction Stop
        
        Write-Log "Installer completed with exit code: $($process.ExitCode)"
        
        if ($process.ExitCode -eq 0) {
            Write-Log "Installation successful with flags: $flags"
            $installSuccess = $true
            break
        }
        else {
            Write-Log "Installation failed with flags: $flags (exit code: $($process.ExitCode))"
        }
    }
    catch {
        Write-Log "Error with flags '$flags': $($_.Exception.Message)"
    }
    
    # Wait between attempts
    Start-Sleep -Seconds 2
}

if (-not $installSuccess) {
    Write-Log "ERROR: All installation attempts failed"
    exit 1
}

# Wait a moment after installation
Start-Sleep -Seconds 3

# Kill any auto-launched GlassBar processes that might have errors
Write-Log "Killing any auto-launched GlassBar processes..."
for ($i = 0; $i -lt 5; $i++) {
    try {
        $processes = Get-Process -Name "GlassBar*" -ErrorAction SilentlyContinue
        if ($processes) {
            Write-Log "Found $($processes.Count) GlassBar process(es), terminating..."
            $processes | Stop-Process -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Milliseconds 500
    }
    catch {
        Write-Log "Error killing auto-launched processes: $($_.Exception.Message)"
    }
}

Write-Log "Installation phase completed. Processes cleaned up."

# Now find and start GlassBar properly
Write-Log "Starting GlassBar launch phase..."

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
    Write-Log "ERROR: GlassBar executable not found after installation!"
    exit 1
}

# Wait a bit more to ensure installation is completely finished
Start-Sleep -Seconds 5

# Start GlassBar using multiple methods
Write-Log "Starting GlassBar from: $glassBarPath"

try {
    # Method 1: Start as a normal user process
    $process = Start-Process -FilePath $glassBarPath -WorkingDirectory (Split-Path $glassBarPath) -PassThru -ErrorAction Stop
    Write-Log "GlassBar started successfully (PID: $($process.Id))"
    
    # Wait to see if it stays running
    Start-Sleep -Seconds 3
    
    if (-not $process.HasExited) {
        Write-Log "GlassBar is running successfully!"
        exit 0
    } else {
        Write-Log "WARNING: GlassBar process exited with code: $($process.ExitCode)"
    }
}
catch {
    Write-Log "Method 1 failed: $($_.Exception.Message)"
}

# Method 2: Shell execute
try {
    Write-Log "Trying shell execute method..."
    Invoke-Item $glassBarPath
    Start-Sleep -Seconds 3
    
    $runningProcesses = Get-Process -Name "GlassBar" -ErrorAction SilentlyContinue
    if ($runningProcesses) {
        Write-Log "GlassBar started successfully using shell execute!"
        exit 0
    }
}
catch {
    Write-Log "Method 2 failed: $($_.Exception.Message)"
}

# Method 3: Direct execution
try {
    Write-Log "Trying direct execution method..."
    & $glassBarPath
    Start-Sleep -Seconds 3
    
    $runningProcesses = Get-Process -Name "GlassBar" -ErrorAction SilentlyContinue
    if ($runningProcesses) {
        Write-Log "GlassBar started successfully using direct execution!"
        exit 0
    }
}
catch {
    Write-Log "Method 3 failed: $($_.Exception.Message)"
}

Write-Log "ERROR: All methods failed to start GlassBar"
exit 1


