[CmdletBinding()]
param(
    [string]$DotNetPath = 'dotnet',
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe',
    [string]$OutputDirectory = '',
    [switch]$SkipUnity,
    [ValidateRange(30, 7200)][int]$UnityTimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot ('Builds/Validation/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$resultDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$binaryDirectory = Join-Path $resultDirectory 'bin'
$temporaryDirectory = Join-Path $resultDirectory 'temp'
$buildLog = Join-Path $resultDirectory 'dotnet-build.log'
$testLog = Join-Path $resultDirectory 'core-tests.log'
$unityLog = Join-Path $resultDirectory 'unity-editmode.log'
$unityXml = Join-Path $resultDirectory 'unity-editmode.xml'
$summaryPath = Join-Path $resultDirectory 'validation.json'
New-Item -ItemType Directory -Path $resultDirectory, $binaryDirectory, $temporaryDirectory -Force | Out-Null

$summary = [ordered]@{
    StartedUtc = [DateTime]::UtcNow.ToString('o')
    Project = $projectRoot
    Core = 'NotRun'
    CoreChecks = 0
    Unity = $(if ($SkipUnity) { 'SkippedByRequest' } else { 'NotRun' })
    UnityTests = 0
    Passed = $false
    Error = ''
}
$previousTemporary = $env:TEMP
$previousTmp = $env:TMP
try {
    if (!(Test-Path -LiteralPath (Join-Path $projectRoot 'tests/UFN.Tests/UFN.Tests.csproj'))) {
        throw 'Projeto de testes não encontrado. Execute este script a partir de uma cópia completa do projeto.'
    }
    if (!(Get-Command $DotNetPath -ErrorAction SilentlyContinue)) {
        throw 'SDK .NET não encontrado. Instale o SDK .NET 10 ou forneça -DotNetPath.'
    }
    # Profile tests create and replace files; keep their scratch data with this run.
    $env:TEMP = $temporaryDirectory
    $env:TMP = $temporaryDirectory
    Write-Host 'Compilando as verificações C#...'
    & $DotNetPath build (Join-Path $projectRoot 'tests/UFN.Tests/UFN.Tests.csproj') --nologo --configuration Release --output $binaryDirectory 2>&1 | Tee-Object -FilePath $buildLog
    if ($LASTEXITCODE -ne 0) { throw "A compilação C# falhou. Consulte $buildLog" }

    Write-Host 'Executando combate, progressão, perfil e kart...'
    & $DotNetPath (Join-Path $binaryDirectory 'UFN.Tests.dll') $projectRoot all 2>&1 | Tee-Object -FilePath $testLog
    if ($LASTEXITCODE -ne 0) { throw "A suite C# falhou. Consulte $testLog" }
    $testOutput = Get-Content -LiteralPath $testLog -Raw
    if ($testOutput -notmatch 'CORE VALIDATION \(all\): (\d+) checks, 0 failures' -or [int]$Matches[1] -le 0) {
        throw 'O processo terminou sem confirmar uma suite C# completa e aprovada.'
    }
    $summary.Core = 'Passed'
    $summary.CoreChecks = [int]$Matches[1]

    if (!$SkipUnity) {
        if (!(Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
            throw 'Unity não encontrado. Informe -UnityPath ou use -SkipUnity para executar somente C#.'
        }
        # Do not add -quit: the test runner exits after writing its result.
        $unityArguments = @(
            '-batchmode', '-nographics',
            '-projectPath', ('"' + $projectRoot + '"'),
            '-runTests', '-testPlatform', 'EditMode',
            '-testResults', ('"' + $unityXml + '"'),
            '-logFile', ('"' + $unityLog + '"')
        )
        Write-Host 'Executando os testes EditMode no Unity. O projeto deve estar fechado no editor.'
        $editorProcess = Start-Process -FilePath $UnityPath -ArgumentList $unityArguments -PassThru -WindowStyle Hidden
        if (!$editorProcess.WaitForExit($UnityTimeoutSeconds * 1000)) {
            # This is the editor process started by this script, never another open editor.
            $editorProcess.Kill()
            $editorProcess.WaitForExit()
            throw "Unity excedeu o limite de $UnityTimeoutSeconds segundos. Consulte $unityLog"
        }
        $editorProcess.Refresh()
        if ($editorProcess.ExitCode -ne 0) { throw "Unity encerrou com código $($editorProcess.ExitCode). Consulte $unityLog" }
        if (!(Test-Path -LiteralPath $unityXml -PathType Leaf)) { throw 'Unity não produziu um XML de resultados.' }
        [xml]$report = Get-Content -LiteralPath $unityXml -Raw
        $run = $report.'test-run'
        if ($null -eq $run -or [int]$run.total -le 0 -or [int]$run.failed -ne 0 -or [string]$run.result -ne 'Passed') {
            throw 'O XML Unity não confirma uma execução aprovada com testes executados.'
        }
        $summary.Unity = 'Passed'
        $summary.UnityTests = [int]$run.total
    }

    $summary.Passed = $true
    Write-Host "C# aprovado: $($summary.CoreChecks) verificações. Unity: $($summary.Unity)."
    Write-Host "Resultados: $resultDirectory"
}
catch {
    $summary.Error = $_.Exception.Message
    throw
}
finally {
    $env:TEMP = $previousTemporary
    $env:TMP = $previousTmp
    $summary['FinishedUtc'] = [DateTime]::UtcNow.ToString('o')
    $summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
}
