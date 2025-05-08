#!/bin/bash
# Shell script wrapper for create-docker-image.ps1
# This helps users on Linux/macOS to create Docker images for the Client application

# Check if PowerShell is installed
if ! command -v pwsh &> /dev/null; then
    echo "PowerShell (pwsh) is not installed or not in your PATH."
    echo "Please install PowerShell for your platform:"
    echo "  Linux: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-linux"
    echo "  macOS: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-macos"
    exit 1
fi

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "Docker is not installed or not in your PATH."
    echo "Please install Docker for your platform:"
    echo "  Docker Desktop: https://www.docker.com/products/docker-desktop/"
    echo "  Docker Engine: https://docs.docker.com/engine/install/"
    exit 1
fi

# Get the directory where this script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Pass all arguments to the PowerShell script
echo "Running create-docker-image.ps1 with PowerShell"
pwsh -File "$SCRIPT_DIR/create-docker-image.ps1" "$@"

# Check the exit code
if [ $? -ne 0 ]; then
    echo "Docker image creation failed. Check the output above for errors."
    exit 1
fi

echo "Docker image creation completed successfully!" 