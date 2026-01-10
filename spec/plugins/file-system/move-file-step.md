# MoveFileStep Specification

## Business Need

Installers sometimes need to move files from one location to another (not just copy). This is useful for cleanup operations or for organizing files into their final structure when source cleanup is necessary.

**Use Cases:**
- Move temporary installation files to final location after extraction
- Consolidate scattered files during installation
- Move files between drives/volumes as part of installation
- Reorganize application structure during setup
- Move configuration files during migration or upgrade

## Parameters

The step accepts configuration through InstallationContext properties:

- `SourcePath` (string, required) - Full path to source file
- `DestinationPath` (string, required) - Full path to destination file
- `OverwriteIfExists` (bool, default: false) - Whether to overwrite existing files
- `PreserveTimestamps` (bool, default: true) - Preserve original file timestamps
- `CreateDestinationDirectoryIfNeeded` (bool, default: true) - Create parent directories if missing
- `BackupExisting` (bool, default: false) - Create backup of existing file before overwriting
- `DeleteSourceOnSuccess` (bool, default: true) - Delete source after successful move

## Validation Cases

### Source File Validation
- Source file must exist
- Source file must be readable
- Source file path must be absolute (or relative to InstallationPath)
- Source file must not be locked by another process
- Source file must not be a directory

### Destination Path Validation
- Destination directory must exist or be creatable
- Destination path must be writable
- Destination directory path must not exceed OS path limits
- If `OverwriteIfExists=false` and file exists, validation fails
- If `OverwriteIfExists=true` and file is locked, validation fails
- Destination must not be inside source directory structure
- Source and destination must not be the same file

### Environment Validation
- Sufficient disk space (if moving across volumes)
- Required permissions for both read (source) and write (destination)
- Parent directory of destination accessible

## Execution Logic

### Sequential Steps

1. **Pre-Move Validation**
   - Verify source file exists and is readable
   - Verify source file is not locked
   - Verify destination directory is accessible (create if needed)
   - Check if destination file exists:
     - If exists and `OverwriteIfExists=false`, fail
     - If exists and `OverwriteIfExists=true`:
       - If `BackupExisting=true`, create backup
       - Mark for overwrite

2. **Move Operation** (Two Strategies)

   **Strategy A: Same Volume (Preferred)**
   - If source and destination on same volume
   - Use OS rename/move operation (atomic)
   - Fast and maintains original timestamps

   **Strategy B: Cross-Volume (Fallback)**
   - If volumes differ:
     - Copy file from source to destination
     - Verify copy successful
     - Delete source file
   - Less atomic but necessary for volume boundaries

3. **Verification**
   - Verify destination file exists
   - Verify file size matches source
   - Verify source file deleted (if not on same volume)
   - Verify timestamps preserved if requested

4. **Success State**
   - Store in context:
     - `MovedFilePath` - actual destination path
     - `SourceDeleted` - whether source was removed
     - `BackupPath` - path to backup file (if created)
     - `FileSize` - size of moved file
     - `MoveDuration` - time taken
     - `CrossVolume` - whether move crossed volumes

5. **Logging**
   - Log source and destination paths
   - Log file size and move strategy used
   - Log if backup was created
   - Report progress

## Rollback Logic

### Rollback Conditions
- If move failed after partial completion
- If subsequent steps failed after move succeeded
- If installation was cancelled

### Rollback Steps

1. **Handle Source File**
   - If source file still exists, no action needed
   - If source was deleted (cross-volume move):
     - Restore source from destination (reverse the move)
     - Verify restoration

2. **Restore Backup** (if exists)
   - If backup exists, restore to destination
   - Delete backup file
   - Verify restoration

3. **Remove Destination File** (if copy-based move)
   - If cross-volume move and rollback required:
     - Delete destination file
     - Source will be restored in step 1

4. **Handle Locked Files**
   - If file is locked, log warning and continue
   - Don't crash on locked file

5. **Verification**
   - Verify source file restored
   - Verify destination removed or contains backup
   - Log rollback completion

## Edge Cases

### Cross-Volume Moves
- **Source and destination on different volumes** - Use copy+delete strategy
- **Copy succeeds but source delete fails** - Rollback can restore from destination
- **Partial write during copy** - Rollback removes incomplete destination

### Overwrite Scenarios
- **File exists, OverwriteIfExists=false** - Fail validation
- **File exists, OverwriteIfExists=true, BackupExisting=false** - Overwrite without backup
- **File exists, OverwriteIfExists=true, BackupExisting=true** - Backup exists file, then overwrite

### Special Files
- **Read-only source file** - Move succeeds; destination inherits attributes
- **Very large files** - Monitor disk space during copy-based move
- **Hidden/system files** - Move including attributes
- **Symlinks as source** - Move the link, not the target

### Concurrency
- **Source file being read** - Should succeed (concurrent reads allowed)
- **Destination being written** - Will fail validation
- **Source file deleted during move** - Fail with clear error

### Path Issues
- **Same source and destination** - Fail with validation error
- **Destination inside source path** - Fail validation
- **Very long paths** - Validate against OS limits
- **Paths with special characters** - Handle correctly

## Success Criteria

✓ File moved to correct destination
✓ File size and content matches source
✓ Source file removed (unless cross-volume and backup required)
✓ Attributes/timestamps preserved if requested
✓ Backup created if overwriting existing file
✓ All operations logged
✓ Rollback restores original state (source restored or removed)

## Error Messages

- `"Source file not found: {path}"` - Source doesn't exist
- `"Source file is locked by another process"` - Cannot read source
- `"Destination file already exists: {path}. Set OverwriteIfExists to true to overwrite."` - File exists
- `"Cannot create destination directory: {path}"` - Permission denied
- `"Cannot delete source file after copy: {path}"` - Cleanup failed in cross-volume move
- `"Move failed: {reason}"` - Generic move failure

## Platform-Specific Notes

### Windows
- Respects file attributes (hidden, system, read-only)
- Handles UNC paths and network shares
- MoveFile API may fail on locked files
- Cross-volume moves use copy+delete
- 260 character path limit applies

### Linux/macOS
- Respects Unix permissions
- Rename operation is atomic on same filesystem
- Cross-filesystem moves detected and use copy+delete
- Case-sensitive file systems
- Can move across filesystems

## Related Steps

- **CopyFileStep** - Used internally for cross-volume moves
- **DeleteFileStep** - Used internally for source cleanup
- **CreateDirectoryStep** - Often run before MoveFileStep
- **SetFilePermissionsStep** - Adjust permissions post-move if needed

## Decision Points

**When to use MoveFileStep vs CopyFileStep:**
- Use MoveFileStep when source should not remain after installation
- Use CopyFileStep when source cleanup is not guaranteed safe
- Use MoveFileStep for temporary file organization
- Use CopyFileStep for template/pattern-based file distribution
