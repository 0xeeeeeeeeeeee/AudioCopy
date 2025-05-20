[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
# �趨��Ŀ·�������Ŀ¼
Set-Location "D:\code\AudioCopy"
$backendDir   = ".\libAudioCopy-Backend"
$flacBackendDir = ".\libAudioCopy-Backend-Resources"
$assetsDir    = ".\AudioCopyUI\Assets"
$publishDir   = Join-Path $backendDir "publish_temp"
$zipFileName  = "backend.zip"
$zipFilePath  = Join-Path (Get-Location) $zipFileName
$versionFile  = Join-Path $assetsDir "backend_version.txt"

# 1. �ҵ������� .csproj �ļ�
$csproj = Get-ChildItem $backendDir -Filter *.csproj | Select-Object -First 1
if (-not $csproj) {
    Write-Error "δ�ҵ��κ� .csproj �ļ��� $backendDir"
    exit 1
}

# �������������һ�ΰ汾��
$oldVersion = (Get-Content -Path $versionFile).Trim()

$parts = $oldVersion.Split('.')
[int]$parts[-1] += 1
$newVersion = $parts -join '.'

# �����޸ĺ�� .csproj
Write-Host "�汾�� $oldVersion ����Ϊ $newVersion" -ForegroundColor Green

# 2. ������Ŀ��Release + �԰�����
# ����ɵķ���Ŀ¼
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
# ���� dotnet publish
dotnet publish $csproj.FullName `
    -c Release `
    --sc `
    -o $publishDir `
    -p:Version=$newVersion `
    | Write-Host

if (Test-Path $flacBackendDir) {
    Copy-Item -Path (Join-Path $flacBackendDir "*") -Destination $publishDir -Recurse -Force
    Write-Host "�ѽ� $flacBackendDir �µ��ļ����Ƶ� $publishDir" -ForegroundColor Green
} else {
    Write-Host "Ŀ¼ $flacBackendDir �����ڣ���������" -ForegroundColor Yellow
}

Remove-Item -Path (Join-Path $publishDir "tokens.json") -Force -ErrorAction SilentlyContinue

# 3. ���Ϊ backend.zip��ȷ����Ŀ¼���֣�
if (Test-Path $zipFilePath) { Remove-Item $zipFilePath -Force }
Compress-Archive -Path (Join-Path $publishDir "*") `
                 -DestinationPath $zipFilePath `
                 -Force
Write-Host "������ѹ������$zipFilePath" -ForegroundColor Green

# 4. ���Ƶ� Assets Ŀ¼
Copy-Item $zipFilePath -Destination $assetsDir -Force
Write-Host "�Ѹ��� $zipFileName �� $assetsDir" -ForegroundColor Green

# 5. д��汾��
[System.IO.File]::WriteAllText($versionFile, $newVersion, [System.Text.Encoding]::UTF8)
Write-Host "�Ѹ��°汾�ļ���$versionFile" -ForegroundColor Green

Read-Host "��������˳�..."
