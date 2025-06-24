#Requires -RunAsAdministrator

[CmdletBinding(SupportsShouldProcess)]
param (
    [switch]$Silent = $true,
    [switch]$AllUsers,
    [string]$User
)

# Show error if current powershell environment is limited by security policies
if ($ExecutionContext.SessionState.LanguageMode -ne "FullLanguage") {
    Write-Host "Error: ClearStartMenu is unable to run on your system, powershell execution is restricted by security policies" -ForegroundColor Red
    exit
}

# Log script output
Start-Transcript -Path "$PSScriptRoot/ClearStartMenu.log" -Append -IncludeInvocationHeader -Force | Out-Null

Write-Output "-------------------------------------------------------------------------------------------"
Write-Output " ClearStartMenu Script - Windows 11 Start Menu Cleaner"
Write-Output "-------------------------------------------------------------------------------------------"

# Get the current user name
function GetUserName {
    if ($User) { 
        return $User 
    }
    return $env:USERNAME
}

# Replace the startmenu for all users, when using the default startmenuTemplate this clears all pinned apps
# Credit: https://lazyadmin.nl/win-11/customize-windows-11-start-menu-layout/
function ReplaceStartMenuForAllUsers {
    param (
        $startMenuTemplate = "$PSScriptRoot/Assets/Start/start2.bin"
    )

    Write-Output "> Removing all pinned apps from the start menu for all users..."

    # Check if template bin file exists, return early if it doesn't
    if (-not (Test-Path $startMenuTemplate)) {
        Write-Host "Error: Unable to clear start menu, start2.bin file missing from script folder" -ForegroundColor Red
        Write-Output ""
        return
    }

    # Get path to start menu file for all users
    $userPathString = $env:USERPROFILE -Replace ('\\' + $env:USERNAME + '$'), "\*\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState"
    $usersStartMenuPaths = get-childitem -path $userPathString

    # Go through all users and replace the start menu file
    ForEach ($startMenuPath in $usersStartMenuPaths) {
        ReplaceStartMenu $startMenuTemplate "$($startMenuPath.Fullname)\start2.bin"
    }

    # Also replace the start menu file for the default user profile
    $defaultStartMenuPath = $env:USERPROFILE -Replace ('\\' + $env:USERNAME + '$'), '\Default\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState'

    # Create folder if it doesn't exist
    if (-not (Test-Path $defaultStartMenuPath)) {
        new-item $defaultStartMenuPath -ItemType Directory -Force | Out-Null
        Write-Output "Created LocalState folder for default user profile"
    }

    # Copy template to default profile
    Copy-Item -Path $startMenuTemplate -Destination $defaultStartMenuPath -Force
    Write-Output "Replaced start menu for the default user profile"
    Write-Output ""
}

# Replace the startmenu for current user, when using the default startmenuTemplate this clears all pinned apps
# Credit: https://lazyadmin.nl/win-11/customize-windows-11-start-menu-layout/
function ReplaceStartMenu {
    param (
        $startMenuTemplate = "$PSScriptRoot/Assets/Start/start2.bin",
        $startMenuBinFile = "$env:LOCALAPPDATA\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start2.bin"
    )

    # Change path to correct user if a user was specified
    if ($User) {
        $startMenuBinFile = $env:USERPROFILE -Replace ('\\' + $env:USERNAME + '$'), "\$(GetUserName)\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start2.bin"
    }

    # Check if template bin file exists, return early if it doesn't
    if (-not (Test-Path $startMenuTemplate)) {
        Write-Host "Error: Unable to replace start menu, template file not found" -ForegroundColor Red
        return
    }

    if ([IO.Path]::GetExtension($startMenuTemplate) -ne ".bin" ) {
        Write-Host "Error: Unable to replace start menu, template file is not a valid .bin file" -ForegroundColor Red
        return
    }

    # Check if bin file exists, return early if it doesn't
    if (-not (Test-Path $startMenuBinFile)) {
        Write-Host "Error: Unable to replace start menu for user $(GetUserName), original start2.bin file not found" -ForegroundColor Red
        return
    }

    $backupBinFile = $startMenuBinFile + ".bak"

    # Backup current start menu file
    Move-Item -Path $startMenuBinFile -Destination $backupBinFile -Force

    # Copy template file
    Copy-Item -Path $startMenuTemplate -Destination $startMenuBinFile -Force

    Write-Output "Replaced start menu for user $(GetUserName)"
}

# Restart the Windows Explorer process
function RestartExplorer {
    Write-Output "> Restarting Windows Explorer process to apply changes... (This may cause some flickering)"

    # Only restart if the powershell process matches the OS architecture.
    # Restarting explorer from a 32bit PowerShell window will fail on a 64bit OS
    if ([Environment]::Is64BitProcess -eq [Environment]::Is64BitOperatingSystem) {
        Stop-Process -processName: Explorer -Force
    }
    else {
        Write-Warning "Unable to restart Windows Explorer process, please manually reboot your PC to apply all changes."
    }
}

# Main execution
if ($AllUsers) {
    ReplaceStartMenuForAllUsers
} else {
    Write-Output "> Removing all pinned apps from the start menu for user $(GetUserName)..."
    ReplaceStartMenu
    Write-Output ""
}

RestartExplorer

Write-Output ""
Write-Output "Script completed! Start menu has been cleared."
Write-Output "Note: If you want to restore your previous start menu, look for .bak files in the LocalState folder."

# Always run silently - no prompts
Stop-Transcript 