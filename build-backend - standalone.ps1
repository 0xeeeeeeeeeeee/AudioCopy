[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
# 设定项目路径和输出目录
Set-Location "D:\code\AudioCopy"
$backendDir   = ".\libAudioCopy-Backend"
$flacBackendDir = ".\flacBackend"
$assetsDir    = ".\AudioCopyUI\Assets"
$publishDir   =  ".\libAudioCopy-Backend-standalone"
$zipFileName  = "backend.zip"
$zipFilePath  = Join-Path (Get-Location) $zipFileName
$versionFile  = Join-Path $assetsDir "backend_version.txt"

# 1. 找到并解析 .csproj 文件
$csproj = Get-ChildItem $backendDir -Filter *.csproj | Select-Object -First 1
if (-not $csproj) {
    Write-Error "未找到任何 .csproj 文件于 $backendDir"
    exit 1
}


# 2. 编译项目（Release + 自包含）
# 清理旧的发布目录
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
# 调用 dotnet publish
dotnet publish $csproj.FullName `
    -c Debug `
    -o $publishDir `
    | Write-Host

if (Test-Path $flacBackendDir) {
    Copy-Item -Path (Join-Path $flacBackendDir "*") -Destination $publishDir -Recurse -Force
    Write-Host "已将 $flacBackendDir 下的文件复制到 $publishDir" -ForegroundColor Green
} else {
    Write-Host "目录 $flacBackendDir 不存在，跳过复制" -ForegroundColor Yellow
}

Read-Host "按任意键退出..."

