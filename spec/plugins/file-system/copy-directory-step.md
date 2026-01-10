# CopyDirectoryStep Specification

## Business Need

Installers frequently need to copy entire directory trees during installation. Examples include copying application binaries, configuration templates, documentation, scripts, and resource files in bulk to their installation locations.

**Use Cases:**
- Copy entire application directory structure to Program Files
- Copy configuration templates to system directories
- Copy documentation and help files
- Copy multi-language resource packs
- Copy plugin or extension directories
- Copy entire folder hierarchies with selective filtering

## Parameters

The step accepts configuration through InstallationContext properties:

- `SourceDirectory` (string, required) - Full path to source directory
- `DestinationDirectory` (string, required) - Full path to destination directory
- `Recursive` (bool, default: true) - Include subdirectories
- `OverwriteExistingFiles` (bool, default: false) - Overwrite existing files in destination
- `PreserveAttributes` (bool, default: true) - Copy file attributes
- `PreserveTimestamps` (bool, default: true) - Preserve original timestamps
- `CreateDestinationIfMissing` (bool, default: true) - Create destination directory if missing
- `IncludeFilter` (string[], default: null) - Only copy files matching patterns (e.g., *.dll, *.config)
- `ExcludeFilter` (string[], default: null) - Skip files matching patterns (e.g., *.tmp, *.log)
- `BackupExisting` (bool, default: false) - Create backup of overwritten files
- `SkipHiddenFiles` (bool, default: false) - Skip hidden and system files
- `PreserveDirectoryStructure` (bool, default: true) - Recreate full directory structure

## Validation Cases

### Source Directory Validation
- Source directory must exist
- Source directory must be readable
- Source directory must be accessible (not in use)
- Source directory path must be absolute
- Source and destination must not be the same

### Destination Directory Validation
- Destination directory must be writable or creatable
- Destination directory path must be valid
- Path length must not exceed OS limits for any file in tree
- Sufficient permissions to create subdirectories

### Filter Validation
- Include and exclude filters must be valid patterns
- Filters must not contradict each other (e.g., include *.dll and exclude *.dll)
- Pattern syntax must be valid glob patterns

### Environment Validation
- Sufficient disk space for entire tree
- Destination directory and parent must be accessible
- No circular directory references (source not inside destination)
- Source directory not locked by another process

## Execution Logic

### Sequential Steps

1. **Pre-Copy Validation**
   - Verify source directory exists and is readable
   - Create destination directory if needed and allowed
   - Check total size of source tree against available disk space
   - Validate all paths for length limits

2. **Directory Enumeration**
   - Build list of files to copy from source tree
   - Apply include/exclude filters
   - Skip hidden files if configured
   - Sort files for consistent copying order

3. **Copy Operation**
   - For each file in enumerated list:
     - Create subdirectory structure in destination
     - Copy file with attributes/timestamps if configured
     - Verify copy completed
     - Handle errors gracefully (continue or fail-fast based on settings)
     - Report progress (current file, percentage complete)

4. **Directory Structure Preservation**
   - Create empty subdirectories even if no files match
   - Preserve relative paths from source to destination
   - Handle deeply nested directories

5. **Success State**
   - Store in context:
     - `DestinationDirectory` - actual destination path
     - `FilesCopied` - count of files copied
     - `DirectoriesCreated` - count of directories created
     - `TotalBytesCopied` - total bytes transferred
     - `BackupLocations` - map of backup locations (if created)
     - `CopyDuration` - time taken for entire operation

6. **Logging**
   - Log source and destination directories
   - Log file count and total size
   - Log files skipped (by filter or error)
   - Log any files that failed
   - Report progress periodically

## Rollback Logic

### Rollback Conditions
- If execution failed partway through copying
- If subsequent steps failed after copy succeeded
- If installation was cancelled during copy

### Rollback Steps

1. **Restore Backups** (if created)
   - For each backed up file, restore from backup
   - Delete backup locations
   - Verify restoration

2. **Remove Copied Files and Directories**
   - Delete all files copied during execution
   - Delete empty subdirectories created
   - Preserve non-empty directories that existed before
   - Work bottom-up to avoid delete failures

3. **Handle Locked Files**
   - If any file is locked, log warning and continue
   - Don't fail rollback due to locked files
   - Best-effort approach

4. **Verification**
   - After rollback, destination should be in original state
   - Log rollback completion and any files that couldn't be rolled back

## Edge Cases

### Overwrite Scenarios
- **Files exist, OverwriteExistingFiles=false** - Skip existing, copy new ones only
- **Files exist, OverwriteExistingFiles=true, BackupExisting=false** - Overwrite without backup
- **Files exist, OverwriteExistingFiles=true, BackupExisting=true** - Create backups, then overwrite
- **Partial directory exists** - Merge with existing directory structure

### Filter Scenarios
- **Include filter only** - Only copy matching files
- **Exclude filter only** - Copy all except matching files
- **Both filters** - Apply both (file must match include AND not match exclude)
- **Empty result** - If filters result in no files, still create directory structure

### Large Directories
- **Many files (10,000+)** - Report progress frequently
- **Large files (>1GB)** - Stream copy to avoid memory issues
- **Deep nesting (50+ levels)** - Handle stack limits gracefully

### Special Cases
- **Empty source directory** - Copy directory structure but no files
- **Symlinks in source tree** - Copy as symlinks (not dereferenced)
- **Hard links in source** - Copy as separate files (not linked)
- **File system boundaries** - Handle correctly across mount points

### Performance
- **Skip files shouldn't accumulate memory** - Stream enumeration
- **Progress reporting shouldn't impact performance** - Report every Nth file
- **Concurrent operations** - Source can be read while copying

## Success Criteria

✓ All non-filtered files copied to correct destinations
✓ Directory structure preserved or recreated as specified
✓ File count and byte count accurate
✓ Attributes/timestamps preserved if requested
✓ Existing files handled per configuration
✓ Filters applied correctly
✓ All operations logged
✓ Rollback removes all copied files and restores backups
✓ Handles both small and large directory trees

## Error Messages

- `"Source directory not found: {path}"` - Source doesn't exist
- `"Cannot create destination directory: {path}"` - Permission denied
- `"Insufficient disk space. Need {bytes}, available {bytes}"` - Not enough space
- `"Path too long: {path}"` - Exceeds OS path limits
- `"Source and destination are the same: {path}"` - Invalid configuration
- `"Circular directory reference detected"` - Destination inside source
- `"Invalid filter pattern: {pattern}"` - Malformed glob pattern
- `"File copy failed: {file} - {reason}"` - Individual file copy failure

## Platform-Specific Notes

### Windows
- Respects file attributes (hidden, system, read-only)
- Handles UNC paths and network shares
- 260 character path limit applies to each file
- Junction points and symlinks handled correctly

### Linux/macOS
- Case-sensitive file systems; handle correctly
- Respects Unix permissions and ownership
- Symlinks preserved in tree (not dereferenced)
- No practical path length limit
- May have different performance characteristics with very large trees

## Related Steps

- **CreateDirectoryStep** - Manual directory creation if more control needed
- **SetFilePermissionsStep** - Adjust permissions on entire tree post-copy
- **DeleteDirectoryStep** - Cleanup if installation rolls back
- **CopyFileStep** - Single file copy if only specific files needed
