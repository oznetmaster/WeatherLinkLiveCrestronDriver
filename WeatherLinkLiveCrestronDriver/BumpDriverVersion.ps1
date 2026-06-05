param(
	[Parameter(Mandatory)][string] $ManifestPath,
	[string] $Configuration = 'Debug'
)

if ($Configuration -ne 'Debug' -or -not (Test-Path $ManifestPath)) {
	exit 0
}

$json = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$parts = @($json.GeneralInformation.DriverVersion -split '\.')
if ($parts.Count -ne 4) {
	Write-Warning 'DriverVersion must contain four numeric components.'
	exit 0
}

$parts[3] = ([int]$parts[3] + 1).ToString('0000')
$json.GeneralInformation.DriverVersion = ($parts -join '.')
$json.GeneralInformation.VersionDate = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
$json | ConvertTo-Json -Depth 20 | Set-Content -Path $ManifestPath -Encoding UTF8
