param(
  [string]$PackageName,
  [string]$PackageVersion,
  [string]$FileName
)

$ApiUrl = "$env:CI_API_V4_URL/projects/$env:CI_PROJECT_ID/packages/generic/$PackageName/$PackageVersion/$FileName"

Write-Host "Uploading $FileName to $ApiUrl"

Invoke-RestMethod -Uri $ApiUrl -Method 'PUT' -InFile $FileName -Headers @{
  "JOB-TOKEN" = $env:CI_JOB_TOKEN
}

Write-Host "Successfully uploaded package."