[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
# 设定项目路径和输出目录
Set-Location "D:\code\AudioCopy"
$backendDir   = "..\AudioClone\AudioClone.Server\"
$flacBackendDir = ".\StaticResources"
$assetsDir    = ".\AudioCopyUI\Assets"
$publishDir   = ".\PublishTemp"
$zipFileName  = "backend.zip"
$zipFilePath  = Join-Path (Get-Location) $zipFileName
$versionFile  = Join-Path $assetsDir "backend_version.txt"


# 1. 找到并解析 .csproj 文件
$csproj = Get-ChildItem $backendDir -Filter *.csproj | Select-Object -First 1
if (-not $csproj) {
    Write-Error "δ????κ? .csproj ????? $backendDir"
    exit 1
}

[xml]$csprojXml = Get-Content $csproj.FullName
$versionNode = $csprojXml.Project.PropertyGroup.Version
if (-not $versionNode) {
    Write-Error ".csproj 文件未找到 <Version> 节点"
    exit 1
}
# $csprojVersion = $versionNode.Trim()

# # 解析并增量最后一段版本号
# $oldVersion = (Get-Content -Path $versionFile).Trim()

# $parts = $oldVersion.Split('.')
# [int]$parts[-1] += 1
$newVersion = $versionNode.Trim()

# 保存修改后的 .csproj
Write-Host "版本从 $oldVersion 更新为 $newVersion" -ForegroundColor Green

# 2. 编译项目（Release + 自包含）
# 清理旧的发布目录
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
# 调用 dotnet publish
$BackendDir = Join-Path $publishDir "backend"
# $BackendUnTrimedDir = Join-Path $publishDir "backend_UnTrimmed"

dotnet publish $csproj.FullName `
    -c Release `
    --sc `
    -o $BackendDir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:SatelliteResourceLanguages=en-GB `
    | Write-Host

Get-ChildItem -Path $publishDir -Recurse -Filter *.pdb | Remove-Item -Force


# dotnet publish $csproj.FullName `
#     -c Release `
#     --sc `
#     -o $BackendUnTrimedDir `
#     | Write-Host

# Get-ChildItem -Path $BackendUnTrimedDir -Filter "AudioClone*.dll" | ForEach-Object {
#     Copy-Item -Path $_.FullName -Destination $BackendDir -Force
# }
# Remove-Item -Path $BackendUnTrimedDir -Recurse -Force


if (Test-Path $flacBackendDir) {
    Copy-Item -Path (Join-Path $flacBackendDir "*") -Destination $publishDir -Recurse -Force
    Write-Host "已将 $flacBackendDir 下的文件复制到 $publishDir" -ForegroundColor Green
} else {
    Write-Host "目录 $flacBackendDir 不存在，跳过复制" -ForegroundColor Yellow
}

Remove-Item -Path (Join-Path $publishDir "tokens.json") -Force -ErrorAction SilentlyContinue

# 3. 打包为 backend.zip（确保根目录布局）
if (Test-Path $zipFilePath) { Remove-Item $zipFilePath -Force }
Compress-Archive -Path (Join-Path $publishDir "*") `
                 -DestinationPath $zipFilePath `
                 -Force
Write-Host "已生成压缩包：$zipFilePath" -ForegroundColor Green

# 4. 复制到 Assets 目录
Copy-Item $zipFilePath -Destination $assetsDir -Force
Write-Host "已复制 $zipFileName 到 $assetsDir" -ForegroundColor Green

# 5. 写入版本号
[System.IO.File]::WriteAllText($versionFile, $newVersion, [System.Text.Encoding]::UTF8)
Write-Host "已更新版本文件：$versionFile" -ForegroundColor Green

Read-Host "按任意键退出..."
