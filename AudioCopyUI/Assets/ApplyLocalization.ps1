[System.Console]::Title = "Applying localization settings"


Write-Host -BackgroundColor Green -ForegroundColor White $args[0]

taskkill.exe /f /im "AudioCopyUI.exe" 2> $null 1>$null

for ($i = 0; $i -lt 3; $i++) {
    Start-Process "audiocopy:nowindow" 
    
    Start-Sleep -Seconds 3

    taskkill.exe /f /im "AudioCopyUI.exe" 2> $null 1>$null
}
Start-Process "audiocopy:" 
