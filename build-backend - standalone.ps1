[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
# �趨��Ŀ·�������Ŀ¼
Set-Location "D:\code\AudioCopy"
$backendDir   = ".\libAudioCopy-Backend"
$flacBackendDir = ".\flacBackend"
$assetsDir    = ".\AudioCopyUI\Assets"
$publishDir   =  ".\libAudioCopy-Backend-standalone"
$zipFileName  = "backend.zip"
$zipFilePath  = Join-Path (Get-Location) $zipFileName
$versionFile  = Join-Path $assetsDir "backend_version.txt"

# 1. �ҵ������� .csproj �ļ�
$csproj = Get-ChildItem $backendDir -Filter *.csproj | Select-Object -First 1
if (-not $csproj) {
    Write-Error "δ�ҵ��κ� .csproj �ļ��� $backendDir"
    exit 1
}


# 2. ������Ŀ��Release + �԰�����
# ����ɵķ���Ŀ¼
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
# ���� dotnet publish
dotnet publish $csproj.FullName `
    -c Debug `
    -o $publishDir `
    | Write-Host

if (Test-Path $flacBackendDir) {
    Copy-Item -Path (Join-Path $flacBackendDir "*") -Destination $publishDir -Recurse -Force
    Write-Host "�ѽ� $flacBackendDir �µ��ļ����Ƶ� $publishDir" -ForegroundColor Green
} else {
    Write-Host "Ŀ¼ $flacBackendDir �����ڣ���������" -ForegroundColor Yellow
}

Read-Host "��������˳�..."

