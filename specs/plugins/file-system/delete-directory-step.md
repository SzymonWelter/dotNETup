# DeleteDirectoryStep Specification

## Business Need

Installers need to clean up entire directory trees as part of installation workflows. This includes removing temporary extraction directories, old application directories during upgrades, and general directory housekeeping.

**Use Cases:**
- Delete temporary extraction directory after setup
- Clean up old application version during upgrade
- Remove entire plugin directory
- Delete leftover directories from failed previous installations
- Clean up temporary work directories
- Remove installation staging areas

## Parameters

The step accepts configuration through InstallationContext properties:

- `DirectoryPath` (string, required) - Full path to directory to delete
- `Recursive` (bool, default: true) - Delete contents recursively
- `AllowIfNotExists` (bool, default: false) - Don't fail if directory doesn't exist
- `CreateBackupBeforeDelete` (bool, default: false) - Back up entire directory before deletion
- `BackupLocation` (string, default: null) - Directory to store backup
- `PreserveSpecificFiles` (string[], default: null) - Don't delete files matching patterns
- `DeleteFilesFirst` (bool, default: true) - Delete files before removing directory
- `AllowDeleteNonEmpty` (bool, default: true) - Allow deletion of non-empty directories
- `ConfirmDelete` (bool, default: false) - Log confirmation that delete is intentional

## Validation Cases

### Directory Validation
- Directory must exist (unless `AllowIfNotExists=true`)
- Directory must be accessible
- Directory must not be locked or in active use
- Directory path must be absolute (or relative to InstallationPath)
- Directory must not be a critical system directory (if protection enabled)

### Permissions Validation
- Current user/process must have delete permission
- Parent directory must be accessible and writable
- All files/subdirectories must be deletable

### Safety Validation
- Not a critical system directory (e.g., Windows, System32)
- Not the installation root unless explicitly confirmed
- Not the active working directory of any process
- Path does not escape expected scope

### Backup Validation** (if creating backup)
- Backup location must exist or be creatable
- Backup location must be writable
- Sufficient disk space for entire directory tree

### Preservation Validation** (if preserving specific files)
- Preserve patterns must be valid
- Cannot preserve everything (pattern matching rules)

## Execution Logic

### Sequential Steps

1. **Pre-Delete Validation**
   - Check if directory exists:
     - If not exists and `AllowIfNotExists=false`, fail
     - If not exists and `AllowIfNotExists=true`, skip operation
   - If exists, enumerate directory contents
   - Check total size of directory tree
   - Verify all contents are deletable

2. **Backup Operation** (if configured)
   - Create backup location if needed
   - Copy entire directory tree to backup location
   - Verify backup created successfully
   - Store backup path in context

3. **Delete Operation** (if `Recursive=true`)
   - Enumerate all files and subdirectories
   - Apply preservation filters if specified
   - Delete files in order (newest/largest first if possible)
   - Delete empty subdirectories bottom-up
   - Handle read-only files (remove flag, then delete)
   - Report progress for large trees

4. **Directory Removal**
   - After all contents deleted, delete the directory itself
   - Verify directory no longer exists

5. **Success State**
   - Store in context:
     - `DeletedDirectory` - path to deleted directory
     - `BackupPath` - path to backup (if created)
     - `FilesDeleted` - count of files deleted
     - `DirectoriesDeleted` - count of directories deleted
     - `TotalBytesDeleted` - total size freed
     - `DeletedSuccessfully` - true if directory deleted, false if skipped

6. **Logging**
   - Log directory path and total size
   - Log file count and directory count
   - Log if backup was created
   - Log deletion confirmation
   - Report progress for large operations
   - Report success/skip status

## Rollback Logic

### Rollback Conditions
- If deletion succeeded but subsequent steps failed
- If installation was cancelled after deletion

### Rollback Steps

1. **Restore from Backup** (if backup exists)
   - Copy entire backup directory tree back to original location
   - Verify restoration completed
   - Restore original permissions if necessary
   - Delete backup directory
   - Log restoration

2. **Handle Missing Backup**
   - If no backup exists and directory was deleted:
     - Log warning that directory cannot be restored
     - Continue rollback (best-effort approach)
     - Directory is irrecoverable

3. **Handle Partial Restoration**
   - If restoration fails partway:
     - Log all files that couldn't be restored
     - Leave partial directory in place for manual recovery
     - Continue rollback (best-effort)

4. **Handle Locked Files**
   - If restoration blocked by locked files:
     - Log warning
     - Retry with delay (if possible)
     - Continue rollback

5. **Verification**
   - After rollback, verify directory structure restored
   - Log rollback completion and any issues

## Edge Cases

### Non-Empty Directories
- **Directory has files, AllowDeleteNonEmpty=true** - Delete recursively
- **Directory has files, AllowDeleteNonEmpty=false** - Fail validation
- **Directory has locked files** - Fail deletion

### Special Files
- **Read-only files** - Remove read-only attribute, then delete
- **System/hidden files** - Include in deletion
- **Symlinks** - Delete the link, not the target
- **Hard links** - Delete the file (hard link count decremented)

### Large Directory Trees
- **Many files (100,000+)** - Report progress frequently
- **Large total size (>10GB)** - Stream enumeration and deletion
- **Deep nesting (50+ levels)** - Handle stack limits gracefully

### Preservation Scenarios
- **Preserve specific files** - Match patterns, skip those files
- **All files preserved** - Directory structure remains but empty
- **Partial preservation** - Delete non-preserved files

### Backup Scenarios
- **Backup location doesn't exist** - Create it
- **Backup location is read-only** - Fail before deleting original
- **Insufficient disk space for backup** - Fail before deleting
- **Backup already exists** - Overwrite with new backup

### Concurrency
- **Directory being accessed** - Deletion may fail per OS
- **Files being written** - Deletion will likely fail
- **Files being read** - May succeed or fail per OS behavior

## Success Criteria

✓ Directory deleted or skipped (if allowed)
✓ All files and subdirectories removed
✓ Backup created (if requested)
✓ Preservation filters applied correctly
✓ All operations logged
✓ Rollback restores directory from backup
✓ Safe deletion (validates before removing)
✓ Handles both small and large directory trees

## Error Messages

- `"Directory not found: {path}"` - Directory doesn't exist and AllowIfNotExists=false
- `"Directory not empty: {path}. Set AllowDeleteNonEmpty to true to delete."` - Has contents
- `"Cannot delete directory: {path}. Directory is in use."` - Locked/in-use
- `"Cannot create backup directory: {path}"` - Backup location error
- `"Insufficient disk space for backup"` - Not enough space
- `"Permission denied: {path}"` - Cannot delete due to permissions
- `"Directory deletion failed: {reason}"` - Generic deletion failure
- `"Critical directory protection prevents deletion: {path}"` - System directory

## Platform-Specific Notes

### Windows
- Read-only attribute must be removed before deletion
- Directories with open handles may fail to delete
- Pending deletion APIs (MoveFileEx) could be used
- Handles UNC paths and network shares
- System directory protection may prevent deletion

### Linux/macOS
- Directories with open files can be deleted (unlinked)
- Permissions on parent directory control deletion
- Case-sensitive file systems
- More reliable cross-filesystem deletion
- Symlinks deleted (not their targets)

## Related Steps

- **DeleteFileStep** - For individual file deletion
- **CopyDirectoryStep** - Often used internally for backup
- **CreateDirectoryStep** - Often precedes directory operations

## Notes

- This is a destructive operation; consider whether backups are important
- Always validate directory paths carefully to avoid accidental deletion
- Strongly recommend using CreateBackupBeforeDelete=true for critical directories
- Consider using installation context to scope deletions to installation directory
- Logging is critical for debugging deletion issues
- Some directories may remain locked after process termination; system restart may be needed
