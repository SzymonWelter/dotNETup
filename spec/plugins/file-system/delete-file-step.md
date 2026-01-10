# DeleteFileStep Specification

## Business Need

Installers need to clean up files as part of installation workflows. This includes removing temporary files, cleaning up old versions, and general file housekeeping.

**Use Cases:**
- Delete temporary installation files after use
- Clean up old configuration files during upgrade
- Remove obsolete DLLs and assemblies
- Delete leftover files from previous installations
- Remove temporary downloads after extraction
- Clean up installer media

## Parameters

The step accepts configuration through InstallationContext properties:

- `FilePath` (string, required) - Full path to file to delete
- `AllowIfNotExists` (bool, default: false) - Don't fail if file doesn't exist
- `CreateBackupBeforeDelete` (bool, default: false) - Create backup before deletion
- `BackupLocation` (string, default: null) - Directory to store backup (if created)
- `ConfirmDelete` (bool, default: false) - Log confirmation that delete is intentional
- `AllowDeleteSystemFiles` (bool, default: false) - Allow deletion of system files (if applicable)

## Validation Cases

### File Validation
- File must exist (unless `AllowIfNotExists=true`)
- File must be accessible
- File must not be locked by another process
- File must not be a directory (use DeleteDirectoryStep instead)
- File path must be absolute (or relative to InstallationPath)

### Permissions Validation
- Current user/process must have delete permission
- File must not be protected/read-only (or permission to delete read-only files)
- Parent directory must be accessible

### Safety Validation
- File is not a critical system file (unless explicitly allowed)
- Path does not escape installation directory (if installation-scoped)
- File is not currently in use by another process

### Backup Validation** (if creating backup)
- Backup location must exist or be creatable
- Backup location must be writable
- Sufficient disk space for backup

## Execution Logic

### Sequential Steps

1. **Pre-Delete Validation**
   - Check if file exists:
     - If not exists and `AllowIfNotExists=false`, fail
     - If not exists and `AllowIfNotExists=true`, skip operation
   - If exists, verify file is accessible
   - Verify file is not locked
   - Verify not a system-critical file (if protection enabled)

2. **Backup Operation** (if configured)
   - Create backup location if needed
   - Copy file to backup location
   - Verify backup created successfully
   - Store backup path in context

3. **Delete Operation**
   - Delete file
   - Verify deletion completed
   - Handle read-only files (remove read-only flag if needed, then delete)

4. **Verification**
   - Confirm file no longer exists
   - Confirm backup exists (if created)
   - Confirm no errors occurred

5. **Success State**
   - Store in context:
     - `DeletedFile` - path to deleted file
     - `BackupPath` - path to backup (if created)
     - `FileSize` - size of deleted file
     - `DeletedSuccessfully` - true if file deleted, false if skipped

6. **Logging**
   - Log file path and size
   - Log if backup was created
   - Log deletion confirmation
   - Report success/skip status

## Rollback Logic

### Rollback Conditions
- If deletion succeeded but subsequent steps failed
- If installation was cancelled after deletion

### Rollback Steps

1. **Restore from Backup** (if backup exists)
   - Copy backup file back to original location
   - Verify restoration completed
   - Delete backup file
   - Log restoration

2. **Handle Missing Backup**
   - If no backup exists and file was deleted:
     - Log warning that file cannot be restored
     - Continue rollback (best-effort approach)

3. **Handle Locked Locations**
   - If original location is locked, log warning
   - Try alternate restoration location
   - Continue rollback even if location locked

4. **Verification**
   - After rollback, verify file is restored
   - Log rollback completion

## Edge Cases

### File Not Existing
- **File doesn't exist, AllowIfNotExists=false** - Fail validation
- **File doesn't exist, AllowIfNotExists=true** - Skip operation, log warning
- **File deleted by another process between validation and deletion** - Handle gracefully

### Special Files
- **Read-only file** - Remove read-only attribute, then delete
- **System/hidden file** - Delete same as regular file
- **Very large file** - Delete still works; may take time
- **Symlink as source** - Delete the link, not the target

### Concurrency
- **File being read** - May succeed or fail per OS
- **File being written** - Will fail deletion
- **File locked by another process** - Fail with clear error

### Backup Scenarios
- **Backup location doesn't exist** - Create it
- **Backup location is read-only** - Fail with permission error
- **Insufficient disk space for backup** - Fail before deleting original
- **Backup file already exists** - Overwrite with version being deleted

## Success Criteria

✓ File deleted or skipped (if allowed)
✓ Backup created (if requested)
✓ All operations logged
✓ Rollback restores file from backup
✓ Safe deletion (validates before removing)

## Error Messages

- `"File not found: {path}"` - File doesn't exist and AllowIfNotExists=false
- `"Cannot delete file: {path}. File is locked by another process."` - File in use
- `"Cannot create backup directory: {path}"` - Backup location error
- `"Insufficient disk space for backup"` - Not enough space
- `"Permission denied: {path}"` - Cannot delete due to permissions
- `"File deletion failed: {reason}"` - Generic deletion failure

## Platform-Specific Notes

### Windows
- Read-only attribute must be removed before deletion
- Files in use may fail to delete immediately
- Temporary deletion APIs (MoveFileEx) could be used for pending deletions
- Handles UNC paths and network shares
- System file protection may prevent deletion of system files

### Linux/macOS
- Files in use can be deleted (unlinked)
- Permissions on parent directory control deletion
- No read-only attribute enforcement
- Case-sensitive file systems
- Symlinks are deleted (not their targets)

## Related Steps

- **DeleteDirectoryStep** - For directory deletion
- **CopyFileStep** - Often used internally for backup
- **CreateBackupStep** - If backup system exists

## Notes

- This is a destructive operation; consider whether backups are important
- Always validate file paths carefully to avoid accidental deletion
- Consider using installation context to scope deletions to installation directory
- Logging is critical for debugging deletion issues
