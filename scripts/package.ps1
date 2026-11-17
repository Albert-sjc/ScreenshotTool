[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')]
    [string]$Version = '1.0.4',
    [string]$DotnetPath,
    [string]$IsccPath,
    [string]$InstallerOutputDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'src/ScreenshotTool/ScreenshotTool.csproj'
$installerScript = Join-Path $repositoryRoot 'installer/ScreenshotTool.iss'
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$publishRoot = Join-Path $artifactsRoot ('publish/' + $Version + '/win-x64')
$stagingRoot = Join-Path $artifactsRoot ('publish/staging-' + [Guid]::NewGuid().ToString('N'))
$installerRoot = if ($InstallerOutputDirectory) {
    if ([IO.Path]::IsPathRooted($InstallerOutputDirectory)) { [IO.Path]::GetFullPath($InstallerOutputDirectory) }
    else { [IO.Path]::GetFullPath((Join-Path $repositoryRoot $InstallerOutputDirectory)) }
} else { Join-Path $artifactsRoot ('installer/' + $Version) }
$installer = Join-Path $installerRoot 'ScreenshotTool-Setup-x64.exe'
$buildRoot = Join-Path $artifactsRoot ('build/win-x64/' + $Version)

function Remove-BuildDirectory {
    param([string]$Path)
    $absolutePath = [IO.Path]::GetFullPath($Path)
    if (-not $absolutePath.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a path outside the build artifact directory: $absolutePath"
    }
    if (Test-Path -LiteralPath $absolutePath) { Remove-Item -LiteralPath $absolutePath -Recurse -Force }
}

if (-not $DotnetPath) {
    $DotnetPath = Join-Path $repositoryRoot '.tools/dotnet/dotnet.exe'
    if (-not (Test-Path -LiteralPath $DotnetPath)) {
        $dotnetCommand = Get-Command 'dotnet.exe' -ErrorAction SilentlyContinue
        if ($dotnetCommand) { $DotnetPath = $dotnetCommand.Source }
    }
}
if (-not $DotnetPath -or -not (Test-Path -LiteralPath $DotnetPath)) {
    throw 'A .NET 10 SDK is required. Run: powershell -ExecutionPolicy Bypass -File scripts/bootstrap.ps1'
}
$DotnetPath = (Resolve-Path -LiteralPath $DotnetPath).Path
if (-not $IsccPath) {
    $compilerCandidates = @(
        (Join-Path $repositoryRoot '.tools/inno/ISCC.exe'),
        $env:ISCC_PATH,
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe')
    )
    $IsccPath = $compilerCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw 'Inno Setup 6 or newer is required. Run scripts/bootstrap.ps1, set ISCC_PATH, or pass -IsccPath.'
}
$IsccPath = (Resolve-Path -LiteralPath $IsccPath).Path
if (-not (Test-Path -LiteralPath $project)) { throw "Application project not found: $project" }
if (-not (Test-Path -LiteralPath $installerScript)) { throw "Installer script not found: $installerScript" }

$numericParts = $Version.Split('-')[0].Split('.') | ForEach-Object { [int]$_ }
if (($numericParts | Where-Object { $_ -gt 65534 }).Count -gt 0) { throw 'Version components must be at most 65534 for .NET assembly version metadata.' }
$fileVersion = ($numericParts -join '.') + '.0'
New-Item -ItemType Directory -Path $installerRoot -Force | Out-Null

Push-Location $repositoryRoot
try {
    $sdkVersion = & $DotnetPath --version
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^10\.') {
        throw 'A compatible .NET 10 SDK is required; a runtime alone cannot build the app. Run scripts/bootstrap.ps1.'
    }

    Write-Host "Publishing ScreenshotTool $Version for win-x64 with its runtime (.NET SDK $sdkVersion)."
    # Keep publish builds separate from any application currently running from
    # src/bin. BaseOutputPath/BaseIntermediateOutputPath retain SDK exclusions.
    $publishOutput = & $DotnetPath publish $project -c $Configuration -r win-x64 --self-contained true -o $stagingRoot "-p:OutputPath=$buildRoot/bin/" "-p:IntermediateOutputPath=$buildRoot/obj/" "-p:MSBuildProjectExtensionsPath=$buildRoot/restore/" "-p:Version=$Version" "-p:FileVersion=$fileVersion" "-p:AssemblyVersion=$fileVersion" '-p:PublishSingleFile=false' '-p:PublishTrimmed=false' '-p:DebugType=None' '-p:DebugSymbols=false'
    $publishExitCode = $LASTEXITCODE
    $publishOutput | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $installerRoot 'dotnet-publish.log')
    if ($publishExitCode -ne 0) { throw "dotnet publish failed (exit $publishExitCode). See $installerRoot/dotnet-publish.log." }

    foreach ($requiredFile in @('ScreenshotTool.exe', 'ScreenshotTool.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'PresentationFramework.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $stagingRoot $requiredFile))) {
            throw "Self-contained publish is incomplete: missing $requiredFile."
        }
    }

    foreach ($process in @(Get-Process -Name 'ScreenshotTool' -ErrorAction SilentlyContinue)) {
        $runningPath = $null
        try { $runningPath = $process.Path } catch { }
        if ($runningPath -and $runningPath.StartsWith($publishRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "ScreenshotTool $Version is running from $publishRoot. Choose a new release version to keep that directory intact."
        }
    }
    Remove-BuildDirectory $publishRoot
    New-Item -ItemType Directory -Path (Split-Path -Parent $publishRoot) -Force | Out-Null
    Move-Item -LiteralPath $stagingRoot -Destination $publishRoot
    if (Test-Path -LiteralPath $installer) { Remove-Item -LiteralPath $installer -Force }

    Write-Host 'Compiling the offline installer.'
    $compilerOutput = & $IsccPath "/DAppVersion=$Version" "/DAppFileVersion=$fileVersion" "/DPublishDir=$publishRoot" "/DInstallerOutputDir=$installerRoot" $installerScript
    $compilerExitCode = $LASTEXITCODE
    $compilerOutput | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $installerRoot 'inno-compile.log')
    if ($compilerExitCode -ne 0) { throw "Inno Setup compilation failed (exit $compilerExitCode). See $installerRoot/inno-compile.log." }
    $compilerVersionLine = $compilerOutput | Select-String -Pattern 'Compiler engine version: Inno Setup ([0-9.]+)' | Select-Object -First 1
    $compilerVersion = if ($compilerVersionLine) { $compilerVersionLine.Matches[0].Groups[1].Value } else { 'unknown (see inno-compile.log)' }
    if (-not (Test-Path -LiteralPath $installer) -or (Get-Item -LiteralPath $installer).Length -eq 0) {
        throw "The compiler did not produce the expected installer: $installer"
    }

    $installerHash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    "$installerHash  ScreenshotTool-Setup-x64.exe" | Set-Content -Encoding ASCII -LiteralPath ($installer + '.sha256')
    $manifest = [ordered]@{
        product = 'ScreenshotTool'; version = $Version; configuration = $Configuration
        builtUtc = [DateTime]::UtcNow.ToString('o'); runtimeIdentifier = 'win-x64'; selfContained = $true
        dotnetSdk = $sdkVersion.Trim(); innoSetup = $compilerVersion
        installer = 'ScreenshotTool-Setup-x64.exe'; sha256 = $installerHash
        installerBytes = (Get-Item -LiteralPath $installer).Length
        publishedExecutable = 'artifacts/publish/' + $Version + '/win-x64/ScreenshotTool.exe'
        executableSha256 = (Get-FileHash -LiteralPath (Join-Path $publishRoot 'ScreenshotTool.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $installerRoot 'build-manifest.json')
    Write-Host "Created $installer"
    Write-Host "SHA256 $installerHash"
}
finally {
    Remove-BuildDirectory $stagingRoot
    Pop-Location
}
