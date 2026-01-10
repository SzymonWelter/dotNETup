# CreateJunctionStep Specification

## Business Need

Windows installers need to create directory junctions as an alternative to symbolic links for directory references. Junctions have been available since Windows 2000 and don't require elevated privileges, making them more practical than symlinks for many installation scenarios.

**Use Cases:**
- Create junctions to alternate data directory locations
- Link to shared network directories
- Create version-agnostic directory links
- Implement backward compatibility links
- Organize directory structure without physical moves
- Redirect data directories to different volumes

## Scope

This step is **Windows-only**. For cross-platform symbolic link creation, use CreateSymbolicLinkStep instead. Junction points are a Windows-specific feature for directory linking that doesn't require admin privileges.

## Parameters

The step accepts configuration through InstallationContext properties:

- `JunctionPath` (string, required) - Full path where junction will be created
- `TargetDirectory` (string, required) - Path to target directory (absolute path required)
- `OverwriteIfExists` (bool, default: false) - Overwrite existing junction/directory
- `CreateParentDirectories` (bool, default: true) - Create parent directories if needed
- `BackupExisting` (bool, default: false) - Back up existing junction before overwriting
- `VerifyTarget` (bool, default: false) - Fail if target directory doesn't exist
- `AllowRelativeTarget` (bool, default: false) - Allow relative paths for target (not recommended)

## Validation Cases

### Path Validation
- JunctionPath must be valid Windows path
- JunctionPath must not already exist (unless `OverwriteIfExists=true`)
- JunctionPath must not exceed 260 character limit (without long path prefix)
- JunctionPath parent directory must exist or be creatable
- JunctionPath and TargetDirectory must be different

### Target Directory Validation**
- TargetDirectory must be a valid Windows path
- TargetDirectory must be an absolute path (or relative conversion allowed if configured)
- TargetDirectory must not contain junctions that point back to junction location
- If `VerifyTarget=true`, target directory must exist
- TargetDirectory must be on same or different NTFS volume

### Parent Directory Validation**
- Parent of JunctionPath must be accessible and writable
- Parent directory must be on NTFS volume (junctions require NTFS)
- Parent must not be read-only

### Permissions Validation**
- Current user must have create permission in parent directory
- No elevated privileges required (unlike symbolic links)
- Target directory must be readable (if VerifyTarget=true)

### Environment Validation**
- Must be running on Windows (junction-specific feature)
- Sufficient disk space for junction metadata
- No circular junction references

## Execution Logic

### Sequential Steps

1. **Pre-Creation Validation**
   - Validate junction path
   - Validate target directory path
   - Verify running on Windows
   - Check if junction already exists:
     - If exists and `OverwriteIfExists=false`, fail
     - If exists and `OverwriteIfExists=true`, backup (if configured)
   - If `VerifyTarget=true`, verify target directory exists
   - Verify no circular references

2. **Parent Directory Creation** (if needed)
   - Create parent directories (if `CreateParentDirectories=true`)
   - Verify parent directories created
   - Verify parent is on NTFS volume

3. **Backup Existing Junction** (if applicable)
   - If junction exists and `BackupExisting=true`:
     - Read junction target path (for information)
     - Rename existing junction to backup
     - Store backup path and original target

4. **Junction Creation**
   - Create junction point using Windows APIs
   - Set reparse point with correct target directory
   - Verify junction created successfully

5. **Verification**
   - Verify junction exists and is accessible
   - Verify junction target is correct
   - Verify junction can be traversed
   - Compare reported target with expected

6. **Success State**
   - Store in context:
     - `JunctionPath` - path of created junction
     - `TargetDirectory` - what junction points to
     - `BackupPath` - backup location (if created)
     - `TargetExists` - whether target directory exists at creation

7. **Logging**
   - Log junction and target paths
   - Log if backup was created
   - Log verification results
   - Report success

## Rollback Logic

### Rollback Conditions
- If junction creation succeeded but subsequent steps failed
- If installation was cancelled

### Rollback Steps

1. **Remove Created Junction**
   - Delete the junction point (not the target directory!)
   - Verify junction removal
   - Confirm target directory still exists and is intact

2. **Restore Backup** (if backup exists)
   - If backup junction exists:
     - Rename backup back to original junction path
     - Verify restoration
     - Verify target still accessible

3. **Restore Parent Directories** (if created)
   - Remove parent directories if empty
   - Walk back up only removing empty directories
   - Stop at first non-empty directory

4. **Handle Locked Junctions**
   - If junction is in use:
     - Log warning
     - Retry with delay (if possible)
     - Continue rollback

5. **Verification**
   - After rollback, verify junction removed
   - Verify target directory untouched
   - Log rollback completion

## Edge Cases

### Path Issues
- **Very long paths** - Windows has 260 character limit (260 without prefix)
- **UNC paths** - Supported as targets
- **Mapped drives** - Supported and resolved correctly
- **Paths with spaces** - Handled correctly

### Target Directory States
- **Target doesn't exist, VerifyTarget=false** - Junction created to non-existent directory
- **Target doesn't exist, VerifyTarget=true** - Fail validation
- **Target exists at creation, deleted later** - Junction points to missing target
- **Target is moved** - Junction may no longer be valid

### Circular References
- **Junction A → B, Junction B → A** - Circular reference; should detect
- **Junction A → A** - Self-reference; should detect
- **Multiple junctions in chain** - Valid but traversal gets complex
- **Must detect and fail** - Prevent infinite loops

### Overwrite Scenarios
- **Junction exists, OverwriteIfExists=false** - Fail validation
- **Junction exists, OverwriteIfExists=true, BackupExisting=false** - Overwrite
- **Junction exists, OverwriteIfExists=true, BackupExisting=true** - Backup and overwrite
- **File exists at location** - Fail (cannot create junction on file)

### Special Cases
- **Existing junction points to different target** - Can be overwritten per config
- **Junction to junction** - Windows may not support; should validate
- **Junction to symlink target** - Valid on Windows
- **Hard link conflict** - Junctions and hard links are different; no conflict

### Volumes and Mounts
- **Junction across NTFS volumes** - Fully supported
- **Junction to different drive** - Fully supported
- **Junction to UNC path** - Supported
- **Non-NTFS volumes** - Junctions require NTFS; fail clearly

### Permissions
- **Read-only parent directory** - Prevents junction creation
- **Target directory permissions** - Don't affect junction creation
- **No privilege elevation required** - Unlike symbolic links
- **Low-privilege user can create** - Generally true for NTFS volumes

### Concurrency
- **Junction created by another process** - Detect and skip/fail per config
- **Target directory deleted** - Junction becomes invalid
- **Parent directory deleted** - Fail gracefully
- **Junction traversed during creation** - May cause issues; avoid

## Success Criteria

✓ Junction created at JunctionPath
✓ Junction points to correct TargetDirectory
✓ Junction is traversable and usable
✓ Backup created (if configured)
✓ All operations logged
✓ Rollback removes junction and restores backup
✓ No privilege escalation required

## Error Messages

- `"This step requires Windows"` - Not running on Windows
- `"Invalid junction path: {path}"` - Invalid characters or format
- `"Path too long: {path}"` - Exceeds 260 character limit
- `"Junction already exists: {path}. Set OverwriteIfExists to true."` - Exists
- `"Target directory not found: {path}"` - Target missing (VerifyTarget=true)
- `"Cannot create parent directory: {path}"` - Parent creation failed
- `"Circular junction reference detected"` - Would create loop
- `"Parent directory is not on NTFS volume"` - Junctions require NTFS
- `"File exists at junction path: {path}"` - Path occupied by file
- `"Junction creation failed: {reason}"` - Generic failure

## Platform-Specific Notes

### Windows
- Requires Windows 2000 or later
- Requires NTFS file system
- No privilege elevation needed (unlike symlinks)
- Junctions are tracked by reparse points
- Removing junction doesn't affect target
- Traversal of junction is transparent to most applications
- Requires appropriate APIs (CreateFile, DeviceIoControl)

### Not Supported
- Linux/macOS - Junctions are Windows-only feature
- FAT32 or other non-NTFS volumes
- Very old Windows versions (pre-2000)

## Related Steps

- **CreateSymbolicLinkStep** - Cross-platform alternative (requires admin)
- **CreateDirectoryStep** - Often used before to create junction parent
- **DeleteDirectoryStep** - Used internally to remove junctions during rollback

## Decision Points

**When to use CreateJunctionStep vs CreateSymbolicLinkStep:**
- Use CreateJunctionStep on Windows when no admin privileges available
- Use CreateSymbolicLinkStep for cross-platform (Windows/Unix) support
- Use CreateJunctionStep for better Windows compatibility
- Use CreateSymbolicLinkStep when symlink semantics specifically needed
- CreateJunctionStep is safer on Windows (no privilege requirements)

## Performance Considerations

- **Creating junctions is very fast** - Only metadata operation
- **Traversing junctions is transparent** - OS handles automatically
- **Junction resolving has minimal overhead** - Built into file system

## Notes

- Junctions work better than symbolic links for directory links on Windows
- No special privileges needed (big advantage over symlinks)
- Removing junction doesn't affect target directory
- Circular junction references should be detected and prevented
- Junctions are transparent to most Windows applications
- Consider using junctions for better Windows installer experience
