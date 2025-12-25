# macOS Code Signing Guide

This guide explains how to sign the Sticky Notes Classic application for macOS testing.

## Why Sign?

macOS requires applications to be code-signed before they can run. Without signing, macOS will block the application with a security warning.

## Ad-hoc Signing (For Testing)

Ad-hoc signing allows you to test the application on your own Mac without an Apple Developer certificate.

### Prerequisites

- A Mac running macOS (signing cannot be done on Windows or Linux)
- The `codesign` tool (included with macOS by default)
- Built application binaries (run `build.ps1` first)

### Steps

1. **Build the application on Windows** (or any platform):
   ```powershell
   .\build.ps1 -Architecture osx-arm64 -SelfContained -SingleFile
   # Or build for both macOS architectures:
   .\build.ps1 -Architecture all -SelfContained -SingleFile
   ```

2. **Transfer the `publish` directory to your Mac**:
   - Copy the entire `publish` folder to your Mac
   - Also copy the `sign-macos.sh` script

3. **Make the signing script executable**:
   ```bash
   chmod +x sign-macos.sh
   ```

4. **Run the signing script on macOS**:
   ```bash
   # Sign all macOS builds
   ./sign-macos.sh
   
   # Or sign a specific architecture
   ./sign-macos.sh osx-arm64
   ```

5. **Run the application**:
   ```bash
   cd publish/osx-arm64
   ./StickyNotesClassic.App
   ```

### What the Script Does

The `sign-macos.sh` script:
- Finds the application executable in the `publish` directory
- Performs ad-hoc code signing using `codesign -s -`
- Verifies the signature
- Displays signature information

### Troubleshooting

**"Operation not permitted" error**:
- The executable may not have execute permissions
- Run: `chmod +x publish/osx-arm64/StickyNotesClassic.App`

**"Cannot be opened because the developer cannot be verified"**:
- Right-click the app and select "Open"
- Click "Open" again in the dialog
- Or run from terminal: `xattr -cr publish/osx-arm64/StickyNotesClassic.App`

**"Code signing failed"**:
- Ensure you're running on macOS (not Windows/Linux)
- Check that the executable exists in the publish directory

## Developer Certificate Signing (For Distribution)

For distributing to other users, you'll need:

1. **Apple Developer Account** ($99/year)
2. **Developer ID Application Certificate**

### Getting a Certificate

1. Join the [Apple Developer Program](https://developer.apple.com/programs/)
2. Generate a "Developer ID Application" certificate in Xcode or on the Apple Developer website
3. Download and install the certificate on your Mac

### Signing with Certificate

Replace the ad-hoc signing command with:

```bash
codesign -s "Developer ID Application: Your Name (TEAM_ID)" \
  --deep \
  --force \
  --options runtime \
  --timestamp \
  publish/osx-arm64/StickyNotesClassic.App
```

### Notarization (Required for Distribution)

For macOS 10.15+ (Catalina and later), you must also notarize the app:

```bash
# Create a zip
cd publish/osx-arm64
zip -r StickyNotesClassic.zip StickyNotesClassic.App

# Submit for notarization
xcrun notarytool submit StickyNotesClassic.zip \
  --apple-id "your@email.com" \
  --password "app-specific-password" \
  --team-id "TEAM_ID" \
  --wait

# Staple the notarization ticket
xcrun stapler staple StickyNotesClassic.App
```

## Additional Resources

- [Apple Code Signing Guide](https://developer.apple.com/support/code-signing/)
- [Notarization Documentation](https://developer.apple.com/documentation/security/notarizing_macos_software_before_distribution)
- [codesign Manual](https://www.manpagez.com/man/1/codesign/)
