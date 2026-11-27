<#
.SYNOPSIS
Runs the CrestApps.OrchardCore.Tests unit suite in several test processes at once.

.DESCRIPTION
The unit test assembly turns xUnit parallelization off (see TestParallelizationBehavior.cs): a few libraries it
tests keep process-wide state, and some tests change the current culture or listen to process-wide meters. In one
process the whole suite therefore runs one test at a time, on one core.

This script keeps that guarantee and still uses every core. It lists the test classes, splits them into chunks of
about the same size, and runs each chunk in its own test process, several processes at a time. Inside a process the
tests still run one at a time, so no test shares static state with a test running beside it; the processes share
nothing but the machine.

When every chunk has finished, the script checks that every test method the assembly declares was run, prints the
failures and the slowest classes, and exits with a failure code if any test failed or a test process crashed.

The timings of each run are saved next to the results (timings.json). The next run reads them to balance the chunks,
so a slow class no longer lands in the last chunk to start.

.PARAMETER Configuration
The build configuration whose test assembly is run. Defaults to Release, the configuration CI uses.

.PARAMETER Parallel
How many test processes run at once. Defaults to the number of logical processors, at most 8.

.PARAMETER Namespace
Runs only the classes in these namespaces (and the namespaces below them). The CrestApps.OrchardCore.Tests prefix is
optional: "Checkout" and "Modules.Telephony" both work.

.PARAMETER Class
Runs only classes whose name contains one of these strings, for example "CheckoutEngineTests".

.PARAMETER Build
Builds the test project before running it.

.PARAMETER AssemblyPath
Runs this test assembly instead of the one built under tests/CrestApps.OrchardCore.Tests/bin.

.PARAMETER ResultsDirectory
Where the xUnit result files, the process logs and timings.json are written. Defaults to TestResults/unit under the
repository root.

.EXAMPLE
./tests/run-unit-tests.ps1 -Build

.EXAMPLE
./tests/run-unit-tests.ps1 -Namespace Checkout, Subscriptions
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [int] $Parallel = 0,
    [string[]] $Namespace = @(),
    [string[]] $Class = @(),
    [switch] $Build,
    [string] $AssemblyPath,
    [string] $ResultsDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectDirectory = Join-Path $PSScriptRoot 'CrestApps.OrchardCore.Tests'
$project = Join-Path $projectDirectory 'CrestApps.OrchardCore.Tests.csproj'
$namespacePrefix = 'CrestApps.OrchardCore.Tests'

if (-not $ResultsDirectory) {
    $ResultsDirectory = Join-Path $repositoryRoot (Join-Path 'TestResults' 'unit')
}

# From another shell (pwsh -File, bash, CI) a list such as "Checkout,Subscriptions" arrives as one string.
$Namespace = @($Namespace | ForEach-Object { $_ -split ',' } | ForEach-Object Trim | Where-Object { $_ })
$Class = @($Class | ForEach-Object { $_ -split ',' } | ForEach-Object Trim | Where-Object { $_ })

if ($Parallel -le 0) {
    $Parallel = [Math]::Min([Environment]::ProcessorCount, 8)
}

if ($Build) {
    dotnet build $project -c $Configuration

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

# The assembly is built for the default target framework; take the newest build if older ones are still on disk.
$assembly = if ($AssemblyPath) { Get-Item $AssemblyPath } else { Get-ChildItem -Path (Join-Path $projectDirectory "bin/$Configuration") -Filter 'CrestApps.OrchardCore.Tests.dll' -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Directory.Parent.Name -eq $Configuration } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1 }

if (-not $assembly) {
    Write-Error "The $Configuration test assembly was not found under $projectDirectory/bin. Build it first, or pass -Build."
}

# Each run starts from an empty results folder, so a result file left by an earlier run is never counted. The
# timings of the earlier run are kept: they balance this one.
$timingsFile = Join-Path $ResultsDirectory 'timings.json'
$previousTimings = @{}

if (Test-Path $timingsFile) {
    try {
        (Get-Content $timingsFile -Raw | ConvertFrom-Json -AsHashtable).GetEnumerator() | ForEach-Object { $previousTimings[$_.Key] = [double]$_.Value }
    }
    catch {
        Write-Warning "Ignoring unreadable timings in ${timingsFile}: $_"
    }
}

if (Test-Path $ResultsDirectory) {
    Get-ChildItem $ResultsDirectory -File | Where-Object Name -ne 'timings.json' | Remove-Item -Force
}
else {
    New-Item -ItemType Directory -Path $ResultsDirectory | Out-Null
}

Write-Host "Test assembly: $($assembly.FullName)"

$listing = dotnet $assembly.FullName -list methods/json

if ($LASTEXITCODE -ne 0) {
    Write-Error "Listing the tests failed with exit code ${LASTEXITCODE}:`n$listing"
}

$methods = @(($listing -join "`n") | ConvertFrom-Json)

# A listed name is Namespace.Class.Method; nested classes are listed with '+'.
$classes = [ordered]@{}

foreach ($method in $methods) {
    $className = $method.Substring(0, $method.LastIndexOf('.'))

    if (-not $classes.Contains($className)) {
        $classes[$className] = [System.Collections.Generic.List[string]]::new()
    }

    $classes[$className].Add($method)
}

$selected = @($classes.Keys)

if ($Namespace.Count -gt 0) {
    $prefixes = $Namespace | ForEach-Object { if ($_.StartsWith("$namespacePrefix.")) { $_ } else { "$namespacePrefix.$_" } }
    $selected = @($selected | Where-Object { $name = $_; $prefixes | Where-Object { $name.StartsWith("$_.") } })
}

if ($Class.Count -gt 0) {
    $selected = @($selected | Where-Object { $name = $_; $Class | Where-Object { $name.Contains($_) } })
}

if ($selected.Count -eq 0) {
    Write-Error 'No test class matches the given filters.'
}

# A class's weight is its time in the previous run, or an estimate from its number of test methods when it has no
# timing yet. 0.05 seconds per method is about the suite average.
$weights = @{}

foreach ($className in $selected) {
    $weights[$className] = if ($previousTimings.ContainsKey($className)) { $previousTimings[$className] } else { 0.05 * $classes[$className].Count }
}

$totalWeight = ($weights.Values | Measure-Object -Sum).Sum

# About four chunks per process, so the processes that finish early pick up the remaining chunks and all of them end
# at about the same time. A chunk is also kept well under the Windows command-line limit of 32,767 characters.
$chunkCount = [Math]::Max(1, [Math]::Min($selected.Count, $Parallel * 4))
$targetWeight = $totalWeight / $chunkCount
$maxArgumentLength = 20000
$chunks = [System.Collections.Generic.List[object]]::new()

# A class heavier than a chunk would hold every other process up while it runs alone at the end, so its methods are
# spread over several chunks of their own. xUnit ANDs different filter kinds, so these chunks select by -method only
# and the others by -class only. Class fixtures are then built once per chunk instead of once.
$wholeClasses = [System.Collections.Generic.List[string]]::new()

foreach ($className in $selected) {
    $classMethods = $classes[$className]
    $parts = [Math]::Min($classMethods.Count, [int][Math]::Ceiling($weights[$className] / $targetWeight))

    if ($parts -le 1) {
        $wholeClasses.Add($className)
        continue
    }

    for ($part = 0; $part -lt $parts; $part++) {
        $partMethods = @(for ($i = $part; $i -lt $classMethods.Count; $i += $parts) { $classMethods[$i] })
        $chunks.Add([pscustomobject]@{ Filter = '-method'; Names = $partMethods; Weight = $weights[$className] / $parts })
    }
}

$current = [System.Collections.Generic.List[string]]::new()
$currentWeight = 0.0
$currentLength = 0

# Heaviest classes first: they start first, and the small ones fill in at the end.
foreach ($className in ($wholeClasses | Sort-Object { $weights[$_] } -Descending)) {
    $argumentLength = $className.Length + 10

    if ($current.Count -gt 0 -and ($currentWeight + $weights[$className] -gt $targetWeight -or $currentLength + $argumentLength -gt $maxArgumentLength)) {
        $chunks.Add([pscustomobject]@{ Filter = '-class'; Names = $current.ToArray(); Weight = $currentWeight })
        $current = [System.Collections.Generic.List[string]]::new()
        $currentWeight = 0.0
        $currentLength = 0
    }

    $current.Add($className)
    $currentWeight += $weights[$className]
    $currentLength += $argumentLength
}

if ($current.Count -gt 0) {
    $chunks.Add([pscustomobject]@{ Filter = '-class'; Names = $current.ToArray(); Weight = $currentWeight })
}

$index = 0
$work = foreach ($chunk in ($chunks | Sort-Object Weight -Descending)) {
    $index++
    [pscustomobject]@{ Index = $index; Filter = $chunk.Filter; Names = $chunk.Names }
}

Write-Host "Running $($selected.Count) test classes ($($methods.Count) methods in the assembly) in $($work.Count) chunks, $Parallel processes at a time."

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$assemblyPath = $assembly.FullName

$outcomes = $work | ForEach-Object -ThrottleLimit $Parallel -Parallel {
    $chunk = $_
    $resultsDirectory = $using:ResultsDirectory
    $xml = Join-Path $resultsDirectory ("chunk-{0:D3}.xml" -f $chunk.Index)
    $log = Join-Path $resultsDirectory ("chunk-{0:D3}.log" -f $chunk.Index)
    $arguments = @($using:assemblyPath, '-noLogo', '-noColor', '-result-xml', $xml)

    foreach ($name in $chunk.Names) {
        $arguments += $chunk.Filter
        $arguments += $name
    }

    $started = [System.Diagnostics.Stopwatch]::StartNew()
    & dotnet @arguments *> $log

    [pscustomobject]@{
        Index = $chunk.Index
        ExitCode = $LASTEXITCODE
        Seconds = $started.Elapsed.TotalSeconds
        Xml = $xml
        Log = $log
        Count = $chunk.Names.Count
        Filter = $chunk.Filter
    }
}

$stopwatch.Stop()

# Read every result file: the totals, the failures, each class's time, and which methods ran.
$total = 0
$failed = 0
$skipped = 0
$failures = [System.Collections.Generic.List[string]]::new()
$classTimes = @{}
$ranMethods = [System.Collections.Generic.HashSet[string]]::new()
$crashed = [System.Collections.Generic.List[object]]::new()

foreach ($outcome in ($outcomes | Sort-Object Index)) {
    # The logs are printed in chunk order, so the output reads the same however the processes were scheduled.
    Write-Host "::group::Chunk $($outcome.Index): $($outcome.Count) $($outcome.Filter -eq '-class' ? 'classes' : 'methods'), exit code $($outcome.ExitCode), $([Math]::Round($outcome.Seconds))s"
    Get-Content $outcome.Log | Write-Host
    Write-Host '::endgroup::'

    if (-not (Test-Path $outcome.Xml)) {
        $crashed.Add($outcome)
        continue
    }

    [xml]$document = Get-Content $outcome.Xml -Raw
    $chunkFailed = 0

    foreach ($result in $document.SelectNodes('//assembly')) {
        $total += [int]$result.GetAttribute('total')
        $failed += [int]$result.GetAttribute('failed') + [int]$result.GetAttribute('errors')
        $chunkFailed += [int]$result.GetAttribute('failed') + [int]$result.GetAttribute('errors')
        $skipped += [int]$result.GetAttribute('skipped')
    }

    foreach ($test in $document.SelectNodes('//test')) {
        $className = $test.GetAttribute('type')
        $classTimes[$className] = ($classTimes[$className] ?? 0.0) + [double]::Parse($test.GetAttribute('time'), [Globalization.CultureInfo]::InvariantCulture)
        [void]$ranMethods.Add("$className.$($test.GetAttribute('method'))")

        if ($test.GetAttribute('result') -eq 'Fail') {
            $message = $test.SelectSingleNode('failure/message')
            $text = ($message ? $message.InnerText : '').Trim()
            $failures.Add("$($test.GetAttribute('name'))`n    $($text -replace "`n", "`n    ")")

            if ($env:GITHUB_ACTIONS -eq 'true') {
                $title = $test.GetAttribute('name') -replace '[,:]', ' '
                $body = $text.Substring(0, [Math]::Min($text.Length, 3000)) -replace '%', '%25' -replace "`r", '' -replace "`n", '%0A'
                Write-Host "::error title=$($title.Substring(0, [Math]::Min($title.Length, 200)))::$body"
            }
        }
    }

    # A process that failed without reporting a failed test stopped part way, so its remaining tests never ran.
    if ($outcome.ExitCode -ne 0 -and $chunkFailed -eq 0) {
        $crashed.Add($outcome)
    }
}

# Every method of the selected classes must have run, so a crashed or mis-filtered chunk cannot pass by running less.
$selectedSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$selected)
$missing = @($methods | Where-Object { $selectedSet.Contains($_.Substring(0, $_.LastIndexOf('.'))) -and -not $ranMethods.Contains($_) })

# Save this run's timings to balance the next one. A filtered run only updates the classes it ran.
foreach ($entry in $classTimes.GetEnumerator()) {
    $previousTimings[$entry.Key] = [Math]::Round($entry.Value, 2)
}

$previousTimings | ConvertTo-Json -Compress | Set-Content $timingsFile

Write-Host ''
Write-Host 'Slowest test classes:'
$classTimes.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 15 | ForEach-Object {
    Write-Host ("  {0,8:N1}s  {1}" -f $_.Value, $_.Key)
}

Write-Host ''
$elapsed = [Math]::Round($stopwatch.Elapsed.TotalSeconds)
$serial = [Math]::Round(($outcomes | Measure-Object Seconds -Sum).Sum)
Write-Host "Total tests: $total, failed: $failed, skipped: $skipped. Finished in ${elapsed}s ($serial process-seconds over $($work.Count) chunks)."

$exitCode = 0

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host "Failed tests ($($failures.Count)):"
    $failures | ForEach-Object { Write-Host "  [FAIL] $_" }
    $exitCode = 1
}

foreach ($outcome in $crashed) {
    Write-Host "::error title=Test process crashed::Chunk $($outcome.Index) exited with code $($outcome.ExitCode) without reporting a failed test. See $($outcome.Log)."
    $exitCode = 1
}

if ($missing.Count -gt 0) {
    Write-Host "::error title=Tests did not run::$($missing.Count) test methods never ran, for example $(($missing | Select-Object -First 5) -join ', ')."
    $exitCode = 1
}

if ($failed -gt 0) {
    $exitCode = 1
}

exit $exitCode
