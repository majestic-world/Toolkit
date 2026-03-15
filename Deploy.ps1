Write-Host "Building L2 Toolkit..." -ForegroundColor Cyan
dotnet publish -r win-x64 -c Release -p:PublishAot=true -p:OptimizationPreference=Speed -p:StackTraceSupport=false -p:InvariantGlobalization=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

Write-Host "Running Inno Setup..." -ForegroundColor Cyan
& "C:\Users\Mk\AppData\Local\Programs\Inno Setup 6\ISCC.exe" "$PSScriptRoot\Setup.iss"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Inno Setup failed." -ForegroundColor Red
    exit 1
}

$installer = "$PSScriptRoot\bin\Release\net10.0\win-x64\publish\Setup\L2 Toolkit Installer.exe"
Write-Host "Launching installer..." -ForegroundColor Green
Start-Process $installer
