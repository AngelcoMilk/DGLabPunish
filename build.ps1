param(
    [string]$Configuration = "Debug",
    [string]$GameDir = "D:\SteamLibrary\steamapps\common\REPO",
    [string]$R2Profile = "$env:APPDATA\r2modmanPlus-local\REPO\profiles\REPO",
    [switch]$InstallToProfile,
    [switch]$PackageToDesktop
)

$ErrorActionPreference = "Stop"

$modName = "DGLabPunish"
$modVersion = "0.6.0"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src\$modName"
$distRoot = Join-Path $root "dist"
$outDir = Join-Path $distRoot "BepInEx\plugins\$modName"
$managed = Join-Path $GameDir "REPO_Data\Managed"
$bepCore = Join-Path $R2Profile "BepInEx\core"
$pluginOut = Join-Path $outDir "$modName.dll"
$desktopZip = Join-Path ([Environment]::GetFolderPath("Desktop")) "$modName-$modVersion.zip"
$qrDll = Join-Path $root "lib\QrCodeGenerator.dll"

if (!(Test-Path (Join-Path $managed "Assembly-CSharp.dll"))) { throw "Assembly-CSharp.dll not found under $managed" }
if (!(Test-Path (Join-Path $bepCore "BepInEx.dll"))) { throw "BepInEx core not found under $bepCore" }
if (!(Test-Path $src)) { throw "Source directory not found at $src" }
if (!(Test-Path $qrDll)) { throw "QrCodeGenerator.dll not found at $qrDll" }

if (Test-Path $distRoot) {
    $resolvedDist = (Resolve-Path -LiteralPath $distRoot).Path
    $resolvedRoot = (Resolve-Path -LiteralPath $root).Path
    if (!$resolvedDist.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean dist outside workspace: $resolvedDist"
    }
    Remove-Item -LiteralPath $distRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (!(Test-Path $csc)) { throw "No .NET Framework csc.exe found." }

$sources = Get-ChildItem -LiteralPath $src -Filter "*.cs" -Recurse | ForEach-Object { $_.FullName }

$refs = @(
    (Join-Path $bepCore "BepInEx.dll"),
    (Join-Path $bepCore "0Harmony.dll"),
    (Join-Path $managed "Assembly-CSharp.dll"),
    (Join-Path $managed "UnityEngine.dll"),
    (Join-Path $managed "UnityEngine.CoreModule.dll"),
    (Join-Path $managed "UnityEngine.IMGUIModule.dll"),
    (Join-Path $managed "UnityEngine.InputLegacyModule.dll"),
    (Join-Path $managed "UnityEngine.TextRenderingModule.dll"),
    (Join-Path $managed "PhotonUnityNetworking.dll"),
    (Join-Path $managed "PhotonRealtime.dll"),
    (Join-Path $managed "Photon3Unity3D.dll"),
    (Join-Path $managed "websocket-sharp.dll"),
    (Join-Path $managed "Newtonsoft.Json.dll"),
    (Join-Path $managed "netstandard.dll"),
    $qrDll
)

$refArgs = $refs | ForEach-Object { "/reference:$_" }

function Test-GameHookTargets {
    param([string]$AssemblyPath, [string]$CecilPath)

    if (!(Test-Path $CecilPath)) {
        Write-Warning "Mono.Cecil.dll not found; skipping Harmony hook target validation."
        return
    }

    Add-Type -Path $CecilPath
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($AssemblyPath)
    $targets = @(
        @{ Type = "PlayerHealth"; Method = "Hurt"; Parameters = @("System.Int32", "System.Boolean", "System.Int32", "System.Boolean") },
        @{ Type = "PlayerAvatar"; Method = "PlayerDeathRPC"; Parameters = @("System.Int32", "Photon.Pun.PhotonMessageInfo") },
        @{ Type = "PlayerAvatar"; Method = "Footstep"; Parameters = @("Materials/SoundType", "UnityEngine.Vector3") },
        @{ Type = "PlayerAvatar"; Method = "Jump"; Parameters = @("System.Boolean") },
        @{ Type = "PlayerAvatar"; Method = "Land"; Parameters = @() },
        @{ Type = "PlayerAvatar"; Method = "Slide"; Parameters = @() },
        @{ Type = "PlayerAvatarVisuals"; Method = "FootstepLight"; Parameters = @() },
        @{ Type = "PlayerAvatarVisuals"; Method = "FootstepMedium"; Parameters = @() },
        @{ Type = "PlayerAvatarVisuals"; Method = "FootstepHeavy"; Parameters = @() },
        @{ Type = "PlayerAvatarVisuals"; Method = "LeftFootDown"; Parameters = @() },
        @{ Type = "PlayerAvatarVisuals"; Method = "RightFootDown"; Parameters = @() },
        @{ Type = "PlayerAvatarVisuals"; Method = "LeftFootDownSlow"; Parameters = @() },
        @{ Type = "PlayerAvatarVisuals"; Method = "RightFootDownSlow"; Parameters = @() },
        @{ Type = "PlayerController"; Method = "Update"; Parameters = @() },
        @{ Type = "EnemyHunterAnim"; Method = "FootstepShort"; Parameters = @() },
        @{ Type = "EnemyHunterAnim"; Method = "FootstepLong"; Parameters = @() }
    )

    $errors = New-Object System.Collections.Generic.List[string]
    foreach ($target in $targets) {
        $type = $assembly.MainModule.Types | Where-Object { $_.Name -eq $target.Type -or $_.FullName -eq $target.Type } | Select-Object -First 1
        if ($null -eq $type) {
            $errors.Add("Missing type: $($target.Type)")
            continue
        }

        $methods = @($type.Methods | Where-Object { $_.Name -eq $target.Method })
        if ($methods.Count -eq 0) {
            $errors.Add("Missing method: $($target.Type).$($target.Method)")
            continue
        }

        $expectedParameters = @($target.Parameters)
        $hasCompatibleSignature = $false
        foreach ($method in $methods) {
            if ($method.Parameters.Count -ne $expectedParameters.Count) { continue }
            $allParametersMatch = $true
            for ($i = 0; $i -lt $expectedParameters.Count; $i++) {
                if ($method.Parameters[$i].ParameterType.FullName -ne $expectedParameters[$i]) {
                    $allParametersMatch = $false
                    break
                }
            }
            if ($allParametersMatch) {
                $hasCompatibleSignature = $true
                break
            }
        }

        if (!$hasCompatibleSignature) {
            $errors.Add("Incompatible signature: $($target.Type).$($target.Method)($($expectedParameters -join ', '))")
        }
    }

    if ($errors.Count -gt 0) { throw "Harmony hook target validation failed:`n$($errors -join "`n")" }
    Write-Host "Validated Harmony hook targets against $AssemblyPath"
}

Test-GameHookTargets -AssemblyPath (Join-Path $managed "Assembly-CSharp.dll") -CecilPath (Join-Path $bepCore "Mono.Cecil.dll")

& $csc /nologo /codepage:65001 /target:library /optimize+ /debug:full /nowarn:1701 /out:$pluginOut $refArgs $sources
if ($LASTEXITCODE -ne 0) { throw "csc.exe failed with exit code $LASTEXITCODE" }

Copy-Item -LiteralPath $qrDll -Destination (Join-Path $outDir "QrCodeGenerator.dll") -Force

Copy-Item -LiteralPath (Join-Path $root "package\manifest.json") -Destination (Join-Path $distRoot "manifest.json") -Force
Copy-Item -LiteralPath (Join-Path $root "package\README.md") -Destination (Join-Path $distRoot "README.md") -Force
Copy-Item -LiteralPath (Join-Path $root "package\README_EN.md") -Destination (Join-Path $distRoot "README_EN.md") -Force
Copy-Item -LiteralPath (Join-Path $root "package\icon.png") -Destination (Join-Path $distRoot "icon.png") -Force

$profilePluginDir = Join-Path $R2Profile "BepInEx\plugins\$modName"
if ($InstallToProfile -and (Test-Path (Join-Path $R2Profile "BepInEx"))) {
    New-Item -ItemType Directory -Force -Path $profilePluginDir | Out-Null
    Copy-Item -LiteralPath $pluginOut -Destination (Join-Path $profilePluginDir "$modName.dll") -Force
    Copy-Item -LiteralPath $qrDll -Destination (Join-Path $profilePluginDir "QrCodeGenerator.dll") -Force
    Write-Host "Installed to $profilePluginDir"
}

if ($PackageToDesktop) {
    if (Test-Path $desktopZip) { Remove-Item -LiteralPath $desktopZip -Force }

    $packageStage = Join-Path $root "dist_package"
    if (Test-Path $packageStage) {
        $resolvedStage = (Resolve-Path -LiteralPath $packageStage).Path
        $resolvedRoot = (Resolve-Path -LiteralPath $root).Path
        if (!$resolvedStage.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean dist_package outside workspace: $resolvedStage"
        }
        Remove-Item -LiteralPath $packageStage -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path (Join-Path $packageStage "BepInEx\plugins\$modName") | Out-Null
    Copy-Item -LiteralPath (Join-Path $distRoot "manifest.json") -Destination (Join-Path $packageStage "manifest.json") -Force
    Copy-Item -LiteralPath (Join-Path $distRoot "README.md") -Destination (Join-Path $packageStage "README.md") -Force
    Copy-Item -LiteralPath (Join-Path $distRoot "README_EN.md") -Destination (Join-Path $packageStage "README_EN.md") -Force
    Copy-Item -LiteralPath (Join-Path $distRoot "icon.png") -Destination (Join-Path $packageStage "icon.png") -Force
    Copy-Item -LiteralPath $pluginOut -Destination (Join-Path $packageStage "BepInEx\plugins\$modName\$modName.dll") -Force
    Copy-Item -LiteralPath $qrDll -Destination (Join-Path $packageStage "BepInEx\plugins\$modName\QrCodeGenerator.dll") -Force

    Compress-Archive -LiteralPath `
        (Join-Path $packageStage "manifest.json"), `
        (Join-Path $packageStage "README.md"), `
        (Join-Path $packageStage "README_EN.md"), `
        (Join-Path $packageStage "icon.png"), `
        (Join-Path $packageStage "BepInEx") `
        -DestinationPath $desktopZip

    Write-Host "Packaged $desktopZip"
}

Write-Host "Built $pluginOut"
