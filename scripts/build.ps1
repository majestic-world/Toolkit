#Requires -Version 7.0
<#
.SYNOPSIS
    Publishes L2 Toolkit as a Native AOT app and optionally packages its installer.
.DESCRIPTION
    windows: dotnet publish win-<arch>; -Installer runs Inno Setup (Setup.iss).
    macos:   dotnet publish osx-<arch>, assembles and ad-hoc signs "L2 Toolkit.app";
             -Installer creates the .dmg with create-dmg.
    linux:   dotnet publish linux-<arch>.
    Native AOT cannot cross-compile between operating systems, so each platform
    must be built on its own OS. Output goes to build/<Configuration>/<rid>/publish/,
    tool logs to build/logs/<rid>/.
#>
[CmdletBinding()]
param(
    [ValidateSet('windows', 'macos', 'linux')]
    [string]$Platform = 'windows',

    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant(),

    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [switch]$Installer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'BuildConsole.psm1') -Force

$root = Split-Path -Parent $PSScriptRoot
$appName = 'L2 Toolkit'
$project = Join-Path $root 'L2Toolkit.csproj'
$rid = @{ windows = 'win'; macos = 'osx'; linux = 'linux' }[$Platform] + "-$Architecture"
$publishDir = Join-Path $root "build/$Configuration/$rid/publish"
$logs = Join-Path $root "build/logs/$rid"
$publishProperties = @(
    '-p:PublishAot=true',
    '-p:OptimizationPreference=Speed',
    '-p:StackTraceSupport=false',
    '-p:InvariantGlobalization=true'
)

# English tool output keeps log parsing independent of the machine's locale.
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

# Returns the log that holds the diagnostic: stderr when the tool wrote there.
function Get-DiagnosticLog([string]$Log) {
    $errorLog = "$Log.stderr"
    if ((Test-Path -LiteralPath $errorLog) -and (Get-Item -LiteralPath $errorLog).Length -gt 0) { return $errorLog }
    return $Log
}

function Format-FileSize([string]$Path) {
    $megabytes = (Get-Item -LiteralPath $Path).Length / 1MB
    return [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0:0.0} MB', $megabytes)
}

function Find-InnoSetupCompiler {
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    return $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

# APP_VERSION from .env; the csproj reads the same value for the assembly version.
function Get-AppVersion {
    $envFile = Join-Path $root '.env'
    if (-not (Test-Path -LiteralPath $envFile)) { throw "Missing $envFile with APP_VERSION=<major.minor.patch>." }
    $match = Select-String -LiteralPath $envFile -Pattern '^APP_VERSION=([0-9]+(\.[0-9]+){1,3})\s*$' | Select-Object -First 1
    if (-not $match) { throw "APP_VERSION in $envFile must look like 3.8.0." }
    return $match.Matches[0].Groups[1].Value
}

$exitCode = 0
try {
    $platformTitle = @{ windows = 'Windows'; macos = 'macOS'; linux = 'Linux' }[$Platform]
    $installerSuffix = if ($Installer) { ' + installer' } else { '' }
    $version = Get-AppVersion
    Initialize-BuildConsole "$appName $version Build - $Configuration|$rid$installerSuffix" $root
    New-Item -ItemType Directory -Force -Path $logs | Out-Null
    Get-ChildItem -LiteralPath $logs -File | Remove-Item

    # ── Toolchain ─────────────────────────────────────────────────────────
    Start-BuildStep 'Checking toolchain'
    $hostMatches = switch ($Platform) { 'windows' { $IsWindows } 'macos' { $IsMacOS } 'linux' { $IsLinux } }
    if (-not $hostMatches) {
        Stop-BuildStep "Native AOT for $platformTitle must be built on $platformTitle" '' ''
        throw "Run this build on $platformTitle; Native AOT does not cross-compile between operating systems."
    }
    if ($Installer -and $Platform -eq 'linux') {
        Stop-BuildStep 'no Linux installer is defined' '' ''
        throw 'The -Installer switch supports windows (Inno Setup) and macos (.dmg).'
    }
    if ($Installer -and $Platform -eq 'windows' -and ($Configuration -ne 'Release' -or $Architecture -ne 'x64')) {
        Stop-BuildStep 'Setup.iss packages Release|win-x64 only' '' ''
        throw 'The Windows installer requires -Configuration Release and -Architecture x64.'
    }

    $dotnet = (Get-Command 'dotnet' -ErrorAction SilentlyContinue)?.Source
    if (-not $dotnet) {
        Stop-BuildStep 'dotnet not found' '' ''
        throw 'Install the .NET 10 SDK and make sure dotnet is on PATH.'
    }
    $log = Join-Path $logs 'toolchain.log'
    if ((Invoke-BuildTool $dotnet @('--version') $log $root) -ne 0) {
        Stop-BuildStep 'dotnet --version failed' (Get-DiagnosticLog $log) '.'
        throw "dotnet --version failed. See $log"
    }
    $tools = @("dotnet $(@(Read-BuildLog $log | Where-Object { $_ })[0])")

    $iscc = $null
    if ($Platform -eq 'windows' -and $Installer) {
        $iscc = Find-InnoSetupCompiler
        if (-not $iscc) {
            Stop-BuildStep 'Inno Setup 6 not found' '' ''
            throw 'Install Inno Setup 6 or put ISCC.exe on PATH.'
        }
        $tools += 'Inno Setup 6'
    }
    if ($Platform -eq 'macos') {
        $required = @('codesign')
        if ($Installer) { $required += 'create-dmg' }
        foreach ($tool in $required) {
            if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
                Stop-BuildStep "$tool not found" '' ''
                throw "Install $tool (create-dmg: brew install create-dmg; codesign: Xcode Command Line Tools)."
            }
        }
        $tools += $required
    }
    Complete-BuildStep ($tools -join ', ')

    # ── Restore ──────────────────────────────────────────────────────────
    Start-BuildStep 'Restoring packages'
    $log = Join-Path $logs 'restore.log'
    $arguments = @('restore', $project, '-r', $rid, '-tl:off', '-v:minimal') + $publishProperties
    if ((Invoke-BuildTool $dotnet $arguments $log $root $rid) -ne 0) {
        Stop-BuildStep 'failed' (Get-DiagnosticLog $log) ': error |error NU\d+'
        throw "dotnet restore failed. See $log"
    }
    $upToDate = Read-BuildLog $log | Where-Object { $_ -match 'All projects are up-to-date for restore' }
    Complete-BuildStep $(if ($upToDate) { 'up to date' } else { 'restored' })

    # ── Publish ──────────────────────────────────────────────────────────
    Start-BuildStep 'Publishing Native AOT'
    $log = Join-Path $logs 'publish.log'
    $arguments = @('publish', $project, '-c', $Configuration, '-r', $rid, '--no-restore', '-tl:off', '-v:minimal') + $publishProperties
    if ((Invoke-BuildTool $dotnet $arguments $log $root $rid) -ne 0) {
        Stop-BuildStep 'failed' (Get-DiagnosticLog $log) ': error '
        throw "dotnet publish failed. See $log"
    }
    $executable = Join-Path $publishDir $(if ($Platform -eq 'windows') { "$appName.exe" } else { $appName })
    $warnings = @(Read-BuildLog $log | Where-Object { $_ -match ': warning ' } | ForEach-Object { $_.Trim() -replace '\s+\[[^\]]+\]$', '' } | Select-Object -Unique).Count
    $detail = "$(Split-Path -Leaf $executable) $(Format-FileSize $executable)"
    if ($warnings) { $detail += ", $(Format-Count $warnings 'warning' 'warnings')" }
    Complete-BuildStep $detail
    $artifact = $publishDir

    # ── Platform packaging ───────────────────────────────────────────────
    switch ($Platform) {
        'windows' {
            if (-not $Installer) {
                Skip-BuildStep 'Creating installer' 'not requested (make dist)'
                break
            }
            Start-BuildStep 'Creating installer'
            $log = Join-Path $logs 'installer.log'
            $arguments = @("/DMyAppVersion=$version", (Join-Path $root 'Setup.iss'))
            if ((Invoke-BuildTool $iscc $arguments $log $root 'Inno Setup') -ne 0) {
                Stop-BuildStep 'failed' (Get-DiagnosticLog $log) 'Error|error'
                throw "Inno Setup failed. See $log"
            }
            $artifact = Join-Path $publishDir "Setup/$appName Installer.exe"
            Complete-BuildStep "$(Split-Path -Leaf $artifact) $(Format-FileSize $artifact)"
        }
        'macos' {
            $bundle = Join-Path $publishDir "$appName.app"
            Start-BuildStep 'Assembling app bundle'
            if (Test-Path -LiteralPath $bundle) { Remove-Item -LiteralPath $bundle -Recurse -Force }
            $macosDir = New-Item -ItemType Directory -Force -Path (Join-Path $bundle 'Contents/MacOS')
            $resourcesDir = New-Item -ItemType Directory -Force -Path (Join-Path $bundle 'Contents/Resources')
            # Publish output: AOT binary plus native .dylib files; earlier installers stay out.
            $files = @(Get-ChildItem -LiteralPath $publishDir -File | Where-Object Extension -ne '.dmg')
            $files | Copy-Item -Destination $macosDir
            [IO.File]::SetUnixFileMode((Join-Path $macosDir $appName), [IO.UnixFileMode]'UserRead, UserWrite, UserExecute, GroupRead, GroupExecute, OtherRead, OtherExecute')
            # Info.plist versions follow APP_VERSION in the bundle copy.
            $plist = [IO.File]::ReadAllText((Join-Path $root 'Info.plist'))
            $plist = $plist -replace '(<key>CFBundle(ShortVersionString|Version)</key>\s*<string>)[^<]*', "`${1}$version"
            [IO.File]::WriteAllText((Join-Path $bundle 'Contents/Info.plist'), $plist)
            Copy-Item -LiteralPath (Join-Path $root 'images/favicon.icns') -Destination $resourcesDir
            Complete-BuildStep (Format-Count $files.Count 'file' 'files')

            Start-BuildStep 'Signing bundle (ad-hoc)'
            $log = Join-Path $logs 'codesign.log'
            if ((Invoke-BuildTool 'codesign' @('--deep', '--force', '--sign', '-', $bundle) $log $root) -ne 0) {
                Stop-BuildStep 'failed' (Get-DiagnosticLog $log) '.'
                throw "codesign failed. See $log"
            }
            Complete-BuildStep 'ad-hoc'
            $artifact = $bundle

            if (-not $Installer) {
                Skip-BuildStep 'Creating installer' 'not requested (-Installer)'
                break
            }
            Start-BuildStep 'Creating installer'
            $dmg = Join-Path $publishDir "$appName.dmg"
            if (Test-Path -LiteralPath $dmg) { Remove-Item -LiteralPath $dmg -Force }
            $log = Join-Path $logs 'installer.log'
            $arguments = @(
                '--volname', $appName,
                '--volicon', (Join-Path $root 'images/favicon.icns'),
                '--window-pos', '200', '120',
                '--window-size', '560', '400',
                '--icon-size', '128',
                '--icon', "$appName.app", '140', '200',
                '--hide-extension', "$appName.app",
                '--app-drop-link', '420', '200',
                $dmg, $bundle
            )
            if ((Invoke-BuildTool 'create-dmg' $arguments $log $root 'create-dmg') -ne 0) {
                Stop-BuildStep 'failed' (Get-DiagnosticLog $log) '.'
                throw "create-dmg failed. See $log"
            }
            $artifact = $dmg
            Complete-BuildStep "$(Split-Path -Leaf $dmg) $(Format-FileSize $dmg)"
        }
    }

    Write-BuildNote "Output: $artifact"
    Complete-BuildConsole "$appName built for $rid" $logs
} catch {
    Stop-BuildStep '' '' ''
    [Console]::Error.WriteLine("  $($_.Exception.Message)")
    $exitCode = 1
} finally {
    Close-BuildConsole
}
exit $exitCode
