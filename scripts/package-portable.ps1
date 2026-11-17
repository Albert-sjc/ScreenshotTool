[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')]
    [string]$Version = '1.0.4'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$publishRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ('artifacts/publish/' + $Version + '/win-x64')))
$outputRoot = Join-Path $repositoryRoot 'artifacts/portable'
$archiveName = "ScreenshotTool-$Version-win-x64.zip"
$archivePath = Join-Path $outputRoot $archiveName
$partialPath = Join-Path $outputRoot ($archiveName + '.partial-' + [Guid]::NewGuid().ToString('N'))
$readmePath = Join-Path $outputRoot ('.portable-readme-' + [Guid]::NewGuid().ToString('N') + '.txt')
$readmeEntryName = '免安装说明.txt'

foreach ($requiredFile in @('ScreenshotTool.exe', 'ScreenshotTool.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'PresentationFramework.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishRoot $requiredFile))) {
        throw "Self-contained release $Version is missing $requiredFile. Run scripts/package.ps1 -Version $Version first."
    }
}
$productVersion = (Get-Item -LiteralPath (Join-Path $publishRoot 'ScreenshotTool.exe')).VersionInfo.ProductVersion
if ($productVersion.Split('+')[0] -ne $Version) { throw "Published executable version $productVersion does not match $Version." }

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$files = @(Get-ChildItem -LiteralPath $publishRoot -File -Recurse | Sort-Object FullName)
$sourcePrefix = $publishRoot + [IO.Path]::DirectorySeparatorChar
$expectedHashes = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
$readme = @"
ScreenshotTool $Version 免安装版

1. 将此 ZIP 全部解压到一个文件夹，再运行 ScreenshotTool.exe。请保留旁边的文件和子文件夹。
2. 运行后常驻系统托盘。左键单击托盘图标打开设置；右键菜单可截图或退出；默认 Ctrl+Alt+S 截图。
3. 切换到新版前，请自行从旧实例的托盘菜单退出，再启动新版。一次只运行一个实例。
4. 已包含运行时，无需另装 .NET。设置保存在当前用户的本地应用数据目录。
5. 贴图和原标注只保留到本次退出；主动保存的 PNG/JPG 会保留。
"@
[IO.File]::WriteAllText($readmePath, $readme, [Text.UTF8Encoding]::new($true))
[IO.File]::SetLastWriteTimeUtc($readmePath, [DateTime]::new(2000, 1, 1, 0, 0, 0, [DateTimeKind]::Utc))

try {
    $fileStream = [IO.File]::Open($partialPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $archive = [IO.Compression.ZipArchive]::new($fileStream, [IO.Compression.ZipArchiveMode]::Create, $false, [Text.Encoding]::UTF8)
        try {
            foreach ($file in $files) {
                $relativePath = $file.FullName.Substring($sourcePrefix.Length).Replace('\', '/')
                if ($relativePath -eq $readmeEntryName) { throw "Publish directory contains the reserved ZIP entry $readmeEntryName." }
                $expectedHashes.Add($relativePath, (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())
                $entry = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relativePath, [IO.Compression.CompressionLevel]::Optimal)
            }
            $expectedHashes.Add($readmeEntryName, (Get-FileHash -LiteralPath $readmePath -Algorithm SHA256).Hash.ToLowerInvariant())
            $entry = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $readmePath, $readmeEntryName, [IO.Compression.CompressionLevel]::Optimal)
        }
        finally { $archive.Dispose() }
    }
    finally { $fileStream.Dispose() }

    $verified = 0
    $archive = [IO.Compression.ZipFile]::OpenRead($partialPath)
    try {
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.Contains('\') -or -not $seen.Add($entry.FullName) -or -not $expectedHashes.ContainsKey($entry.FullName)) {
                throw "Unexpected or nonstandard ZIP entry: $($entry.FullName)"
            }
            $stream = $entry.Open()
            $sha256 = [Security.Cryptography.SHA256]::Create()
            try { $actualHash = [BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
            finally { $sha256.Dispose(); $stream.Dispose() }
            if ($actualHash -ne $expectedHashes[$entry.FullName]) { throw "ZIP content hash mismatch: $($entry.FullName)" }
            $verified++
        }
        if ($verified -ne $expectedHashes.Count) { throw 'ZIP does not contain every source file.' }
    }
    finally { $archive.Dispose() }

    Move-Item -LiteralPath $partialPath -Destination $archivePath -Force
    $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$archiveHash  $archiveName" | Set-Content -Encoding ASCII -LiteralPath ($archivePath + '.sha256')
    [ordered]@{
        product = 'ScreenshotTool'; version = $Version; builtUtc = [DateTime]::UtcNow.ToString('o')
        source = 'artifacts/publish/' + $Version + '/win-x64'; sourceFileCount = $files.Count
        verifiedEntryCount = $verified; allEntriesUseForwardSlashes = $true
        archive = $archiveName; archiveBytes = (Get-Item -LiteralPath $archivePath).Length; sha256 = $archiveHash
    } | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath ($archivePath + '.manifest.json')
    Write-Host "Created $archivePath; verified $verified entries against source SHA256 hashes."
    Write-Host "SHA256 $archiveHash"
}
finally {
    if (Test-Path -LiteralPath $partialPath) { Remove-Item -LiteralPath $partialPath -Force }
    if (Test-Path -LiteralPath $readmePath) { Remove-Item -LiteralPath $readmePath -Force }
}
