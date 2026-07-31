param(
    [Parameter(Mandatory = $true)]
    [string]$BossModRoot,

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot "../BetterDeaths/BossModUltimateCatalog.Generated.cs"
}

$encounters = @(
    @{ Name = "UCOB"; Territory = 733; RelativePath = "BossMod/Modules/Stormblood/Ultimate/UCOB" },
    @{ Name = "UWU"; Territory = 777; RelativePath = "BossMod/Modules/Stormblood/Ultimate/UWU" },
    @{ Name = "TEA"; Territory = 887; RelativePath = "BossMod/Modules/Shadowbringers/Ultimate/TEA" },
    @{ Name = "DSR"; Territory = 968; RelativePath = "BossMod/Modules/Endwalker/Ultimate/DSW1" },
    @{ Name = "DSR"; Territory = 968; RelativePath = "BossMod/Modules/Endwalker/Ultimate/DSW2" },
    @{ Name = "TOP"; Territory = 1122; RelativePath = "BossMod/Modules/Endwalker/Ultimate/TOP" },
    @{ Name = "FRU"; Territory = 1238; RelativePath = "BossMod/Modules/Dawntrail/Ultimate/FRU" },
    @{ Name = "UMAD"; Territory = 1363; RelativePath = "BossMod/Modules/Dawntrail/Ultimate/UMAD" }
)

$kindMap = [ordered]@{
    "OID" = "Object"
    "AID" = "Action"
    "SID" = "Status"
    "IconID" = "Icon"
    "TetherID" = "Tether"
}

function Escape-CSharpString([string]$value) {
    return $value.Replace("\", "\\").Replace('"', '\"')
}

function Format-Float([double]$value) {
    return $value.ToString("0.###", [Globalization.CultureInfo]::InvariantCulture) + "f"
}

function Parse-UInt32([string]$value) {
    if ($value.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
        return [Convert]::ToUInt32($value.Substring(2), 16)
    }

    return [Convert]::ToUInt32($value, 10)
}

function New-Geometry([string]$comment, [string]$name) {
    if ([string]::IsNullOrWhiteSpace($comment) -or
        $name -match 'Fake' -or
        $comment -match '\bdoes nothing\b|\bno effect\b') {
        return $null
    }

    $anchor = if ($comment -match '->(?:player|players|location)\b') { "Target" } else { "Source" }
    $shape = $null
    $radius = 0.0
    $length = 0.0
    $width = 0.0
    $angle = 0.0

    if ($comment -match 'range\s+(?<inner>\d+(?:\.\d+)?)\s*-\s*(?<outer>\d+(?:\.\d+)?)\s+donut') {
        $shape = "Donut"
        $radius = [double]$Matches.outer
        $width = [double]$Matches.inner
    }
    elseif ($comment -match 'range\s+(?<length>\d+(?:\.\d+)?)(?:\+R)?\s+width\s+(?<width>\d+(?:\.\d+)?)\s+rect') {
        $shape = "Line"
        $length = [double]$Matches.length
        $width = [double]$Matches.width
    }
    elseif ($comment -match 'range\s+(?<length>\d+(?:\.\d+)?)(?:\+R)?\s+(?<angle>\d+(?:\.\d+)?)-degree\s+cone') {
        $shape = "Cone"
        $radius = [double]$Matches.length
        $length = [double]$Matches.length
        $angle = [double]$Matches.angle
    }
    elseif ($comment -match 'range\s+(?<radius>\d+(?:\.\d+)?)(?:\+R)?\s+circle') {
        $shape = "Circle"
        $radius = [double]$Matches.radius
    }

    if ($null -eq $shape) {
        return $null
    }

    if ($comment -match '\btower\b') {
        $shape = "Tower"
    }
    elseif ($comment -match '\bstack\b') {
        $shape = "Stack"
    }
    elseif ($comment -match '\bspread\b') {
        $shape = "Spread"
    }

    return @{
        Shape = $shape
        Radius = $radius
        Length = $length
        Width = $width
        Angle = $angle
        Anchor = $anchor
    }
}

$identifiers = [Collections.Generic.List[object]]::new()
$actions = [Collections.Generic.List[object]]::new()

foreach ($encounter in $encounters) {
    $directory = Join-Path $BossModRoot $encounter.RelativePath
    if (-not (Test-Path -LiteralPath $directory)) {
        throw "BossMod Ultimate directory was not found: $directory"
    }

    $enumFiles = Get-ChildItem -LiteralPath $directory -Filter "*Enums.cs"
    foreach ($enumFile in $enumFiles) {
        $content = Get-Content -LiteralPath $enumFile.FullName -Raw
        foreach ($enumName in $kindMap.Keys) {
            $enumPattern = "public\s+enum\s+$enumName\s*:\s*uint\s*\{(?<body>.*?)\r?\n\}"
            $enumMatch = [regex]::Match($content, $enumPattern, [Text.RegularExpressions.RegexOptions]::Singleline)
            if (-not $enumMatch.Success) {
                continue
            }

            $entryPattern = '(?m)^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>0x[0-9A-Fa-f]+|\d+)\s*,?\s*(?://\s*(?<comment>.*))?$'
            foreach ($entryMatch in [regex]::Matches($enumMatch.Groups["body"].Value, $entryPattern)) {
                $name = $entryMatch.Groups["name"].Value
                $id = Parse-UInt32 $entryMatch.Groups["value"].Value
                $comment = $entryMatch.Groups["comment"].Value.Trim()
                $identifier = @{
                    Territory = [uint32]$encounter.Territory
                    Encounter = [string]$encounter.Name
                    Kind = [string]$kindMap[$enumName]
                    Id = $id
                    Name = $name
                }
                $identifiers.Add($identifier)

                if ($enumName -eq "AID") {
                    $actions.Add(@{
                        Territory = [uint32]$encounter.Territory
                        Encounter = [string]$encounter.Name
                        Id = $id
                        Name = $name
                        Geometry = New-Geometry $comment $name
                    })
                }
            }
        }
    }
}

$sourceCommit = (& git -C $BossModRoot rev-parse HEAD).Trim()
if ([string]::IsNullOrWhiteSpace($sourceCommit)) {
    throw "Could not determine the BossMod source commit."
}

$builder = [Text.StringBuilder]::new()
[void]$builder.AppendLine("// <auto-generated />")
[void]$builder.AppendLine("// Generated by scripts/generate-bossmod-ultimate-catalog.ps1.")
[void]$builder.AppendLine("// Source: https://github.com/awgil/ffxiv_bossmod")
[void]$builder.AppendLine()
[void]$builder.AppendLine("namespace BetterDeaths;")
[void]$builder.AppendLine()
[void]$builder.AppendLine("internal static partial class BossModUltimateCatalog")
[void]$builder.AppendLine("{")
[void]$builder.AppendLine("    public const string SourceCommit = `"$sourceCommit`";")
[void]$builder.AppendLine("    public const string SourceProvenance = `"BossMod@$($sourceCommit.Substring(0, 12))`";")
[void]$builder.AppendLine()
[void]$builder.AppendLine("    private static readonly ReplayCatalogAction[] GeneratedActions =")
[void]$builder.AppendLine("    [")

foreach ($action in $actions) {
    $geometryText = "null"
    $anchor = "Source"
    if ($null -ne $action.Geometry) {
        $geometry = $action.Geometry
        $anchor = $geometry.Anchor
        $geometryText = "new ReplayMechanicGeometry(ReplayMechanicShape.$($geometry.Shape), Radius: $(Format-Float $geometry.Radius), Length: $(Format-Float $geometry.Length), Width: $(Format-Float $geometry.Width), AngleDegrees: $(Format-Float $geometry.Angle))"
    }

    [void]$builder.AppendLine(
        "        new($($action.Territory)u, `"$(Escape-CSharpString $action.Encounter)`", $($action.Id)u, `"$(Escape-CSharpString $action.Name)`", $geometryText, ReplayMechanicAnchor.$anchor),")
}

[void]$builder.AppendLine("    ];")
[void]$builder.AppendLine()
[void]$builder.AppendLine("    private static readonly ReplayCatalogIdentifier[] GeneratedIdentifiers =")
[void]$builder.AppendLine("    [")

foreach ($identifier in $identifiers) {
    [void]$builder.AppendLine(
        "        new($($identifier.Territory)u, `"$(Escape-CSharpString $identifier.Encounter)`", ReplayCatalogIdentifierKind.$($identifier.Kind), $($identifier.Id)u, `"$(Escape-CSharpString $identifier.Name)`"),")
}

[void]$builder.AppendLine("    ];")
[void]$builder.AppendLine("}")

$resolvedOutputPath = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $resolvedOutputPath
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
[IO.File]::WriteAllText($resolvedOutputPath, $builder.ToString(), [Text.UTF8Encoding]::new($false))

Write-Host "Generated $($actions.Count) Ultimate actions and $($identifiers.Count) identifiers."
Write-Host "BossMod commit: $sourceCommit"
Write-Host "Output: $resolvedOutputPath"
