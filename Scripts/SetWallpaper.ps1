param(
    [Parameter(Mandatory=$true)]
    [string]$WallpaperPath
)

# Function to set wallpaper using SystemParametersInfo
function Set-Wallpaper {
    param([string]$Path)
    
    Add-Type -TypeDefinition @"
    using System;
    using System.Runtime.InteropServices;
    
    public class Wallpaper {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        
        public const int SPI_SETDESKWALLPAPER = 0x0014;
        public const int SPIF_UPDATEINIFILE = 0x01;
        public const int SPIF_SENDCHANGE = 0x02;
    }
"@
    
    # Set wallpaper style to fill
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "WallpaperStyle" -Value "10" -Type String
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "TileWallpaper" -Value "0" -Type String
    
    # Set the wallpaper
    $result = [Wallpaper]::SystemParametersInfo([Wallpaper]::SPI_SETDESKWALLPAPER, 0, $Path, [Wallpaper]::SPIF_UPDATEINIFILE -bor [Wallpaper]::SPIF_SENDCHANGE)
    
    if ($result -eq 0) {
        throw "Failed to set wallpaper"
    }
    
    Write-Host "Wallpaper set successfully to: $Path"
}

try {
    # Check if file exists
    if (-not (Test-Path $WallpaperPath)) {
        throw "Wallpaper file not found: $WallpaperPath"
    }
    
    Write-Host "Setting wallpaper to: $WallpaperPath"
    Set-Wallpaper -Path $WallpaperPath
    Write-Host "Wallpaper change completed successfully"
}
catch {
    Write-Error "Error setting wallpaper: $($_.Exception.Message)"
    exit 1
} 