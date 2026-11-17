[CmdletBinding()]
param(
    [switch]$SkipInno
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$toolsRoot = Join-Path $repositoryRoot '.tools'
$downloadsRoot = Join-Path $toolsRoot 'downloads'
$dotnetRoot = Join-Path $toolsRoot 'dotnet'
$innoRoot = Join-Path $toolsRoot 'inno'

# Pin official release assets so repeated builds use the same toolchain.
$sdkVersion = '10.0.404'
$sdkUrl = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.404/dotnet-sdk-10.0.404-win-x64.zip'
$sdkSha512 = '330ddd2665b43cd2e428216a0d8ea1ac7bb19b5434fdc530e62010dc3b52823a620bf95489d628e1ed6f5122401793ee4887cca4a019122361a1231497f632ee'
$innoVersion = '6.7.4'
$innoUrl = 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_4/innosetup-6.7.4.exe'
$innoSha256 = 'a549929319f9120716677c685535ca4671e4b937b5fdc87e2585f32d8eb30e8c'
$innoCompilerSha256 = 'f02fa0e5f237b47d7ed1edae9b991866ec94fe02af61eb6a73e94f92fe8e0604'

function Get-VerifiedDownload {
    param([string]$Url, [string]$Destination, [string]$Algorithm, [string]$ExpectedHash)

    if (Test-Path -LiteralPath $Destination) {
        if ((Get-FileHash -LiteralPath $Destination -Algorithm $Algorithm).Hash -eq $ExpectedHash) {
            return
        }
    }

    $partialPath = $Destination + '.partial'
    Write-Host "Downloading $Url"
    $curl = Get-Command 'curl.exe' -ErrorAction SilentlyContinue
    if ($curl) {
        & $curl.Source --fail --location --retry 3 --connect-timeout 30 --max-time 1200 --output $partialPath $Url
        if ($LASTEXITCODE -ne 0) { throw "Download failed (curl exit $LASTEXITCODE): $Url" }
    }
    else {
        Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $partialPath
    }

    if ((Get-FileHash -LiteralPath $partialPath -Algorithm $Algorithm).Hash -ne $ExpectedHash) {
        throw "$Algorithm verification failed for $Url. The untrusted download remains at $partialPath."
    }
    Move-Item -LiteralPath $partialPath -Destination $Destination -Force
}

New-Item -ItemType Directory -Path $downloadsRoot -Force | Out-Null
$dotnet = Join-Path $dotnetRoot 'dotnet.exe'
$installedSdk = if (Test-Path -LiteralPath $dotnet) { & $dotnet --list-sdks }
if (-not ($installedSdk -match ('^' + [regex]::Escape($sdkVersion) + ' '))) {
    $sdkArchive = Join-Path $downloadsRoot "dotnet-sdk-$sdkVersion-win-x64.zip"
    Get-VerifiedDownload $sdkUrl $sdkArchive 'SHA512' $sdkSha512
    New-Item -ItemType Directory -Path $dotnetRoot -Force | Out-Null
    Write-Host "Extracting .NET SDK $sdkVersion into $dotnetRoot"
    Expand-Archive -LiteralPath $sdkArchive -DestinationPath $dotnetRoot -Force
}

$sdkList = & $dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdkList -match ('^' + [regex]::Escape($sdkVersion) + ' '))) {
    throw "The portable .NET SDK could not be validated at $dotnet."
}

if (-not $SkipInno) {
    $iscc = Join-Path $innoRoot 'ISCC.exe'
    $installedCompilerHash = if (Test-Path -LiteralPath $iscc) { (Get-FileHash -LiteralPath $iscc -Algorithm SHA256).Hash }
    if ($installedCompilerHash -ne $innoCompilerSha256) {
        $innoInstaller = Join-Path $downloadsRoot "innosetup-$innoVersion.exe"
        Get-VerifiedDownload $innoUrl $innoInstaller 'SHA256' $innoSha256
        $signature = Get-AuthenticodeSignature -LiteralPath $innoInstaller
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*Pyrsys B.V.*') {
            throw 'Inno Setup must have a valid Authenticode signature from Pyrsys B.V.'
        }

        # Official portable mode suppresses registry entries, shortcuts and uninstaller registration.
        $installLog = Join-Path $downloadsRoot 'inno-portable-install.log'
        $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/CURRENTUSER', '/PORTABLE=1', '/NOICONS', '/TASKS=""', ('/DIR="' + $innoRoot + '"'), ('/LOG="' + $installLog + '"'))
        $process = Start-Process -FilePath $innoInstaller -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
        if ($process.ExitCode -ne 0) { throw "Inno Setup portable extraction failed (exit $($process.ExitCode)). See $installLog." }
    }
    if (-not (Test-Path -LiteralPath $iscc) -or (Get-FileHash -LiteralPath $iscc -Algorithm SHA256).Hash -ne $innoCompilerSha256) {
        throw "Inno Setup $innoVersion could not be validated at $iscc."
    }
}

$toolchain = [ordered]@{
    acquiredUtc = [DateTime]::UtcNow.ToString('o')
    dotnet = [ordered]@{ version = $sdkVersion; path = $dotnet; url = $sdkUrl; sha512 = $sdkSha512 }
    inno = if ($SkipInno) { $null } else { [ordered]@{ version = $innoVersion; path = (Join-Path $innoRoot 'ISCC.exe'); url = $innoUrl; sha256 = $innoSha256; compilerSha256 = $innoCompilerSha256; publisher = 'Pyrsys B.V.' } }
    sources = @('https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json', 'https://api.github.com/repos/jrsoftware/issrc/releases/tags/is-6_7_4')
}
$toolchain | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $toolsRoot 'toolchain.json')
Write-Host "Ready: $dotnet (SDK $sdkVersion)"
if (-not $SkipInno) { Write-Host "Ready: $iscc (Inno Setup $innoVersion)" }
Write-Host 'No machine-wide installation, PATH changes or environment changes were made.'
