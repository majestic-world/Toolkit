#Requires -Version 5.1
# Console output for scripts/build.ps1: one line per finished step, a spinner with the
# live status of the running tool, and an error excerpt with the log path on failure.
# Tool output always goes to log files. When stdout is redirected (IDE, CI, another
# script capturing output) the same steps print as plain lines.

$script:State = $null

function Initialize-BuildConsole([string]$Title, [string]$Root) {
    $interactive = -not [Console]::IsOutputRedirected
    $unicode = $interactive -and ($env:WT_SESSION -or $env:TERM_PROGRAM)
    # Glyphs by code point: Windows PowerShell reads BOM-less scripts as ANSI.
    $script:State = [pscustomobject]@{
        Interactive    = $interactive
        Frames         = if ($unicode) { @(0x280B, 0x2819, 0x2839, 0x2838, 0x283C, 0x2834, 0x2826, 0x2827, 0x2807, 0x280F | ForEach-Object { [string][char]$_ }) } else { @('|', '/', '-', '\') }
        Ok             = if ($unicode) { [string][char]0x2714 } else { '+' }
        Fail           = if ($unicode) { [string][char]0x2716 } else { 'x' }
        Skip           = if ($unicode) { [string][char]0x00B7 } else { '-' }
        Ellipsis       = if ($unicode) { [string][char]0x2026 } else { '~' }
        Started        = [Diagnostics.Stopwatch]::StartNew()
        Step           = $null
        StepClock      = $null
        Encoding       = [Console]::OutputEncoding
        Root           = $Root
    }
    if ($unicode) { [Console]::OutputEncoding = [Text.Encoding]::UTF8 }
    Write-Host ''
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host ''
}

function Close-BuildConsole {
    if (-not $script:State) { return }
    if ($script:State.Interactive) { try { [Console]::CursorVisible = $true } catch { } }
    [Console]::OutputEncoding = $script:State.Encoding
}

# Elapsed time as "850ms", "12.4s" or "1m 05s".
function Format-BuildDuration([TimeSpan]$Time) {
    $culture = [Globalization.CultureInfo]::InvariantCulture
    if ($Time.TotalSeconds -lt 1) { return [string]::Format($culture, '{0}ms', [int]$Time.TotalMilliseconds) }
    if ($Time.TotalSeconds -lt 60) { return [string]::Format($culture, '{0:0.0}s', $Time.TotalSeconds) }
    return [string]::Format($culture, '{0}m {1:00}s', [int][Math]::Floor($Time.TotalMinutes), $Time.Seconds)
}

function Clear-SpinnerLine {
    if (-not $script:State.Interactive) { return }
    $width = [Math]::Max(1, [Console]::WindowWidth - 1)
    [Console]::Write("`r" + (' ' * $width) + "`r")
}

# Interactive output shows the running step until its result line replaces it;
# plain output (redirected stdout) prints only the result line.
function Start-BuildStep([string]$Title) {
    $script:State.Step = $Title
    $script:State.StepClock = [Diagnostics.Stopwatch]::StartNew()
    if ($script:State.Interactive) {
        Write-Host "  $($script:State.Frames[0]) $Title" -ForegroundColor Cyan -NoNewline
    }
}

function Write-StepResult([string]$Mark, [ConsoleColor]$Color, [string]$Detail) {
    Clear-SpinnerLine
    $elapsed = Format-BuildDuration $script:State.StepClock.Elapsed
    Write-Host "  $Mark " -ForegroundColor $Color -NoNewline
    Write-Host ($script:State.Step.PadRight(34)) -NoNewline
    if ($Detail) { Write-Host "$Detail  " -ForegroundColor Gray -NoNewline }
    Write-Host $elapsed -ForegroundColor DarkGray
    $script:State.Step = $null
}

function Complete-BuildStep([string]$Detail) { Write-StepResult $script:State.Ok Green $Detail }

function Skip-BuildStep([string]$Title, [string]$Reason) {
    Start-BuildStep $Title
    Clear-SpinnerLine
    Write-Host "  $($script:State.Skip) " -ForegroundColor DarkGray -NoNewline
    Write-Host ($Title.PadRight(34)) -ForegroundColor DarkGray -NoNewline
    Write-Host $Reason -ForegroundColor DarkGray
    $script:State.Step = $null
}

# Marks the running step as failed and shows the matching lines of its log, without
# MSBuild's trailing [project]. The terminal gets paths relative to the repository
# root; plain output keeps them absolute so an IDE can open the file.
function Stop-BuildStep([string]$Detail, [string]$Log, [string]$ErrorPattern) {
    if (-not $script:State -or -not $script:State.Step) { return }
    Write-StepResult $script:State.Fail Red $Detail
    if ($Log -and (Test-Path -LiteralPath $Log)) {
        $separator = [IO.Path]::DirectorySeparatorChar
        $root = if ($script:State.Interactive -and $script:State.Root) { [regex]::Escape($script:State.Root.TrimEnd($separator) + $separator) } else { $null }
        $lines = @(Read-BuildLog $Log | Where-Object { $_ -match $ErrorPattern } | ForEach-Object {
            $line = $_.Trim() -replace '\s+\[[^\]]+\]$', ''
            if ($root) { $line -replace "^$root", '' } else { $line }
        } | Select-Object -Unique | Select-Object -First 15)
        foreach ($line in $lines) { Write-Host "      $line" -ForegroundColor Red }
        Write-Host "      Log: $Log" -ForegroundColor DarkGray
    }
}

# "1 warning", "2 warnings".
function Format-Count([int]$Count, [string]$Singular, [string]$Plural) {
    if ($Count -eq 1) { return "1 $Singular" }
    return "$Count $Plural"
}

function Write-BuildNote([string]$Text) {
    Clear-SpinnerLine
    Write-Host "    $Text" -ForegroundColor DarkGray
}

function Complete-BuildConsole([string]$Summary, [string]$LogDirectory) {
    Write-Host ''
    Write-Host "  $($script:State.Ok) $Summary " -ForegroundColor Green -NoNewline
    Write-Host "in $(Format-BuildDuration $script:State.Started.Elapsed)" -ForegroundColor DarkGray
    if ($LogDirectory) { Write-Host "    Logs: $LogDirectory" -ForegroundColor DarkGray }
    Write-Host ''
}

# Reads a log another process may still be writing, as UTF-8 or the ANSI code page.
function Read-BuildLog([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        $bytes = New-Object byte[] $stream.Length
        [void]$stream.Read($bytes, 0, $bytes.Length)
    } finally { $stream.Dispose() }
    try { $text = (New-Object Text.UTF8Encoding($false, $true)).GetString($bytes) }
    catch { $text = [Text.Encoding]::Default.GetString($bytes) }
    return $text -split "`r?`n"
}

# What the tool is doing now, from the tail of its growing log: the MSBuild output
# of dotnet restore/publish ("Project -> file", ILC native code generation) or the
# file Inno Setup is compressing.
function Get-LogStatus([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return '' }
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        $count = [int][Math]::Min(8192, $stream.Length)
        [void]$stream.Seek(-$count, [IO.SeekOrigin]::End)
        $bytes = New-Object byte[] $count
        [void]$stream.Read($bytes, 0, $count)
    } finally { $stream.Dispose() }
    $lines = [Text.Encoding]::Default.GetString($bytes) -split "`r?`n"
    for ($i = $lines.Count - 1; $i -ge 0; $i--) {
        $line = $lines[$i].Trim()
        if ($line -match '^([\w.\-]+) -> (.+)$') { return "$($Matches[1]) -> $(Split-Path -Leaf $Matches[2].TrimEnd('\', '/'))" }
        if ($line -match '^Generating native code') { return 'generating native code' }
        if ($line -match '^Restored (.+?)( \(in .+\))?\.?$') { return "restored $(Split-Path -Leaf $Matches[1])" }
        if ($line -match '^Determining projects to restore') { return 'resolving packages' }
        if ($line -match '^Compressing: (.+)$') { return "compressing $(Split-Path -Leaf $Matches[1])" }
        if ($line -match '^(Compiling|Reading|Preprocessing|Parsing) .+') { return $Matches[0] }
    }
    return ''
}

function ConvertTo-ArgumentString([string[]]$Arguments) {
    return (@($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
    }) -join ' ')
}

# Runs a tool with stdout in $Log and a spinner showing its latest log line.
# Returns the exit code. Standard input is empty, so console prompts take their default.
function Invoke-BuildTool([string]$FilePath, [string[]]$Arguments, [string]$Log, [string]$WorkingDirectory, [string]$Label) {
    $errorLog = "$Log.stderr"
    $emptyInput = [IO.Path]::GetTempFileName()
    try {
        $process = Start-Process -FilePath $FilePath -ArgumentList (ConvertTo-ArgumentString $Arguments) -WorkingDirectory $WorkingDirectory `
            -NoNewWindow -PassThru -RedirectStandardOutput $Log -RedirectStandardError $errorLog -RedirectStandardInput $emptyInput
        $null = $process.Handle  # Keeps ExitCode available after the process exits.
        if ($script:State.Interactive) {
            try { [Console]::CursorVisible = $false } catch { }
            $frame = 0
            $status = ''
            while (-not $process.WaitForExit(100)) {
                if ($frame % 3 -eq 0) { $status = Get-LogStatus $Log }
                $glyph = $script:State.Frames[$frame % $script:State.Frames.Count]
                $elapsed = Format-BuildDuration $script:State.StepClock.Elapsed
                $width = [Math]::Max(20, [Console]::WindowWidth - 1)
                $head = "  $glyph $($script:State.Step)"
                if ($Label) { $head += "  $Label" }
                $room = $width - $head.Length - $elapsed.Length - 4
                $tail = ''
                if ($room -gt 8 -and $status) {
                    $tail = if ($status.Length -gt $room) { $status.Substring(0, $room - 1) + $script:State.Ellipsis } else { $status }
                }
                $middle = ("  $tail").PadRight([Math]::Max(0, $width - $head.Length - $elapsed.Length))
                [Console]::Write("`r")
                Write-Host $head -ForegroundColor Cyan -NoNewline
                Write-Host $middle -ForegroundColor DarkGray -NoNewline
                Write-Host $elapsed -ForegroundColor DarkGray -NoNewline
                $frame++
            }
            try { [Console]::CursorVisible = $true } catch { }
        }
        $process.WaitForExit()
        if ((Test-Path -LiteralPath $errorLog) -and (Get-Item -LiteralPath $errorLog).Length -eq 0) { Remove-Item -LiteralPath $errorLog }
        return $process.ExitCode
    } finally {
        Remove-Item -LiteralPath $emptyInput -ErrorAction SilentlyContinue
    }
}

Export-ModuleMember -Function Initialize-BuildConsole, Close-BuildConsole, Format-BuildDuration, Start-BuildStep, Complete-BuildStep,
    Skip-BuildStep, Stop-BuildStep, Write-BuildNote, Complete-BuildConsole, Read-BuildLog, Invoke-BuildTool, Format-Count
