$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Get-ReleaseVersion.ps1'

$cases = @(
    @{ Tag = 'v1.0.0'; Prerelease = $false; Expected = '1.0.0' }
    @{ Tag = '2.3.4'; Prerelease = $false; Expected = '2.3.4' }
    @{ Tag = 'v0.1.0'; Prerelease = $false; Expected = '0.1.0' }
    @{ Tag = 'v1.2.3-rc.1'; Prerelease = $true; Expected = '1.2.3-rc.1' }
    @{ Tag = '1.0.0-alpha.0.a-1'; Prerelease = $true; Expected = '1.0.0-alpha.0.a-1' }
    @{ Tag = '1.0'; Prerelease = $false }
    @{ Tag = 'V1.0.0'; Prerelease = $false }
    @{ Tag = 'v01.0.0'; Prerelease = $false }
    @{ Tag = '1.0.0.0'; Prerelease = $false }
    @{ Tag = '1.0.0+build.1'; Prerelease = $false }
    @{ Tag = '1.0.0-rc.01'; Prerelease = $true }
    @{ Tag = '1.0.0-rc..1'; Prerelease = $true }
    @{ Tag = '1.0.0-'; Prerelease = $true }
    @{ Tag = '1.0.0-rc.1'; Prerelease = $false }
    @{ Tag = '1.0.0'; Prerelease = $true }
    @{ Tag = "1.0.0`n"; Prerelease = $false }
)

foreach ($case in $cases) {
    $failed = $false
    try {
        $actual = & $script -Tag $case.Tag -Prerelease:$case.Prerelease
    }
    catch {
        if ($case.ContainsKey('Expected')) {
            throw
        }
        $failed = $true
    }

    if ($case.ContainsKey('Expected')) {
        if ($actual -cne $case.Expected) {
            throw "Tag '$($case.Tag)' returned '$actual' instead of '$($case.Expected)'."
        }
    }
    elseif (-not $failed) {
        throw "Invalid tag or prerelease flag was accepted: '$($case.Tag)'."
    }
}

Write-Host "Passed $($cases.Count) release-version cases."
