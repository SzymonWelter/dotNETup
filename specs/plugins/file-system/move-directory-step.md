# MoveDirectoryStep Specification

## Business Need

Installers frequently need to move entire directory trees as part of installation workflows. This is essential for reorganizing application structures, consolidating files, or completing installation after extraction.

**Use Cases:**
- Move extracted application files to final installation location
- Reorganize application directory structure during setup
- Move entire plugin or extension directories
- Consolidate temporary installation files
- Move data directories to their final locations
- Upgrade scenarios where directory structure changes

## Parameters

The step accepts configuration through InstallationContext properties:

- `SourceDirectory` (string, required) - Full path to source directory
- `DestinationDirectory` (string, required) - Full path to destination directory
- `OverwriteExistingFiles` (bool, default: false) - Overwrite existing files in destination
- `MergeWithExisting` (bool, default: false) - Merge directory contents if destination exists
- `CreateDestinationIfMissing` (bool, default: true) - Create destination parent directories
- `DeleteSourceAfterMove` (bool, default: true) - Delete source directory after successful move
- `PreserveAttributes` (bool, default: true) - Copy file attributes during move
- `PreserveTimestamps` (bool, default: true) - Preserve file timestamps
- `BackupExisting` (bool, default: false) - Backup overwritten files
- `IncludeFilter` (string[], default: null) - Only move files matching patterns
- `ExcludeFilter` (string[], default: null) - Skip files matching patterns

## Validation Cases

### Source Directory Validation
- Source directory must exist
- Source directory must be readable and executable
- Source directory must be accessible
- Source directory path must be absolute
- Source directory must not be locked

### Destination Directory Validation
- Destination parent directory must exist or be creatable
- Destination must be writable
- Destination path must be valid
- Source and destination must not be the same
- Destination must not be inside source directory
- Path lengths must not exceed OS limits

### Merge Validation**
- If `MergeWithExisting=false` and destination exists, validation fails
- If `MergeWithExisting=true`:
  - Existing files must be handleable per overwrite settings
  - Check for file conflicts

### Environment Validation
- Sufficient disk space (if cross-volume move)
- Destination parent accessible and writable
- Source directory not in active use
- No circular directory references

## Execution Logic

### Sequential Steps

1. **Pre-Move Validation**
   - Verify source directory exists
   - Verify source is accessible and readable
   - Check if destination exists:
     - If exists and `MergeWithExisting=false`, fail
     - If exists and `MergeWithExisting=true`, prepare for merge
   - Create destination parent directory if needed
   - Check total size of source tree

2. **Move Strategy Selection**

   **Strategy A: Rename (Same Volume)**
   - If source and destination on same filesystem
   - Use OS rename operation (atomic and fast)
   - Minimal disk I/O

   **Strategy B: Copy+Delete (Cross-Volume)**
   - If move crosses filesystem boundaries
   - Copy entire directory tree to destination
   - Delete source tree after verification
   - More I/O but necessary for volume boundaries

3. **Move Operation**
   - Create destination directory structure
   - Move files from source to destination:
     - Apply include/exclude filters if provided
     - Handle overwrite settings per file
     - Create backups of overwritten files
     - Preserve attributes/timestamps
   - Report progress for large trees

4. **Source Cleanup**
   - If move successful:
     - Delete source directory (if `DeleteSourceAfterMove=true`)
     - Verify source removal
   - If move failed:
     - Leave source intact for rollback

5. **Success State**
   - Store in context:
     - `DestinationDirectory` - actual destination path
     - `FilesMoved` - count of files moved
     - `DirectoriesCreated` - count of directories created
     - `TotalBytesMoved` - total bytes transferred
     - `SourceDeleted` - whether source was removed
     - `BackupLocations` - map of backups (if created)
     - `MoveDuration` - time taken
     - `CrossVolume` - whether move crossed volumes

6. **Logging**
   - Log source and destination directories
   - Log file count, directory count, total size
   - Log move strategy used (rename vs copy+delete)
   - Log progress for large operations
   - Log any skipped files (filters)

## Rollback Logic

### Rollback Conditions
- If move failed partway through
- If subsequent steps failed after move succeeded
- If installation was cancelled

### Rollback Steps

1. **Restore Source Directory** (if source was deleted)
   - If cross-volume move and source was deleted:
     - Copy destination back to source location
     - Verify restoration
   - If rename move and source deleted manually, attempt recovery from destination

2. **Restore Backed-Up Files**
   - For each backed-up file, restore from backup location
   - Delete backup files
   - Verify restorations

3. **Clean Up Destination** (if needed)
   - If source restoration successful:
     - Delete destination directory
   - If source restoration failed:
     - Leave destination in place for manual recovery

4. **Handle Locked Files**
   - If files are locked, log warnings and continue
   - Best-effort approach

5. **Verification**
   - After rollback, verify source directory restored (or destination empty)
   - Log rollback completion

## Edge Cases

### Merge Scenarios
- **Destination exists, MergeWithExisting=false** - Fail validation
- **Destination exists, MergeWithExisting=true, OverwriteExistingFiles=false** - Merge but skip conflicting files
- **Destination exists, MergeWithExisting=true, OverwriteExistingFiles=true** - Merge and overwrite
- **Destination exists, BackupExisting=true** - Backup conflicting files before overwrite

### Cross-Volume Moves
- **Source and destination on different volumes** - Use copy+delete strategy
- **Copy succeeds but source delete fails** - Rollback can restore from destination
- **Partial copy operation** - Rollback removes incomplete destination

### Filter Scenarios
- **Include filter only** - Move only matching files
- **Exclude filter only** - Move all except matching files
- **Empty filter result** - If no files match, still move directory structure
- **Conflicting filters** - Handle correctly (include AND NOT exclude)

### Large Directory Trees
- **Many files (100,000+)** - Report progress frequently
- **Large total size (>10GB)** - Stream operations to avoid memory issues
- **Deep nesting** - Handle stack limits gracefully

### Special Cases
- **Empty source directory** - Move directory structure only
- **Symlinks in tree** - Preserve as symlinks during move
- **Hard links in tree** - Move as separate files
- **System/hidden files** - Include in move operation

### Concurrency
- **Source directory being read** - Move may succeed or fail per OS
- **Destination parent in use** - Create parent if possible
- **Files being written to source** - May fail depending on OS locks

## Success Criteria

✓ All files moved to correct destinations
✓ Directory structure preserved
✓ Files merged correctly (if applicable)
✓ File count and byte count accurate
✓ Existing files handled per configuration
✓ Attributes/timestamps preserved if requested
✓ Backups created for overwritten files
✓ Source directory removed (if configured)
✓ All operations logged
✓ Rollback restores source or removes destination
✓ Handles both small and large directory trees

## Error Messages

- `"Source directory not found: {path}"` - Source doesn't exist
- `"Destination directory already exists: {path}. Set MergeWithExisting to true to merge."` - Destination exists
- `"Cannot create destination parent directory: {path}"` - Permission denied
- `"Insufficient disk space for cross-volume move"` - Not enough space
- `"Source and destination are the same"` - Invalid configuration
- `"Circular directory reference detected"` - Destination inside source
- `"Cannot delete source directory after move"` - Cleanup failed
- `"Directory move failed: {reason}"` - Generic move failure

## Platform-Specific Notes

### Windows
- Respects file attributes
- Handles UNC paths and network shares
- MoveFile API may fail on directories with open files
- Cross-volume moves use copy+delete
- Recursive directory operations may be slower

### Linux/macOS
- Rename operation is atomic on same filesystem
- Cross-filesystem moves detected and use copy+delete
- Case-sensitive file systems
- Respects Unix permissions
- Generally faster cross-filesystem operations

## Related Steps

- **CopyDirectoryStep** - Used internally for cross-volume moves
- **DeleteDirectoryStep** - Used internally for source cleanup
- **CreateDirectoryStep** - Often run before move
- **SetFilePermissionsStep** - Adjust permissions on entire tree

## Decision Points

**When to use MoveDirectoryStep vs CopyDirectoryStep:**
- Use MoveDirectoryStep when source should not remain
- Use CopyDirectoryStep when source needs to be preserved
- Use MoveDirectoryStep for consolidation and reorganization
- Use CopyDirectoryStep for template/pattern-based distribution
