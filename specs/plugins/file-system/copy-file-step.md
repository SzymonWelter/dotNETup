# CopyFileStep Specification

## Business Need

Installers frequently need to copy individual files from source locations (setup media, temporary directories) to their final installation locations. This is a fundamental operation in any installation workflow.

**Use Cases:**
- Copy application binaries to program files
- Copy configuration files to installation directory
- Copy license files to system directories
- Copy user documentation to designated folders
- Copy DLLs/assemblies to specific locations
- Copy scripts and tools during installation

## Parameters

The step accepts configuration through InstallationContext properties:

- `SourcePath` (string, required) - Full path to source file
- `DestinationPath` (string, required) - Full path to destination file
- `OverwriteIfExists` (bool, default: false) - Whether to overwrite existing files
- `PreserveTimestamps` (bool, default: true) - Preserve original file timestamps
- `CopyAttributes` (bool, default: true) - Copy file attributes (read-only, hidden, etc.)
- `BackupExisting` (bool, default: false) - Create backup of existing file before overwriting
- `CreateDirectoriesIfNeeded` (bool, default: true) - Create parent directories if missing

## Validation Cases

### Source File Validation
- Source file must exist
- Source file must be readable
- Source file path must be absolute (or relative to InstallationPath)
- Source file must not exceed disk read limits
- Source file must be accessible (not locked by another process)

### Destination Path Validation
- Destination directory must exist or be creatable
- Destination path must be writable
- Destination directory path must not exceed OS path length limits (260 chars on Windows, varies on Unix)
- If `OverwriteIfExists=false` and file exists, validation fails
- If `OverwriteIfExists=true` and file is locked, validation fails
- Destination must not be inside source (no circular copies)

### Environment Validation
- Sufficient disk space for the file
- No permission restrictions preventing write access
- Parent directories are accessible

## Execution Logic

### Sequential Steps

1. **Pre-Copy Validation**
   - Verify source file exists and is readable
   - Verify destination directory is accessible (create if needed and allowed)
   - If destination file exists:
     - If `OverwriteIfExists=false`, fail with clear error
     - If `OverwriteIfExists=true`:
       - If `BackupExisting=true`, create backup (.backup or .bak extension)
       - Mark existing file for potential restoration

2. **Copy Operation**
   - Copy file from source to destination
   - Preserve attributes if configured
   - Preserve timestamps if configured
   - Handle partial writes gracefully (cleanup on error)

3. **Verification**
   - Verify copied file exists
   - Verify file size matches source
   - Optionally verify file hash/checksum matches

4. **Success State**
   - Store in context:
     - `CopiedFilePath` - actual destination path
     - `BackupPath` - path to backup file (if created)
     - `FileSize` - size of copied file in bytes
     - `CopyDuration` - time taken to copy

5. **Logging**
   - Log source path, destination path, file size
   - Log if backup was created
   - Report progress with bytes copied
   - Log timing information

## Rollback Logic

### Rollback Conditions
- If execution failed after file was partially written
- If subsequent steps failed after copy succeeded
- If installation was cancelled during copy

### Rollback Steps

1. **Remove Copied File** (if fully written)
   - Delete destination file
   - Log removal

2. **Restore Backup** (if backup exists)
   - Restore original file from backup location
   - Verify restoration completed
   - **Note:** Backup file deletion is handled by DisposeAsync (see Disposal Logic below)
   - Log restoration

3. **Handle Locked Files**
   - If destination is locked, log warning and continue
   - Don't crash on locked file; best-effort approach

4. **Verification**
   - After rollback, verify original state is restored (or destination is gone)
   - Log rollback completion

### Design Note: Backup Deletion

Backup files are **NOT** deleted in RollbackAsync. Instead, deletion is handled exclusively by `DisposeAsync()` for these reasons:

- **Guaranteed Execution:** DisposeAsync is always called in finally block
- **Exception Safety:** If rollback throws after restore, backup still cleaned up
- **Single Responsibility:** Rollback = restore state, Disposal = cleanup resources
- **ContinueOnError Support:** Handles scenarios where rollback isn't called

---

## Disposal Logic (Resource Cleanup)

### Purpose
The `DisposeAsync()` method is the **single source of truth** for backup file cleanup. It has two responsibilities:

1. **Restore backup** if copy failed (prevents data corruption)
2. **Delete backup file** in all cases (prevents orphaned resources)

This design ensures cleanup happens regardless of:
- Success/failure status
- Whether rollback was called
- ContinueOnError settings
- Exceptions thrown during rollback

### Why Not Delete in RollbackAsync?

Critical architectural decision: backup deletion happens ONLY in DisposeAsync:

| Concern | Problem if deleted in Rollback | Solution with DisposeAsync |
|---------|-------------------------------|----------------------------|
| **Guaranteed Execution** | Rollback may not be called | DisposeAsync always called (finally block) |
| **Exception Safety** | If rollback throws after restore, backup orphaned | Finally block guarantees execution |
| **Code Success** | When copy succeeds, rollback not called | DisposeAsync handles all success cases |
| **Single Responsibility** | Rollback does restore AND cleanup | Rollback = restore, Dispose = cleanup |

### Disposal Behavior

**Scenario 1: Copy Failed, No Rollback Called**
- Backup exists, `_copySucceeded=false`
- **Action:**
  1. Restore backup to destination (prevents data corruption)
  2. Delete backup file
- **Example:** ContinueOnError=true, copy fails, installation continues
- **Rationale:** Matches Windows Installer - original file always preserved

**Scenario 2: Copy Succeeded, No Rollback Called**
- Backup exists, `_copySucceeded=true`
- **Action:** Delete backup file (no restore needed)
- **Example:** ContinueOnError=true, later step fails, installation continues
- **Rationale:** Backup no longer needed, prevent orphaned resources

**Scenario 3: Rollback Called, Then Dispose**
- Backup exists (restored in rollback, but not deleted)
- **Action:** Delete backup file
- **Example:** Normal rollback flow, then DisposeAsync in finally block
- **Rationale:** Rollback restored file, DisposeAsync cleans up backup

### Implementation Notes
- Called automatically by Installation executor in finally block
- **Always called** after ExecuteAsync/RollbackAsync complete (or fail)
- Best-effort: silently ignores errors (can't log without context)
- Idempotent: safe to call multiple times
- Guarantees no orphaned backup files
- Simpler rollback code (no cleanup logic mixed with restore logic)

## Edge Cases

### Overwrite Scenarios
- **File exists, OverwriteIfExists=false** - Fail validation
- **File exists, OverwriteIfExists=true, BackupExisting=false** - Overwrite without backup
- **File exists, OverwriteIfExists=true, BackupExisting=true** - Create backup, then overwrite
- **Destination locked during copy** - Fail with clear error about locked file

### Special Files
- **Read-only source files** - Copy succeeds; destination may be read-only too
- **Hidden/system files** - Copy including attributes if configured
- **Very large files** - Report progress periodically during copy
- **Sparse files** - Handle efficiently (copy sparse structure if supported)

### Path Issues
- **UNC paths (network shares)** - Support with appropriate access checks
- **Paths with spaces** - Handle correctly
- **Very long paths** - Validate against OS limits
- **Symlinks as source** - Copy the file, not the link

### Concurrency
- **Source file being read** - Should succeed (concurrent reads allowed)
- **Destination being written** - Will fail; validation should catch this
- **Directory being deleted during copy** - Rollback should handle gracefully

## Success Criteria

✓ File copied to correct destination
✓ File size and content matches source
✓ Attributes/timestamps preserved if requested
✓ Backup created if overwriting existing file
✓ All operations logged
✓ Rollback removes copied files and restores backups
✓ Disposal cleans up backup files when rollback not called
✓ Disposal restores original file when copy failed
✓ No orphaned backup files after installation completes

## Error Messages

- `"Source file not found: {path}"` - Source doesn't exist
- `"Destination file already exists: {path}. Set OverwriteIfExists to true to overwrite."` - File exists
- `"Cannot write to destination directory: {path}"` - Permission denied
- `"Insufficient disk space. Need {bytes}, available {bytes}"` - Not enough space
- `"File copy failed: {reason}"` - Generic copy failure
- `"Destination file is locked by another process"` - Cannot overwrite

## Platform-Specific Notes

### Windows
- Supports UNC paths and network shares
- Respects file attributes (hidden, system, read-only)
- May encounter file locking more frequently
- 260 character path limit (unless using \\?\ prefix)

### Linux/macOS
- Respects Unix permissions and ownership
- Supports symlinks in paths
- No file locking mechanism; concurrent writes possible
- Case-sensitive file systems

## Related Steps

- **CreateDirectoryStep** - Often run before CopyFileStep to ensure destination directory exists
- **SetFilePermissionsStep** - Often run after to adjust file permissions post-copy
- **MoveFileStep** - Alternative if source should be removed
