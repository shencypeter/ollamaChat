$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Actual, [object]$Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$Message`nExpected: $Expected`nActual: $Actual"
    }
}

function Assert-Sequence([string[]]$Actual, [string[]]$Expected, [string]$Message) {
    Assert-Equal ($Actual -join '|') ($Expected -join '|') $Message
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$fixtureRoot = Join-Path $repositoryRoot '.codex-build\build-pipeline-test'
$projectRoot = Join-Path $fixtureRoot 'project'
$fakeBin = Join-Path $fixtureRoot 'bin'
$commandLog = Join-Path $fixtureRoot 'npm-commands.log'
$originalPath = $env:PATH
$dotnetCommand = (Get-Command dotnet -ErrorAction Stop).Source

try {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path (Join-Path $projectRoot 'ClientApp') -Force | Out-Null
    New-Item -ItemType Directory -Path $fakeBin -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'AiTeachingAssistant.csproj') -Destination (Join-Path $projectRoot 'AiTeachingAssistant.csproj')
    Set-Content -LiteralPath (Join-Path $projectRoot 'ClientApp\package-lock.json') -Value '{"lockfileVersion":3}' -Encoding UTF8

    $fakeNpm = @'
@echo off
echo %*>>"%NPM_TEST_LOG%"
if "%1"=="ci" (
  if not exist node_modules mkdir node_modules
  copy /y package-lock.json node_modules\.package-lock.json >nul
)
if "%1"=="run" if "%2"=="build" (
  if exist fail-build exit 9
  if not exist ..\wwwroot\portal mkdir ..\wwwroot\portal
  echo built>..\wwwroot\portal\index.html
)
exit /b 0
'@
    Set-Content -LiteralPath (Join-Path $fakeBin 'npm.cmd') -Value $fakeNpm -Encoding ASCII
    $env:PATH = "$fakeBin;$originalPath"
    $env:NPM_TEST_LOG = $commandLog

    function Invoke-Target(
        [string]$Target,
        [bool]$SkipFrontend = $false,
        [bool]$ExpectFailure = $false,
        [string]$VisualStudioNpmExecutable = ''
    ) {
        $skipValue = if ($SkipFrontend) { 'true' } else { 'false' }
        $arguments = @(
            'msbuild'
            (Join-Path $projectRoot 'AiTeachingAssistant.csproj')
            "-t:$Target"
            "-p:SkipFrontendBuild=$skipValue"
            '-v:q'
            '-nologo'
            '-nr:false'
        )
        if ($VisualStudioNpmExecutable) {
            $arguments += "-p:VisualStudioNpmExecutable=$VisualStudioNpmExecutable"
        }
        & $dotnetCommand @arguments
        $exitCode = $LASTEXITCODE
        if ($ExpectFailure) {
            if ($exitCode -eq 0) { throw "Expected target $Target to fail." }
        } elseif ($exitCode -ne 0) {
            throw "Target $Target failed with exit code $exitCode."
        }
    }

    function Read-Commands {
        if (-not (Test-Path -LiteralPath $commandLog)) { return @() }
        return @(Get-Content -LiteralPath $commandLog | Where-Object { $_ -ne '' })
    }

    function Clear-Commands {
        Remove-Item -LiteralPath $commandLog -Force -ErrorAction SilentlyContinue
    }

    $fastCheckOutput = @(& $dotnetCommand msbuild (Join-Path $projectRoot 'AiTeachingAssistant.csproj') '-getProperty:DisableFastUpToDateCheck' '-nologo' '-nr:false')
    if ($LASTEXITCODE -ne 0) { throw 'Could not evaluate DisableFastUpToDateCheck.' }
    Assert-Equal (($fastCheckOutput -join '').Trim()) 'true' 'Visual Studio must not bypass MSBuild after Vue-only edits.'

    Invoke-Target 'BuildVue'
    Assert-Sequence (Read-Commands) @('ci --no-audit --no-fund', 'run build') 'A clean build must restore dependencies before building Vue.'

    Clear-Commands
    Invoke-Target 'BuildVue'
    Assert-Sequence (Read-Commands) @('run build') 'An unchanged lockfile must skip npm ci but still build Vue.'

    Clear-Commands
    $lockfile = Get-Item -LiteralPath (Join-Path $projectRoot 'ClientApp\package-lock.json')
    $lockfile.LastWriteTime = (Get-Date).AddSeconds(2)
    Invoke-Target 'BuildVue'
    Assert-Sequence (Read-Commands) @('ci --no-audit --no-fund', 'run build') 'A newer lockfile must restore dependencies again before building Vue.'

    Clear-Commands
    Invoke-Target 'BuildVue' $true
    Assert-Equal (Read-Commands).Count 0 'SkipFrontendBuild=true must invoke no npm commands.'

    Clear-Commands
    Invoke-Target 'BuildVueForPublish'
    Assert-Sequence (Read-Commands) @('run build') 'The publish collection target must reuse BuildVue and invoke the Vue build once.'

    Clear-Commands
    New-Item -ItemType File -Path (Join-Path $projectRoot 'ClientApp\fail-build') -Force | Out-Null
    Invoke-Target 'BuildVue' $false $true
    Assert-Sequence (Read-Commands) @('run build') 'A failed npm build must be attempted exactly once and fail MSBuild.'

    Remove-Item -LiteralPath (Join-Path $projectRoot 'ClientApp\fail-build') -Force
    Clear-Commands
    $visualStudioNpm = Join-Path $fixtureRoot 'Visual Studio NodeJs\npm.cmd'
    New-Item -ItemType Directory -Path (Split-Path $visualStudioNpm) -Force | Out-Null
    Move-Item -LiteralPath (Join-Path $fakeBin 'npm.cmd') -Destination $visualStudioNpm
    Set-Content -LiteralPath $visualStudioNpm -Value "@echo off`r`nnode %*" -Encoding ASCII
    Set-Content -LiteralPath (Join-Path (Split-Path $visualStudioNpm) 'node.cmd') -Value "@echo off`r`necho %*>>`"%NPM_TEST_LOG%`"" -Encoding ASCII
    $env:PATH = 'C:\Windows\System32;C:\Windows'
    Invoke-Target 'BuildVue' $false $false $visualStudioNpm
    Assert-Sequence (Read-Commands) @('run build') 'Visual Studio bundled npm must expose its adjacent node executable to package scripts.'

    'Build pipeline behavior tests passed.'
}
finally {
    $env:PATH = $originalPath
    Remove-Item Env:NPM_TEST_LOG -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
