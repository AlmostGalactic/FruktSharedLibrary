# Builds the asset bundle the self-test loads, and copies it to FRUKT/UserData/FruktSharedLibrary.testbundle.
# Needs Unity 6000.3.18f1 (the game's version). The first run creates a URP project, which takes a few minutes.
#
#   powershell -File tools\build-test-bundle.ps1 -Unity "C:\path\to\6000.3.18f1\Editor\Unity.exe" -GameDir "C:\path\to\FRUKT"
param(
    [Parameter(Mandatory = $true)][string]$Unity,
    [string]$GameDir = "D:\SteamLibrary\steamapps\common\FRUKT",
    [string]$ProjectDir = (Join-Path $env:TEMP "FruktSharedLibrary.TestBundleProject")
)
$ErrorActionPreference = "Stop"
$editorDir = Split-Path $Unity
$template = Get-ChildItem (Join-Path $editorDir "Data\Resources\PackageManager\ProjectTemplates") -Filter "com.unity.template.3d-cross-platform-*.tgz" | Select-Object -First 1

if (-not (Test-Path (Join-Path $ProjectDir "Assets"))) {
    if (-not $template) { throw "The URP '3D cross-platform' project template isn't in this Unity install." }
    "Creating the Unity project (first run only)..."
    & $Unity -batchmode -nographics -quit -createProject $ProjectDir -cloneFromTemplate $template.FullName -logFile "$ProjectDir.create.log" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Creating the project failed; see $ProjectDir.create.log" }
}

$editorScripts = Join-Path $ProjectDir "Assets\Editor"
New-Item -ItemType Directory -Force $editorScripts | Out-Null
Copy-Item (Join-Path $PSScriptRoot "TestBundle\Editor\BuildTestBundle.cs") $editorScripts -Force

"Building the bundle..."
& $Unity -batchmode -nographics -quit -projectPath $ProjectDir -executeMethod BuildTestBundle.Build -logFile "$ProjectDir.build.log" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Building the bundle failed; see $ProjectDir.build.log" }

$bundle = Join-Path $ProjectDir "BundleOut\fsltest.bundle"
Copy-Item $bundle (Join-Path $GameDir "UserData\FruktSharedLibrary.testbundle") -Force
"Built and copied to $GameDir\UserData\FruktSharedLibrary.testbundle"
