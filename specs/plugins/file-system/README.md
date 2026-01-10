# FileSystem Plugin Specification

## Overview

The FileSystem plugin (DotNetUp.Steps.FileSystem) provides atomic installation steps for managing files and directories during application installation. Each step handles a specific file operation and includes comprehensive validation, execution, and rollback logic.

## Supported Operations

1. **CopyFileStep** - Copy a single file with optional overwrite and permission handling
2. **CopyDirectoryStep** - Recursively copy a directory tree with selective filtering
3. **MoveFileStep** - Move a file from source to destination with validation
4. **MoveDirectoryStep** - Move a directory tree with validation
5. **DeleteFileStep** - Delete files with safety checks and logging
6. **DeleteDirectoryStep** - Recursively delete directory trees with validation
7. **CreateDirectoryStep** - Create directory structures with permission management
8. **SetFilePermissionsStep** - Set file/directory permissions and ownership
9. **CreateSymbolicLinkStep** - Create symbolic links (cross-platform)
10. **CreateJunctionStep** - Create directory junctions (Windows)
11. **ExtractArchiveStep** - Extract archives (ZIP, TAR, 7Z) to a target directory

## Design Principles

- **Atomic Operations** - Each step represents a single, well-defined file operation
- **Best-Effort Rollback** - Rollback attempts best-effort recovery; failures don't crash the system
- **Safe by Default** - Operations validate prerequisites; don't overwrite without confirmation
- **Cross-Platform** - Steps work on Windows, Linux, and macOS (with platform-specific notes)
- **Comprehensive Logging** - All operations log detailed information for troubleshooting

## Common Validation Patterns

### Pre-Execution Validation
- Check disk space availability
- Verify source file/directory exists
- Verify destination path accessibility
- Check file permissions (read/write)
- Validate paths don't exceed OS limits
- Check for conflicting files/directories

### Execution Context
- Use properties for configuration (paths, permissions, etc.)
- Store execution state for rollback (e.g., what was created/modified)
- Report progress for user feedback
- Handle cancellation tokens gracefully

### Rollback Strategy
- Track what was created/modified during execution
- Restore original files from backup if applicable
- Remove newly created files/directories
- Restore original permissions if modified
- Log all rollback actions
- Continue rollback even if individual operations fail

## Directory Structure

Each step has a dedicated specification file describing:
- Business need and use cases
- Step-specific validation requirements
- Execution logic and behavior
- Rollback logic and edge cases
- Parameters and configuration options
- Platform-specific considerations
