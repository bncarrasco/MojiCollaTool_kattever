[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$userDotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
$pathDotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
$dotnet = @($userDotnet, $pathDotnet) |
    Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
    Select-Object -First 1

if (-not $dotnet) {
    throw 'No .NET SDK host was found. Install the required .NET SDK before running this script.'
}

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'MojiCollaTool-dotnet-cli'
$env:NUGET_PACKAGES = Join-Path $env:TEMP 'MojiCollaTool-nuget'

Push-Location $repoRoot
try {
    $actualSdkVersion = (& $dotnet --version).Trim()
    if ($actualSdkVersion -ne '6.0.428') {
        throw "Expected .NET SDK 6.0.428 from global.json, but found $actualSdkVersion."
    }
    Write-Output "Using .NET SDK $actualSdkVersion from global.json."

    & $dotnet test 'MojiCollaTool/MojiCollaTool.sln' --configuration Debug --nologo --logger 'console;verbosity=detailed'
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

exit $exitCode
