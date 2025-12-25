#!/bin/bash
#
# macOS Ad-hoc Code Signing Script
# 
# This script performs ad-hoc code signing on macOS builds for local testing.
# Ad-hoc signing allows the app to run on your own Mac without a Developer certificate.
#
# Usage:
#   ./sign-macos.sh [architecture]
#
# Arguments:
#   architecture - Optional. One of: osx-x64, osx-arm64, or 'all' (default: all)
#
# Examples:
#   ./sign-macos.sh              # Sign all macOS builds
#   ./sign-macos.sh osx-arm64    # Sign only ARM64 build
#
# Note: This script must be run on macOS after building the application.
#

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PUBLISH_DIR="$SCRIPT_DIR/publish"

# Determine which architectures to sign
ARCH="${1:-all}"

if [ "$ARCH" = "all" ]; then
    ARCHITECTURES=("osx-x64" "osx-arm64")
else
    ARCHITECTURES=("$ARCH")
fi

echo -e "${CYAN}macOS Ad-hoc Code Signing${NC}"
echo -e "${CYAN}==========================${NC}\n"

# Check if we're on macOS
if [ "$(uname)" != "Darwin" ]; then
    echo -e "${RED}Error: This script must be run on macOS${NC}"
    exit 1
fi

# Check if publish directory exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo -e "${RED}Error: Publish directory not found: $PUBLISH_DIR${NC}"
    echo -e "${YELLOW}Please run build.ps1 first to build the application.${NC}"
    exit 1
fi

# Function to sign a single architecture
sign_architecture() {
    local arch=$1
    local arch_dir="$PUBLISH_DIR/$arch"
    
    echo -e "${CYAN}Signing $arch...${NC}"
    
    # Check if architecture directory exists
    if [ ! -d "$arch_dir" ]; then
        echo -e "${YELLOW}  Warning: Directory not found: $arch_dir${NC}"
        echo -e "${YELLOW}  Skipping...${NC}\n"
        return 1
    fi
    
    # Find the main executable
    local exe_path="$arch_dir/StickyNotesClassic.App"
    
    if [ ! -f "$exe_path" ]; then
        echo -e "${YELLOW}  Warning: Executable not found: $exe_path${NC}"
        echo -e "${YELLOW}  Skipping...${NC}\n"
        return 1
    fi
    
    # Perform ad-hoc signing
    # -s - means ad-hoc signing (no certificate)
    # -f forces re-signing if already signed
    # --deep signs all nested code (frameworks, bundles, etc.)
    if codesign -s - -f --deep "$exe_path" 2>&1; then
        echo -e "${GREEN}  ✓ Successfully signed: $exe_path${NC}"
        
        # Verify the signature
        if codesign --verify --verbose "$exe_path" 2>&1; then
            echo -e "${GREEN}  ✓ Signature verified${NC}"
        else
            echo -e "${YELLOW}  ⚠ Warning: Signature verification failed${NC}"
        fi
        
        # Display signature info
        echo -e "  ${CYAN}Signature info:${NC}"
        codesign -dvv "$exe_path" 2>&1 | grep -E "Identifier|Authority|Signature" | sed 's/^/    /'
        
        echo ""
        return 0
    else
        echo -e "${RED}  ✗ Failed to sign: $exe_path${NC}\n"
        return 1
    fi
}

# Sign each architecture
success_count=0
failure_count=0

for arch in "${ARCHITECTURES[@]}"; do
    if sign_architecture "$arch"; then
        ((success_count++))
    else
        ((failure_count++))
    fi
done

# Summary
echo -e "${CYAN}=====================================${NC}"
echo -e "${CYAN}Signing Summary${NC}"
echo -e "${CYAN}=====================================${NC}"
echo -e "Total: ${#ARCHITECTURES[@]}"
echo -e "${GREEN}Successful: $success_count${NC}"

if [ $failure_count -gt 0 ]; then
    echo -e "${RED}Failed: $failure_count${NC}"
else
    echo -e "${GREEN}Failed: $failure_count${NC}"
fi

echo ""

if [ $failure_count -gt 0 ]; then
    echo -e "${YELLOW}Some builds failed to sign.${NC}"
    exit 1
else
    echo -e "${GREEN}All builds signed successfully!${NC}"
    echo -e "${CYAN}The applications can now be run on this Mac.${NC}"
    exit 0
fi
