#!/bin/bash
# Simple shell script wrapper for publish-and-zip.ps1
# This helps users on Linux/macOS to run the publishing script
# By default, it will build for Windows (x64), Linux (x64), macOS (x64), and macOS (ARM64)

# Check if PowerShell is installed
if ! command -v pwsh &> /dev/null; then
    echo "PowerShell (pwsh) is not installed or not in your PATH."
    echo "Please install PowerShell for your platform:"
    echo "  Linux: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-linux"
    echo "  macOS: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-macos"
    exit 1
fi

# Get the directory where this script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Pass all arguments to the PowerShell script
echo "Running publish-and-zip.ps1 with PowerShell"
echo "Building for Windows (x64), Linux (x64), macOS (x64), and macOS (ARM64) by default"
pwsh -File "$SCRIPT_DIR/publish-and-zip.ps1" "$@"

# Check the exit code
if [ $? -ne 0 ]; then
    echo "Publishing failed. Check the output above for errors."
    exit 1
fi

echo "Publishing completed successfully!" 