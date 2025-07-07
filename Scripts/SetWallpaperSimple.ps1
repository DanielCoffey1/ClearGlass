param(
    [Parameter(Mandatory=$true)]
    [string]$WallpaperPath
)

try {
    Write-Host "Setting wallpaper to: $WallpaperPath"
    
    # Set wallpaper style to fill (10 = fill, 6 = fit, 2 = stretch, 0 = center)
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "WallpaperStyle" -Value "10" -Type String -Force
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "TileWallpaper" -Value "0" -Type String -Force
    
    # Set the wallpaper path
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "WallPaper" -Value $WallpaperPath -Type String -Force
    
    # Force refresh the desktop
    $code = @'
    using System.Runtime.InteropServices;
    public class Wallpaper {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        public const int SPI_SETDESKWALLPAPER = 0x0014;
        public const int SPIF_UPDATEINIFILE = 0x01;
        public const int SPIF_SENDCHANGE = 0x02;
    }
'@
    
    Add-Type -TypeDefinition $code
    $result = [Wallpaper]::SystemParametersInfo([Wallpaper]::SPI_SETDESKWALLPAPER, 0, $WallpaperPath, [Wallpaper]::SPIF_UPDATEINIFILE -bor [Wallpaper]::SPIF_SENDCHANGE)
    
    if ($result -eq 0) {
        throw "Failed to set wallpaper via SystemParametersInfo"
    }
    
    Write-Host "Wallpaper set successfully"
    exit 0
}
catch {
    Write-Error "Error setting wallpaper: $($_.Exception.Message)"
    exit 1
} 