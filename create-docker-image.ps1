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

.PARAMETER Debug
  Enable debug output.
#>
param (
    [string]$Tag = "benchmarking-client:latest",
    [bool]$SaveImage = $true,
    [bool]$PushImage = $false,
    [string]$Registry = "",
    [string]$Architectures = "amd64,arm64",
    [switch]$Debug = $false
)

# Set error action preference
$ErrorActionPreference = "Stop"
if ($Debug) {
    $VerbosePreference = "Continue"
}

function Write-DebugMessage {
    param (
        [string]$Message
    )
    
    if ($Debug) {
        Write-Host "[DEBUG] $Message" -ForegroundColor Magenta
    }
}

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
    if ($Debug) {
        docker version
    }
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
        Write-DebugMessage "Checking Docker Buildx capability"
        docker buildx version | Out-Null
        if ($Debug) {
            docker buildx version
        }
        
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
        Write-DebugMessage "Docker Buildx error: $_"
        $useMultiArch = $false
    }
}

# Verify ARM64 support
if ($archList -contains "arm64") {
    Write-DebugMessage "Testing ARM64 support in current environment"
    
    try {
        # Test if ARM64 emulation is possible
        $testArm64 = docker run --rm --platform linux/arm64 alpine:latest uname -m 2>$null
        Write-DebugMessage "ARM64 test result: $testArm64"
        
        if ($testArm64 -ne "aarch64") {
            Write-Host "Warning: ARM64 emulation may not be properly configured on this system." -ForegroundColor Yellow
            Write-Host "The ARM64 build might fail or not work correctly." -ForegroundColor Yellow
        }
        else {
            Write-Host "ARM64 emulation confirmed working." -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Warning: Could not test ARM64 support. Error: $_" -ForegroundColor Yellow
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
        Write-DebugMessage "Running dotnet publish with runtime $runtimeId"
        
        $publishCommand = "dotnet publish $clientProjectPath " +
            "--configuration Release " +
            "--output $publishOutputPath " +
            "--runtime $runtimeId " +
            "--self-contained true " +
            "/p:PublishSingleFile=true " +
            "/p:PublishTrimmed=true"
        
        if ($Debug) {
            Write-Host $publishCommand -ForegroundColor DarkGray
        }
        
        Invoke-Expression $publishCommand
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to publish the Client application for $arch."
        }
        
        # Verify published executable exists
        $expectedExeName = "BenchmarkingWithOtel.Client"
        if ($arch -eq "amd64" -or $arch -eq "arm64") {
            # Linux executables don't have an extension
            $exePath = Join-Path $publishOutputPath $expectedExeName
        } else {
            $exePath = Join-Path $publishOutputPath "$expectedExeName.exe"
        }
        
        if (-not (Test-Path $exePath)) {
            throw "Published executable not found at $exePath"
        }
        
        Write-Host "Client application published successfully for $arch." -ForegroundColor Green
        Write-DebugMessage "Published executable: $exePath"
    }
    catch {
        $errorMessage = "Error publishing the Client application for $arch"
        Write-Host "$errorMessage - $_" -ForegroundColor Red
        Write-Host "Skipping this architecture and continuing with others." -ForegroundColor Yellow
        continue  # Skip this architecture but try others
    }
    
    # Create architecture-specific Dockerfile
    Write-Host "Creating Dockerfile for $arch..." -ForegroundColor Yellow
    
    $dockerfileContent = @"
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine

WORKDIR /app

COPY ./client-$arch/ ./

# Set permissions for the executable
RUN chmod +x ./BenchmarkingWithOtel.Client

# Environment variables can be set when running the container
# Example: docker run -e "ServiceUrl=http://server:5000" -e "OtelEndpoint=http://otelcollector:4317" benchmarking-client

# Set entry point to the application
ENTRYPOINT ["./BenchmarkingWithOtel.Client"]
"@
    
    $dockerfileContent | Out-File -FilePath $dockerfilePath -Encoding UTF8
    Write-DebugMessage "Created Dockerfile at $dockerfilePath"

    # Build Docker image if not using multi-arch
    if (-not $useMultiArch) {
        Write-Host "Building Docker image for $arch with tag '$archTag'..." -ForegroundColor Yellow
        try {
            $dockerPlatform = if ($arch -eq "amd64") { "linux/amd64" } else { "linux/arm64" }
            
            $buildCommand = "docker build --platform $dockerPlatform -t $archTag -f $dockerfilePath $publishBaseDir"
            Write-DebugMessage "Running Docker build: $buildCommand"
            
            if ($Debug) {
                Write-Host $buildCommand -ForegroundColor DarkGray
            }
            
            Invoke-Expression $buildCommand
            
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to build Docker image for $arch."
            }
            
            # Save Docker image to file
            if ($SaveImage) {
                $archTarPath = $dockerImageTarPathTemplate -f $arch
                Write-Host "Saving Docker image for $arch to file '$archTarPath'..." -ForegroundColor Yellow
                
                $saveCommand = "docker save -o $archTarPath $archTag"
                Write-DebugMessage "Running Docker save: $saveCommand"
                
                Invoke-Expression $saveCommand
                
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to save Docker image to file $archTarPath."
                }
                
                if (Test-Path $archTarPath) {
                    $fileSize = (Get-Item $archTarPath).Length / 1MB
                    $formattedSize = [math]::Round($fileSize, 2)
                    Write-Host "Docker image for $arch saved successfully to '$archTarPath' (Size - $formattedSize MB)." -ForegroundColor Green
                } else {
                    throw "Docker image file $archTarPath was not created."
                }
            }
            
            Write-Host "Docker image for $arch built successfully with tag '$archTag'." -ForegroundColor Green
        }
        catch {
            $errorMessage = "Error building/saving Docker image for $arch"
            Write-Host "$errorMessage - $_" -ForegroundColor Red
            Write-Host "Skipping this architecture and continuing with others." -ForegroundColor Yellow
            continue  # Skip this architecture but try others
        }
    }
}

# Build multi-architecture image if enabled
if ($useMultiArch) {
    Write-Host "`nBuilding multi-architecture Docker image with tag '$Tag'..." -ForegroundColor Yellow
    
    $platforms = ($archList | ForEach-Object { if ($_ -eq "amd64") { "linux/amd64" } else { "linux/arm64" } }) -join ","
    $buildxArgs = "buildx build --platform=$platforms -t $Tag"
    
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
        $buildCommand = "docker $buildxArgs"
        Write-DebugMessage "Running Docker Buildx: $buildCommand"
        
        if ($Debug) {
            Write-Host $buildCommand -ForegroundColor DarkGray
        }
        
        Invoke-Expression $buildCommand
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build multi-architecture Docker image."
        }
        
        Write-Host "Multi-architecture Docker image built successfully with tag '$Tag'." -ForegroundColor Green
        
        # Save Docker images to files if requested
        if ($SaveImage) {
            # Multi-arch images can't be directly saved to a tar file, so save each architecture separately
            foreach ($arch in $archList) {
                $dockerPlatform = if ($arch -eq "amd64") { "linux/amd64" } else { "linux/arm64" }
                $archTag = "$Tag-$arch"
                $archTarPath = $dockerImageTarPathTemplate -f $arch
                
                Write-Host "Saving Docker image for $arch to file '$archTarPath'..." -ForegroundColor Yellow
                
                # Need to create a separate tag for saving
                $tagCommand = "docker tag $Tag $archTag"
                Write-DebugMessage "Running Docker tag: $tagCommand"
                Invoke-Expression $tagCommand
                
                $saveCommand = "docker save -o $archTarPath $archTag"
                Write-DebugMessage "Running Docker save: $saveCommand"
                Invoke-Expression $saveCommand
                
                if ($LASTEXITCODE -ne 0) {
                    Write-Host "Warning: Could not save Docker image to file $archTarPath - $_" -ForegroundColor Yellow
                } 
                else {
                    if (Test-Path $archTarPath) {
                        $fileSize = (Get-Item $archTarPath).Length / 1MB
                        $formattedSize = [math]::Round($fileSize, 2)
                        Write-Host "Docker image for $arch saved successfully to '$archTarPath' (Size - $formattedSize MB)." -ForegroundColor Green
                    } 
                    else {
                        Write-Host "Warning: Docker image file $archTarPath was not created." -ForegroundColor Yellow
                    }
                }
            }
        }
    }
    catch {
        $errorMessage = "Error building multi-architecture Docker image"
        Write-Host "$errorMessage - $_" -ForegroundColor Red
        Write-Host "You may need to install Docker Buildx or a newer version of Docker to support multi-arch builds." -ForegroundColor Yellow
        Write-Host "Falling back to individual architecture builds..." -ForegroundColor Yellow
        
        # Fall back to individual builds for each architecture
        foreach ($arch in $archList) {
            if (-not ($archTags -contains "$Tag-$arch")) {
                Write-Host "Building fallback image for $arch..." -ForegroundColor Yellow
                $dockerPlatform = if ($arch -eq "amd64") { "linux/amd64" } else { "linux/arm64" }
                $archTag = "$Tag-$arch"
                $dockerfilePath = $dockerfilePathTemplate -f $arch
                
                $buildCommand = "docker build --platform $dockerPlatform -t $archTag -f $dockerfilePath $publishBaseDir"
                Write-DebugMessage "Running fallback Docker build: $buildCommand"
                
                try {
                    Invoke-Expression $buildCommand
                    
                    if ($LASTEXITCODE -ne 0) {
                        Write-Host "Warning: Failed to build fallback Docker image for $arch." -ForegroundColor Yellow
                        continue
                    }
                    
                    # Save Docker image to file
                    if ($SaveImage) {
                        $archTarPath = $dockerImageTarPathTemplate -f $arch
                        Write-Host "Saving fallback Docker image for $arch to file '$archTarPath'..." -ForegroundColor Yellow
                        
                        $saveCommand = "docker save -o $archTarPath $archTag"
                        Invoke-Expression $saveCommand
                        
                        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $archTarPath)) {
                            Write-Host "Warning: Could not save fallback Docker image to file $archTarPath" -ForegroundColor Yellow
                        } else {
                            $fileSize = (Get-Item $archTarPath).Length / 1MB
                            $formattedSize = [math]::Round($fileSize, 2)
                            Write-Host "Fallback Docker image for $arch saved successfully to '$archTarPath' (Size - $formattedSize MB)." -ForegroundColor Green
                        }
                    }
                }
                catch {
                    Write-Host "Warning: Failed to build/save fallback Docker image for $arch - $_" -ForegroundColor Yellow
                }
            }
        }
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

# Clean up
Write-Host "`nCleaning up temporary files..." -ForegroundColor Yellow
Remove-Item -Recurse -Force $publishBaseDir

# Display summary
Write-Host "`nDocker image process completed." -ForegroundColor Green
Write-Host "Image details:" -ForegroundColor Cyan
Write-Host " - Image tag: $Tag" -ForegroundColor White
Write-Host " - Architectures attempted: $($archList -join ', ')" -ForegroundColor White

# Check which image files actually exist
if ($SaveImage) {
    Write-Host " - Image files created:" -ForegroundColor White
    $createdImageCount = 0
    
    foreach ($arch in $archList) {
        $archTarPath = $dockerImageTarPathTemplate -f $arch
        if (Test-Path $archTarPath) {
            $fileSize = (Get-Item $archTarPath).Length / 1MB
            $formattedSize = [math]::Round($fileSize, 2)
            Write-Host "   - $archTarPath ($formattedSize MB)" -ForegroundColor White
            $createdImageCount++
        }
    }
    
    if ($createdImageCount -eq 0) {
        Write-Host "   None! No Docker image files were successfully created." -ForegroundColor Red
        Write-Host "Try running with -Debug switch for more information." -ForegroundColor Yellow
    }
}

if ($PushImage) {
    Write-Host " - Pushed to registry: $Registry" -ForegroundColor White
}

Write-Host "`nTo use these Docker images:" -ForegroundColor Cyan
Write-Host "1. Run the container directly:" -ForegroundColor White

$createdArches = @()
foreach ($arch in $archList) {
    $archTarPath = $dockerImageTarPathTemplate -f $arch
    if (Test-Path $archTarPath) {
        $createdArches += $arch
    }
}

if ($createdArches.Count -gt 0) {
    foreach ($arch in $createdArches) {
        $archTag = "$Tag-$arch"
        Write-Host "   For $arch - docker run -e \"ServiceUrl=http://server:5000\" $archTag" -ForegroundColor White
    }
    
    Write-Host "2. Or load the saved image files on another machine:" -ForegroundColor White
    foreach ($arch in $createdArches) {
        $archTarPath = $dockerImageTarPathTemplate -f $arch
        Write-Host "   For $arch - docker load -i $archTarPath" -ForegroundColor White
    }
    
    if ($createdArches -contains "arm64") {
        Write-Host "`nNote: When running on ARM64 devices (like Raspberry Pi or Apple Silicon):" -ForegroundColor Yellow
        Write-Host "   docker run -e \"ServiceUrl=http://server:5000\" $Tag-arm64" -ForegroundColor White
    }
} else {
    Write-Host "   No Docker images were successfully created. See errors above." -ForegroundColor Red
}

# Return 0 for success if at least one architecture was built
if ($createdArches.Count -gt 0) {
    exit 0
} else {
    exit 1
} 