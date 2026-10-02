[CmdletBinding()]
param(
    [ValidateSet('Portal', 'Subscription', 'WithIAM')]
    [string]$Demo = 'Portal',
    [switch]$Reset,
    [switch]$NoSeed,
    [switch]$NoRun,
    [string]$ConnectionString
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this with PowerShell 7 (pwsh), not Windows PowerShell.'
}
$ProgressPreference = 'SilentlyContinue'

$demos = @{
    Portal = @{ ConnectionKey = 'Billing'; Port = 7110; Seeds = $true; SignIn = 'No sign in; the demo account is fixed' }
    Subscription = @{ ConnectionKey = 'Billing'; Port = 7111; Seeds = $false; SignIn = 'No sign in; subscribe, visit the members area, cancel' }
    WithIAM = @{ ConnectionKey = 'Default'; Port = 7112; Seeds = $true; SignIn = 'olivia, carla or bobby / Test1234' }
}
$config = $demos[$Demo]
$repoRoot = $PSScriptRoot
$demoProject = Join-Path $repoRoot "Corely.Billing.Demos.$Demo"
$cliProject = Join-Path $repoRoot 'Corely.Billing.DataAccessMigrations.Cli'
$iamTool = Join-Path $HOME '.dotnet' 'tools' 'corely-iam-db'
$appUrl = "https://localhost:$($config.Port)"
$legacyHistoryTable = '__EFMigrationsHistory'

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked([string]$What, [scriptblock]$Command) {
    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed:`n$($output -join "`n")"
    }
    return $output
}

function Test-PortListening([int]$Port) {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        return $client.ConnectAsync('127.0.0.1', $Port).Wait(250)
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

$connection = $ConnectionString
if ([string]::IsNullOrWhiteSpace($connection)) {
    $settings = Get-Content (Join-Path $demoProject 'appsettings.Development.json') -Raw | ConvertFrom-Json
    $connection = $settings.ConnectionStrings.($config.ConnectionKey)
}
$appArguments = @()
if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
    $appArguments = @("--ConnectionStrings:$($config.ConnectionKey)=$ConnectionString")
}

if (Test-PortListening $config.Port) {
    throw "Port $($config.Port) is already in use; the $Demo demo is probably still running, and it would also lock the files the build needs. Stop it and run this again."
}

if ($connection -match '\(localdb\)\\([^;]+)') {
    $instance = $Matches[1]
    Write-Step "Starting LocalDB instance $instance"
    if (Get-Command sqllocaldb -ErrorAction SilentlyContinue) {
        sqllocaldb start $instance | Out-Null
    }
    else {
        Write-Warning 'sqllocaldb was not found; assuming the instance is already running.'
    }
}

Write-Step "Building the migration tool and the $Demo demo"
foreach ($project in $cliProject, $demoProject) {
    Invoke-Checked "Building $(Split-Path $project -Leaf)" { dotnet build $project -v q -nologo } | Out-Null
}

function Invoke-BillingDb([string[]]$Arguments) {
    $output = dotnet run --project $cliProject --no-build -- db @Arguments -p MsSql -c $connection 2>&1
    return ($output | ForEach-Object { "$_" })
}

function Invoke-IamDb([string[]]$Arguments) {
    $output = & $iamTool db @Arguments -p MsSql -c $connection 2>&1
    return ($output | ForEach-Object { "$_" })
}

if ($Reset) {
    Write-Step 'Dropping the database (-Reset)'
    Invoke-BillingDb @('drop', '--force') | Out-Null
}

if ($Demo -eq 'WithIAM') {
    Write-Step 'Installing or updating the IAM migration tool (corely-iam-db 3.x)'
    Invoke-Checked 'Installing corely-iam-db' { dotnet tool update --global Corely.IAM.DataAccessMigrations.Cli --version '3.*' } | Out-Null

    Write-Step 'Applying the IAM schema'
    $historyArguments = @()
    if ((Invoke-IamDb @('test-connection')) -match 'Successfully connected') {
        if (-not ((Invoke-IamDb @('list')) -match '\[Applied\]') -and ((Invoke-IamDb @('list', '--history-table', $legacyHistoryTable)) -match '\[Applied\]')) {
            Write-Host "    IAM migration history found in $legacyHistoryTable; using it."
            $historyArguments = @('--history-table', $legacyHistoryTable)
        }
    }
    $output = Invoke-IamDb (@('create') + $historyArguments)
    if (-not ($output -match 'migrations applied successfully')) {
        throw "Applying the IAM schema failed:`n$($output -join "`n")"
    }
}

Write-Step 'Applying the Billing schema'
$output = Invoke-BillingDb @('create')
if (-not ($output -match 'migrations applied successfully')) {
    throw "Applying the Billing schema failed:`n$($output -join "`n")"
}

if ($NoSeed -or -not $config.Seeds) {
    Write-Step 'No demo seed'
}
else {
    Write-Step 'Seeding the demo data (skipped if it is already there)'
    $output = Invoke-Checked 'Seeding' { dotnet run --project $demoProject --no-build -- @appArguments --seed }
    $output | Where-Object { "$_" -match 'seed|Nothing' } | ForEach-Object { Write-Host "    $_" }
}

Write-Host ''
Write-Host "The $Demo demo is ready." -ForegroundColor Green
Write-Host "  Open:     $appUrl"
Write-Host "  Sign in:  $($config.SignIn)"
if ($NoRun) {
    Write-Host "  Run it:   dotnet run --project Corely.Billing.Demos.$Demo (or run this script without -NoRun)"
    return
}

Write-Step "Starting the $Demo demo on $appUrl (Ctrl+C stops it)"
$app = Start-Process dotnet -NoNewWindow -PassThru -WorkingDirectory $demoProject -ArgumentList (@(
    'run', '--no-build', '--launch-profile', "Corely.Billing.Demos.$Demo", '--'
) + ($appArguments | ForEach-Object { "`"$_`"" }))
try {
    $deadline = (Get-Date).AddMinutes(2)
    while (-not (Test-PortListening $config.Port)) {
        if ($app.HasExited) {
            throw "The demo exited during startup with code $($app.ExitCode)."
        }
        if ((Get-Date) -gt $deadline) {
            throw "The demo did not start listening on $($config.Port) within two minutes."
        }
        Start-Sleep -Milliseconds 500
    }
    try {
        Start-Process $appUrl
    }
    catch {
        Write-Warning "Could not open a browser; go to $appUrl yourself."
    }
    $app.WaitForExit()
}
finally {
    if (-not $app.HasExited) {
        $app.Kill($true)
    }
}
