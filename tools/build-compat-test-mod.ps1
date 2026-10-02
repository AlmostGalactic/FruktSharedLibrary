# Builds the compatibility test mod and puts it in FRUKT/Mods. It's built against a fake future version of the
# library, so the self-test can check that the real library refuses to start it. Remove it again with -Remove.
#
#   powershell -File tools\build-compat-test-mod.ps1 -GameDir "C:\path\to\FRUKT"
param(
    [string]$GameDir = "D:\SteamLibrary\steamapps\common\FRUKT",
    [switch]$Remove
)
$ErrorActionPreference = "Stop"
$installed = Join-Path $GameDir "Mods\CompatTestMod.dll"
if ($Remove) {
    if (Test-Path $installed) { Remove-Item -LiteralPath $installed }
    "Removed $installed"
    return
}
$project = Join-Path $PSScriptRoot "CompatTestMod\CompatTestMod.csproj"
dotnet build $project -c Release -nologo -v q "-p:FruktGameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "Building the test mod failed." }
Copy-Item (Join-Path $PSScriptRoot "CompatTestMod\bin\Release\net6.0\CompatTestMod.dll") $installed -Force
"Installed $installed"
