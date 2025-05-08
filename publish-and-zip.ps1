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

.PARAMETER Platforms
  Platforms to publish the client application for.
  By default, builds for Windows (x64), Linux (x64), macOS (x64), and macOS (ARM64).
  Available options: win-x64, linux-x64, osx-x64, win-arm64, linux-arm64, osx-arm64
#>
param (
    [string[]]$Platforms = @("win-x64", "linux-x64", "osx-x64", "osx-arm64")
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "Starting application publishing and zipping process..." -ForegroundColor Cyan
Write-Host "Building client application for platforms: $($Platforms -join ', ')" -ForegroundColor Cyan

# Configuration
$serverProjectPath = "Main/BenchmarkingWithOtel.Server"
$clientProjectPath = "Main/BenchmarkingWithOtel.Client"
$reverseProxyProjectPath = "Main/BenchmarkingWithOtel.ReverseProxy"

$outputPath = "publish"
$serverOutputPath = Join-Path $outputPath "server"
$clientOutputBaseDir = Join-Path $outputPath "client"
$reverseProxyOutputPath = Join-Path $outputPath "revproxy"

$serverZipName = "server-publish.zip"
$clientZipBaseNameFormat = "client-publish-{0}.zip"
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

# Function to publish, zip, and move a regular project
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

# Function to publish, zip, and move client with special options (single file, portable)
function Publish-Client-For-Platform {
    param (
        [string]$projectPath,
        [string]$outputBaseDir,
        [string]$zipNameFormat,
        [string]$platform
    )
    
    $platformOutputDir = Join-Path $outputBaseDir $platform
    $platformZipName = $zipNameFormat -f $platform
    
    Write-Host "`nPublishing Client for $platform to $platformOutputDir..." -ForegroundColor Yellow
    
    try {
        # Create output directory if it doesn't exist
        if (-not (Test-Path $platformOutputDir)) {
            New-Item -ItemType Directory -Path $platformOutputDir -Force | Out-Null
        }
        
        # Create temporary properties file for publish settings
        $propsContent = @"
<Project>
  <PropertyGroup>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>true</PublishTrimmed>
    <PublishReadyToRun>false</PublishReadyToRun>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    <DebugType>embedded</DebugType>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>$platform</RuntimeIdentifier>
  </PropertyGroup>
</Project>
"@
        $propsPath = Join-Path $outputPath "Client-$platform.pubxml"
        $propsContent | Out-File -FilePath $propsPath -Encoding UTF8
        
        Write-Host "Using platform-specific settings: Runtime=$platform, Props file=$propsPath" -ForegroundColor Cyan
        
        # Publish the client application with platform-specific and portable options
        # Explicitly set the runtime identifier on the command line as well
        dotnet publish $projectPath `
            --configuration Release `
            --output $platformOutputDir `
            --self-contained true `
            --runtime $platform `
            -p:PublishProfile=$propsPath
        
        # Check if publish was successful
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to publish Client for $platform"
        }
        
        Write-Host "Creating zip file: $platformZipName..." -ForegroundColor Yellow
        
        # Create zip file
        Compress-Archive -Path "$platformOutputDir\*" -DestinationPath $platformZipName -Force
        
        # Move zip file to root directory
        Move-Item -Path $platformZipName -Destination ".\$platformZipName" -Force
        
        Write-Host "Successfully published and zipped Client for $platform to $platformZipName" -ForegroundColor Green
        
        # Cleanup temporary props file
        Remove-Item -Path $propsPath -Force -ErrorAction SilentlyContinue
    }
    catch {
        Write-Host "Error processing Client for $platform" -ForegroundColor Red
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

# Publish Server and ReverseProxy
Write-Host "`nStarting publishing process..." -ForegroundColor Cyan
Publish-And-Zip -projectPath $serverProjectPath -outputPath $serverOutputPath -zipName $serverZipName -projectName "Server"
Publish-And-Zip -projectPath $reverseProxyProjectPath -outputPath $reverseProxyOutputPath -zipName $reverseProxyZipName -projectName "ReverseProxy"

# Publish Client for each specified platform
foreach ($platform in $Platforms) {
    Publish-Client-For-Platform -projectPath $clientProjectPath `
                                -outputBaseDir $clientOutputBaseDir `
                                -zipNameFormat $clientZipBaseNameFormat `
                                -platform $platform
}

# Clean up temporary directories
Write-Host "`nCleaning up temporary directories..." -ForegroundColor Cyan
Remove-Item -Recurse -Force $outputPath

# Display summary of published files
Write-Host "`nAll applications have been published, zipped, and placed in the root directory:" -ForegroundColor Green
Write-Host " - $serverZipName" -ForegroundColor White
Write-Host " - $reverseProxyZipName" -ForegroundColor White

foreach ($platform in $Platforms) {
    $platformZipName = $clientZipBaseNameFormat -f $platform
    Write-Host " - $platformZipName" -ForegroundColor White
}

Write-Host "`nTo use these published applications:" -ForegroundColor Cyan
Write-Host "1. Extract the zip files to your target environment" -ForegroundColor White
Write-Host "2. For the Client application, the executable is now a single file that can be run directly:" -ForegroundColor White
Write-Host "   - On Windows: BenchmarkingWithOtel.Client.exe" -ForegroundColor White
Write-Host "   - On Linux/macOS: ./BenchmarkingWithOtel.Client (may need 'chmod +x' first)" -ForegroundColor White
Write-Host "3. Make sure to properly configure environment variables if needed" -ForegroundColor White

Write-Host "`nNote: If using the bash wrapper script (publish-and-zip.sh) on Linux/macOS," -ForegroundColor Yellow
Write-Host "make it executable first with: chmod +x publish-and-zip.sh" -ForegroundColor Yellow

Write-Host "`nNote: You can still specify custom platforms if needed:" -ForegroundColor Yellow
Write-Host "./publish-and-zip.ps1 -Platforms win-arm64,linux-arm64,osx-arm64" -ForegroundColor White 