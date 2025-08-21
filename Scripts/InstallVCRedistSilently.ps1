# InstallVCRedistSilently.ps1 - Install Visual C++ Redistributable silently before GlassBar
param(
    [string]$InstallerPath,
    [string]$LogPath = "$env:TEMP\ClearGlass_VCRedist_Install.log"
)

function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] $Message"
    Write-Host $logMessage
    Add-Content -Path $LogPath -Value $logMessage -ErrorAction SilentlyContinue
}

Write-Log "Starting Visual C++ Redistributable silent installation..."
Write-Log "Installer path: $InstallerPath"

if (-not (Test-Path $InstallerPath)) {
    Write-Log "ERROR: VC_redist.x64.exe not found at $InstallerPath"
    exit 1
}

# Check if VC++ Redistributable is already installed
Write-Log "Checking if Visual C++ Redistributable is already installed..."

$vcRedistInstalled = $false
try {
    # Check for common VC++ Redistributable registry entries
    $regPaths = @(
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\12.0\VC\Runtimes\x64",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\12.0\VC\Runtimes\x64"
    )
    
    foreach ($regPath in $regPaths) {
        if (Test-Path $regPath) {
            $installed = Get-ItemProperty -Path $regPath -Name "Installed" -ErrorAction SilentlyContinue
            if ($installed -and $installed.Installed -eq 1) {
                Write-Log "Visual C++ Redistributable already installed (found in registry: $regPath)"
                $vcRedistInstalled = $true
                break
            }
        }
    }
    
    # Also check for installed programs
    if (-not $vcRedistInstalled) {
        try {
            $installedPrograms = Get-WmiObject -Class Win32_Product | Where-Object { 
                $_.Name -like "*Visual C++*Redistributable*" -or 
                $_.Name -like "*Microsoft Visual C++*" 
            }
            
            if ($installedPrograms) {
                Write-Log "Found installed Visual C++ Redistributable packages:"
                foreach ($program in $installedPrograms) {
                    Write-Log "  - $($program.Name) (Version: $($program.Version))"
                }
                $vcRedistInstalled = $true
            }
        }
        catch {
            Write-Log "Warning: Error checking installed programs: $($_.Exception.Message)"
        }
    }
}
catch {
    Write-Log "Warning: Error checking existing installation: $($_.Exception.Message)"
}

if ($vcRedistInstalled) {
    Write-Log "Visual C++ Redistributable is already installed. Skipping installation."
    exit 0
}

Write-Log "Visual C++ Redistributable not found. Proceeding with installation..."

# Try different silent installation flags for VC++ Redistributable
$silentFlags = @(
    "/quiet /norestart",
    "/passive /norestart",
    "/q /norestart",
    "/silent /norestart"
)

$installSuccess = $false

foreach ($flags in $silentFlags) {
    Write-Log "Trying installation with flags: $flags"
    
    try {
        # Start the installer with current flags
        $process = Start-Process -FilePath $InstallerPath -ArgumentList $flags -Wait -PassThru -NoNewWindow -ErrorAction Stop
        
        Write-Log "Installer completed with exit code: $($process.ExitCode)"
        
        # VC++ Redistributable installers often return 0 for success, but some may return other codes
        # that still indicate successful installation
        if ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010) {
            Write-Log "Installation successful with flags: $flags (exit code: $($process.ExitCode))"
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
    Start-Sleep -Seconds 3
}

if (-not $installSuccess) {
    Write-Log "ERROR: All VC++ Redistributable installation attempts failed"
    exit 1
}

# Wait for installation to complete and system to stabilize
Write-Log "Waiting for installation to complete and system to stabilize..."
Start-Sleep -Seconds 5

# Verify installation was successful
Write-Log "Verifying installation..."
$verificationSuccess = $false

try {
    # Check registry again
    $regPaths = @(
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\12.0\VC\Runtimes\x64",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\12.0\VC\Runtimes\x64"
    )
    
    foreach ($regPath in $regPaths) {
        if (Test-Path $regPath) {
            $installed = Get-ItemProperty -Path $regPath -Name "Installed" -ErrorAction SilentlyContinue
            if ($installed -and $installed.Installed -eq 1) {
                Write-Log "Verification successful: Visual C++ Redistributable found in registry: $regPath"
                $verificationSuccess = $true
                break
            }
        }
    }
    
    # Also check for installed programs
    if (-not $verificationSuccess) {
        $installedPrograms = Get-WmiObject -Class Win32_Product | Where-Object { 
            $_.Name -like "*Visual C++*Redistributable*" -or 
            $_.Name -like "*Microsoft Visual C++*" 
        }
        
        if ($installedPrograms) {
            Write-Log "Verification successful: Found installed Visual C++ Redistributable packages"
            foreach ($program in $installedPrograms) {
                Write-Log "  - $($program.Name) (Version: $($program.Version))"
            }
            $verificationSuccess = $true
        }
    }
}
catch {
    Write-Log "Warning: Error during verification: $($_.Exception.Message)"
}

if ($verificationSuccess) {
    Write-Log "Visual C++ Redistributable installation completed successfully!"
    exit 0
} else {
    Write-Log "WARNING: Installation may have succeeded but verification failed. Proceeding anyway..."
    exit 0
}
