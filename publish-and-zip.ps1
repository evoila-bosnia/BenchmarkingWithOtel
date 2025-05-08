#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Publishes, zips, and places Server, Client, and ReverseProxy applications in the root folder.
.DESCRIPTION
  This script will:
  1. Publish each application in a self-contained way
  2. Zip the published output
  3. Move the zipped files to the root folder
.NOTES
  Run this script from the root of the repository.
  You may need to run PowerShell as administrator or change execution policy:
  Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
#>

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "Starting application publishing and zipping process..." -ForegroundColor Cyan

# Configuration
$serverProjectPath = "Main/BenchmarkingWithOtel.Server"
$clientProjectPath = "Main/BenchmarkingWithOtel.Client"
$reverseProxyProjectPath = "Main/BenchmarkingWithOtel.ReverseProxy"

$outputPath = "publish"
$serverOutputPath = Join-Path $outputPath "server"
$clientOutputPath = Join-Path $outputPath "client"
$reverseProxyOutputPath = Join-Path $outputPath "revproxy"

$serverZipName = "server-publish.zip"
$clientZipName = "client-publish.zip"
$reverseProxyZipName = "revproxy-publish.zip"

# Check if dotnet CLI is available
try {
    $dotnetVersion = dotnet --version
    Write-Host "Using .NET SDK version: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Host "Error: .NET SDK is not installed or not in the PATH." -ForegroundColor Red
    Write-Host "Please install .NET SDK from https://dotnet.microsoft.com/download" -ForegroundColor Red
    exit 1
}

# Ensure output directory exists
if (-not (Test-Path $outputPath)) {
    Write-Host "Creating output directory: $outputPath"
    New-Item -ItemType Directory -Path $outputPath | Out-Null
}

# Function to publish, zip, and move a project
function Publish-And-Zip {
    param (
        [string]$projectPath,
        [string]$outputPath,
        [string]$zipName,
        [string]$projectName
    )
    
    Write-Host "`nPublishing $projectName to $outputPath..." -ForegroundColor Yellow
    
    try {
        # Create output directory if it doesn't exist
        if (-not (Test-Path $outputPath)) {
            New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
        }
        
        # Publish the application (self-contained for Windows x64)
        dotnet publish $projectPath `
            --configuration Release `
            --self-contained true `
            --runtime win-x64 `
            --output $outputPath
        
        # Check if publish was successful
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to publish $projectPath"
        }
        
        Write-Host "Creating zip file: $zipName..." -ForegroundColor Yellow
        
        # Create zip file
        Compress-Archive -Path "$outputPath\*" -DestinationPath $zipName -Force
        
        # Move zip file to root directory
        Move-Item -Path $zipName -Destination ".\$zipName" -Force
        
        Write-Host "Successfully published and zipped $projectName to $zipName" -ForegroundColor Green
    }
    catch {
        Write-Host "Error processing $projectName" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        exit 1
    }
}

# Clean up any existing output directories
Write-Host "Cleaning up any existing output directories..." -ForegroundColor Cyan
if (Test-Path $outputPath) { 
    Remove-Item -Recurse -Force $outputPath 
    Write-Host "Removed existing $outputPath directory" -ForegroundColor Green
}

# Publish, zip, and move each project
Write-Host "`nStarting publishing process..." -ForegroundColor Cyan
Publish-And-Zip -projectPath $serverProjectPath -outputPath $serverOutputPath -zipName $serverZipName -projectName "Server"
Publish-And-Zip -projectPath $clientProjectPath -outputPath $clientOutputPath -zipName $clientZipName -projectName "Client"
Publish-And-Zip -projectPath $reverseProxyProjectPath -outputPath $reverseProxyOutputPath -zipName $reverseProxyZipName -projectName "ReverseProxy"

# Clean up temporary directories
Write-Host "`nCleaning up temporary directories..." -ForegroundColor Cyan
Remove-Item -Recurse -Force $outputPath

Write-Host "`nAll applications have been published, zipped, and placed in the root directory:" -ForegroundColor Green
Write-Host " - $serverZipName" -ForegroundColor White
Write-Host " - $clientZipName" -ForegroundColor White
Write-Host " - $reverseProxyZipName" -ForegroundColor White

Write-Host "`nTo use these published applications:" -ForegroundColor Cyan
Write-Host "1. Extract the zip files to your target environment" -ForegroundColor White
Write-Host "2. For each application, run the executable (.exe) file in the extracted directory" -ForegroundColor White
Write-Host "3. Make sure to properly configure environment variables if needed" -ForegroundColor White 