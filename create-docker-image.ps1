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

.PARAMETER Architectures
  Architectures to build for. Default is 'amd64,arm64'.
  Available options: amd64, arm64
#>
param (
    [string]$Tag = "benchmarking-client:latest",
    [bool]$SaveImage = $true,
    [bool]$PushImage = $false,
    [string]$Registry = "",
    [string]$Architectures = "amd64,arm64"
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Parse architectures
$archList = $Architectures.Split(",").Trim()
Write-Host "Starting Docker image creation for the Client application..." -ForegroundColor Cyan
Write-Host "Building for architectures: $($archList -join ', ')" -ForegroundColor Cyan

# Configuration
$clientProjectPath = "Main/BenchmarkingWithOtel.Client"
$publishBaseDir = "docker-build"
$dockerfilePathTemplate = "docker-build/Dockerfile.{0}"
$dockerImageTarPathTemplate = "benchmarking-client-docker-image-{0}.tar"

# Map .NET runtime identifiers to Docker architecture names
$archMap = @{
    "amd64" = "linux-x64"
    "arm64" = "linux-arm64"
}

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
if (-not (Test-Path $publishBaseDir)) {
    New-Item -ItemType Directory -Path $publishBaseDir | Out-Null
}

# Check if Docker Buildx is available for multi-architecture builds
$useMultiArch = $false
if ($archList.Count -gt 1) {
    try {
        docker buildx version | Out-Null
        $builderExists = (docker buildx ls | Select-String "multiarch") -ne $null
        
        if (-not $builderExists) {
            Write-Host "Creating Docker Buildx builder for multi-architecture builds..." -ForegroundColor Yellow
            docker buildx create --name multiarch --use | Out-Null
        }
        else {
            docker buildx use multiarch | Out-Null
        }
        
        $useMultiArch = $true
        Write-Host "Docker Buildx is available. Will create multi-architecture image." -ForegroundColor Green
    }
    catch {
        Write-Host "Docker Buildx not available. Will build separate images for each architecture." -ForegroundColor Yellow
        $useMultiArch = $false
    }
}

# Build images for each architecture
$archTags = @()
foreach ($arch in $archList) {
    $runtimeId = $archMap[$arch]
    if (-not $runtimeId) {
        Write-Host "Unsupported architecture: $arch. Skipping." -ForegroundColor Red
        continue
    }
    
    $publishOutputPath = Join-Path $publishBaseDir "client-$arch"
    $dockerfilePath = $dockerfilePathTemplate -f $arch
    $archTag = $Tag + "-" + $arch
    $archTags += $archTag
    
    # Create output directory
    if (-not (Test-Path $publishOutputPath)) {
        New-Item -ItemType Directory -Path $publishOutputPath -Force | Out-Null
    }
    
    # Publish the application for the specific architecture
    Write-Host "`nPublishing the Client application for $arch ($runtimeId)..." -ForegroundColor Yellow
    try {
        dotnet publish $clientProjectPath `
            --configuration Release `
            --output $publishOutputPath `
            --runtime $runtimeId `
            --self-contained true `
            /p:PublishSingleFile=true `
            /p:PublishTrimmed=true

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to publish the Client application for $arch."
        }
        
        Write-Host "Client application published successfully for $arch." -ForegroundColor Green
    }
    catch {
        $errorMessage = "Error publishing the Client application"
        Write-Host "$errorMessage - $_" -ForegroundColor Red
        exit 1
    }
    
    # Create architecture-specific Dockerfile
    Write-Host "Creating Dockerfile for $arch..." -ForegroundColor Yellow
    @"
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine

WORKDIR /app

COPY ./client-$arch/ ./

# Set permissions for the executable
RUN chmod +x ./BenchmarkingWithOtel.Client

# Environment variables can be set when running the container
# Example: docker run -e "ServiceUrl=http://server:5000" -e "OtelEndpoint=http://otelcollector:4317" benchmarking-client

# Set entry point to the application
ENTRYPOINT ["./BenchmarkingWithOtel.Client"]
"@ | Out-File -FilePath $dockerfilePath -Encoding UTF8

    # Build Docker image if not using multi-arch
    if (-not $useMultiArch) {
        Write-Host "Building Docker image for $arch with tag '$archTag'..." -ForegroundColor Yellow
        try {
            docker build -t $archTag -f $dockerfilePath $publishBaseDir

            if ($LASTEXITCODE -ne 0) {
                throw "Failed to build Docker image for $arch."
            }
            
            Write-Host "Docker image for $arch built successfully with tag '$archTag'." -ForegroundColor Green
        }
        catch {
            $errorMessage = "Error building Docker image for $arch"
            Write-Host "$errorMessage - $_" -ForegroundColor Red
            exit 1
        }
    }
}

# Build multi-architecture image if enabled
if ($useMultiArch) {
    Write-Host "`nBuilding multi-architecture Docker image with tag '$Tag'..." -ForegroundColor Yellow
    
    $buildxArgs = "buildx build --platform=" + ($archList -join ",") + " -t $Tag"
    
    # Add push flag if requested
    if ($PushImage) {
        if ([string]::IsNullOrEmpty($Registry)) {
            Write-Host "Error: Registry parameter is required when PushImage is true." -ForegroundColor Red
            exit 1
        }
        
        $remoteTag = "$Registry/$Tag"
        $buildxArgs += " -t $remoteTag --push"
        Write-Host "Will push multi-arch image to '$remoteTag'." -ForegroundColor Yellow
    }
    else {
        $buildxArgs += " --load"
    }
    
    # Add Dockerfile paths for each architecture
    $buildxArgs += " -f $($dockerfilePathTemplate -f $archList[0]) $publishBaseDir"
    
    try {
        Invoke-Expression "docker $buildxArgs"
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build multi-architecture Docker image."
        }
        
        Write-Host "Multi-architecture Docker image built successfully with tag '$Tag'." -ForegroundColor Green
    }
    catch {
        $errorMessage = "Error building multi-architecture Docker image"
        Write-Host "$errorMessage - $_" -ForegroundColor Red
        Write-Host "You may need to install Docker Buildx or a newer version of Docker to support multi-arch builds." -ForegroundColor Yellow
        exit 1
    }
}
else {
    # Handle registry push for single architecture images
    if ($PushImage) {
        if ([string]::IsNullOrEmpty($Registry)) {
            Write-Host "Error: Registry parameter is required when PushImage is true." -ForegroundColor Red
            exit 1
        }
        
        foreach ($archTag in $archTags) {
            $remoteTag = "$Registry/$archTag"
            Write-Host "Tagging Docker image for registry '$Registry'..." -ForegroundColor Yellow
            docker tag $archTag $remoteTag
            
            Write-Host "Pushing Docker image to registry..." -ForegroundColor Yellow
            try {
                docker push $remoteTag
                
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to push Docker image to registry."
                }
                
                Write-Host "Docker image pushed successfully to '$remoteTag'." -ForegroundColor Green
            }
            catch {
                $errorMessage = "Error pushing Docker image"
                Write-Host "$errorMessage - $_" -ForegroundColor Red
                exit 1
            }
        }
    }
}

# Save Docker images to files if requested
if ($SaveImage) {
    if ($useMultiArch) {
        # Multi-arch images can't be directly saved to a tar file, so save each architecture separately
        foreach ($arch in $archList) {
            $archTag = $Tag + "-" + $arch
            $archTarPath = $dockerImageTarPathTemplate -f $arch
            
            Write-Host "Saving Docker image for $arch to file '$archTarPath'..." -ForegroundColor Yellow
            try {
                docker save -o $archTarPath $archTag
                
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to save Docker image to file."
                }
                
                Write-Host "Docker image for $arch saved successfully to '$archTarPath'." -ForegroundColor Green
            }
            catch {
                $errorMessage = "Error saving Docker image"
                Write-Host "$errorMessage - $_" -ForegroundColor Red
            }
        }
    }
    else {
        # Save each architecture image
        foreach ($archTag in $archTags) {
            $arch = ($archTag).Replace($Tag + "-", "")
            $archTarPath = $dockerImageTarPathTemplate -f $arch
            
            Write-Host "Saving Docker image for $arch to file '$archTarPath'..." -ForegroundColor Yellow
            try {
                docker save -o $archTarPath $archTag
                
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to save Docker image to file."
                }
                
                Write-Host "Docker image for $arch saved successfully to '$archTarPath'." -ForegroundColor Green
            }
            catch {
                $errorMessage = "Error saving Docker image"
                Write-Host "$errorMessage - $_" -ForegroundColor Red
            }
        }
    }
}

# Clean up
Write-Host "`nCleaning up temporary files..." -ForegroundColor Yellow
Remove-Item -Recurse -Force $publishBaseDir

Write-Host "`nDocker image process completed successfully." -ForegroundColor Green
Write-Host "Image details:" -ForegroundColor Cyan
Write-Host " - Image tag: $Tag" -ForegroundColor White
Write-Host " - Architectures: $($archList -join ', ')" -ForegroundColor White

if ($SaveImage) {
    Write-Host " - Image files:" -ForegroundColor White
    foreach ($arch in $archList) {
        $archTarPath = $dockerImageTarPathTemplate -f $arch
        if (Test-Path $archTarPath) {
            $fileSize = (Get-Item $archTarPath).Length / 1MB
            Write-Host "   - $archTarPath ($([math]::Round($fileSize, 2)) MB)" -ForegroundColor White
        }
    }
}

if ($PushImage) {
    Write-Host " - Pushed to registry: $Registry" -ForegroundColor White
}

Write-Host "`nTo use these Docker images:" -ForegroundColor Cyan
Write-Host "1. Run the container directly:" -ForegroundColor White
Write-Host "   docker run -e \"ServiceUrl=http://server:5000\" -e \"OtelEndpoint=http://otelcollector:4317\" $Tag" -ForegroundColor White

if ($SaveImage) {
    Write-Host "2. Or load the saved image files on another machine:" -ForegroundColor White
    foreach ($arch in $archList) {
        $archTarPath = $dockerImageTarPathTemplate -f $arch
        Write-Host "   For $arch - docker load -i $archTarPath" -ForegroundColor White
    }
}

Write-Host "`nNote: When running on ARM64 devices (like Raspberry Pi or Apple Silicon):" -ForegroundColor Yellow
Write-Host "   docker run -e \"ServiceUrl=http://server:5000\" benchmarking-client:latest-arm64" -ForegroundColor White 