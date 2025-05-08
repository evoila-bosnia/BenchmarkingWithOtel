#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Creates a Docker image for the Client application and places it in the root folder.
.DESCRIPTION
  This script will:
  1. Build the Client application
  2. Create a Docker image with the built application
  3. Save the Docker image to a tar file in the root folder
  4. Optionally tag and push the image to a registry
.NOTES
  Run this script from the root of the repository.
  Requires Docker to be installed and running.

.PARAMETER Tag
  Tag for the Docker image. Default is 'benchmarking-client:latest'

.PARAMETER SaveImage
  Whether to save the Docker image to a tar file. Default is true.

.PARAMETER PushImage
  Whether to push the Docker image to a registry. Default is false.

.PARAMETER Registry
  Registry to push the Docker image to. Required if PushImage is true.
#>
param (
    [string]$Tag = "benchmarking-client:latest",
    [bool]$SaveImage = $true,
    [bool]$PushImage = $false,
    [string]$Registry = ""
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "Starting Docker image creation for the Client application..." -ForegroundColor Cyan

# Configuration
$clientProjectPath = "Main/BenchmarkingWithOtel.Client"
$publishOutputPath = "docker-build/client"
$dockerfilePath = "docker-build/Dockerfile"
$dockerImageTarPath = "benchmarking-client-docker-image.tar"

# Check if Docker is installed and running
try {
    docker version | Out-Null
    Write-Host "Docker is installed and running." -ForegroundColor Green
}
catch {
    Write-Host "Error: Docker is not installed or not running." -ForegroundColor Red
    Write-Host "Please install Docker and ensure it's running before executing this script." -ForegroundColor Red
    exit 1
}

# Create temporary directories
if (-not (Test-Path "docker-build")) {
    New-Item -ItemType Directory -Path "docker-build" | Out-Null
}
if (-not (Test-Path $publishOutputPath)) {
    New-Item -ItemType Directory -Path $publishOutputPath -Force | Out-Null
}

# Publish the application for Linux (which works well in Docker)
Write-Host "Publishing the Client application for Linux..." -ForegroundColor Yellow
try {
    dotnet publish $clientProjectPath `
        --configuration Release `
        --output $publishOutputPath `
        --runtime linux-x64 `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:PublishTrimmed=true

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish the Client application."
    }
    
    Write-Host "Client application published successfully." -ForegroundColor Green
}
catch {
    Write-Host "Error publishing the Client application: $_" -ForegroundColor Red
    exit 1
}

# Create Dockerfile
Write-Host "Creating Dockerfile..." -ForegroundColor Yellow
@"
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine

WORKDIR /app

COPY ./client/ ./

# Set permissions for the executable
RUN chmod +x ./BenchmarkingWithOtel.Client

# Environment variables can be set when running the container
# Example: docker run -e "ServiceUrl=http://server:5000" -e "OtelEndpoint=http://otelcollector:4317" benchmarking-client

# Set entry point to the application
ENTRYPOINT ["./BenchmarkingWithOtel.Client"]
"@ | Out-File -FilePath $dockerfilePath -Encoding UTF8

# Build Docker image
Write-Host "Building Docker image with tag '$Tag'..." -ForegroundColor Yellow
try {
    docker build -t $Tag -f $dockerfilePath docker-build

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build Docker image."
    }
    
    Write-Host "Docker image built successfully with tag '$Tag'." -ForegroundColor Green
}
catch {
    Write-Host "Error building Docker image: $_" -ForegroundColor Red
    exit 1
}

# Push Docker image if requested
if ($PushImage) {
    if ([string]::IsNullOrEmpty($Registry)) {
        Write-Host "Error: Registry parameter is required when PushImage is true." -ForegroundColor Red
        exit 1
    }
    
    $remoteTag = "$Registry/$Tag"
    Write-Host "Tagging Docker image for registry '$Registry'..." -ForegroundColor Yellow
    docker tag $Tag $remoteTag
    
    Write-Host "Pushing Docker image to registry..." -ForegroundColor Yellow
    try {
        docker push $remoteTag
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to push Docker image to registry."
        }
        
        Write-Host "Docker image pushed successfully to '$remoteTag'." -ForegroundColor Green
    }
    catch {
        Write-Host "Error pushing Docker image: $_" -ForegroundColor Red
        exit 1
    }
}

# Save Docker image to a file if requested
if ($SaveImage) {
    Write-Host "Saving Docker image to file '$dockerImageTarPath'..." -ForegroundColor Yellow
    try {
        docker save -o $dockerImageTarPath $Tag
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to save Docker image to file."
        }
        
        Write-Host "Docker image saved successfully to '$dockerImageTarPath'." -ForegroundColor Green
    }
    catch {
        Write-Host "Error saving Docker image: $_" -ForegroundColor Red
        exit 1
    }
}

# Clean up
Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
Remove-Item -Recurse -Force "docker-build"

Write-Host "`nDocker image process completed successfully." -ForegroundColor Green
Write-Host "Image details:" -ForegroundColor Cyan
Write-Host " - Image tag: $Tag" -ForegroundColor White

if ($SaveImage) {
    Write-Host " - Image file: $dockerImageTarPath" -ForegroundColor White
    
    # Get file size
    $fileSize = (Get-Item $dockerImageTarPath).Length / 1MB
    Write-Host " - Image file size: $([math]::Round($fileSize, 2)) MB" -ForegroundColor White
}

if ($PushImage) {
    Write-Host " - Pushed to: $remoteTag" -ForegroundColor White
}

Write-Host "`nTo use this Docker image:" -ForegroundColor Cyan
Write-Host "1. Run the container directly:" -ForegroundColor White
Write-Host "   docker run -e \"ServiceUrl=http://server:5000\" -e \"OtelEndpoint=http://otelcollector:4317\" $Tag" -ForegroundColor White
Write-Host "2. Or load the saved image file on another machine:" -ForegroundColor White
Write-Host "   docker load -i $dockerImageTarPath" -ForegroundColor White 