[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Tag,

    [switch] $Prerelease
)

$ErrorActionPreference = 'Stop'

# NuGet strips build metadata from package identity, so reject it to avoid collisions.
$pattern = '\Av?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z'
$match = [regex]::Match($Tag, $pattern)
if (-not $match.Success) {
    throw "Release tag '$Tag' must be MAJOR.MINOR.PATCH or MAJOR.MINOR.PATCH-prerelease, optionally prefixed with v. Build metadata is not supported."
}

$suffix = $match.Groups[4].Value
foreach ($identifier in ($suffix -split '\.')) {
    if ($identifier -match '\A[0-9]+\z' -and $identifier.Length -gt 1 -and $identifier.StartsWith('0')) {
        throw "Numeric prerelease identifiers cannot contain leading zeroes: '$Tag'."
    }
}

if ($Prerelease.IsPresent -ne ($suffix.Length -gt 0)) {
    throw "The GitHub release prerelease flag must match the SemVer prerelease suffix: '$Tag'."
}

$Tag -creplace '\Av', ''
