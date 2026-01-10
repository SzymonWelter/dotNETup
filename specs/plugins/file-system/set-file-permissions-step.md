# SetFilePermissionsStep Specification

## Business Need

Installers need to set file and directory permissions to ensure proper access controls. This includes making executable files runnable, restricting access to configuration files, and setting proper ownership for security.

**Use Cases:**
- Make application executables runnable
- Restrict access to configuration files containing sensitive data
- Set proper permissions on data directories
- Ensure database files have correct ownership
- Make scripts executable
- Set read-only permissions on program files
- Configure ACLs on Windows for multi-user scenarios

## Parameters

The step accepts configuration through InstallationContext properties:

- `Path` (string, required) - Full path to file or directory
- `Recursive` (bool, default: false) - Apply to directory contents recursively
- `Permissions` (string, required) - Permission specification (platform-dependent)
- `Owner` (string, default: null) - Set owner/user (Unix-like systems)
- `Group` (string, default: null) - Set group ownership (Unix-like systems)
- `BackupExistingPermissions` (bool, default: true) - Back up original permissions
- `PreserveSpecialBits` (bool, default: false) - Preserve setuid/setgid/sticky bits
- `FailIfTargetMissing` (bool, default: false) - Fail if target doesn't exist

## Validation Cases

### Path Validation
- Target path must exist (unless `FailIfTargetMissing=false`)
- Target must be accessible
- Path must be absolute (or relative to InstallationPath)

### Permissions Validation
- Permission string must be valid for the platform
- Permission string must be parseable
- Permissions must be assignable by current user
- Cannot set contradictory permissions

### User/Group Validation** (Unix-like systems)
- Specified owner must exist (if provided)
- Specified group must exist (if provided)
- Current user must have privilege to change ownership (usually requires root)

### Recursive Validation** (if recursive)
- All files/directories must be accessible
- All must be writable
- No circular symbolic links

### Environment Validation**
- Current process must have sufficient privilege
- No permission restrictions preventing change
- File/directory not locked by critical process

## Execution Logic

### Sequential Steps

1. **Pre-Permission Validation**
   - Verify target path exists (or allow missing per config)
   - Verify target is accessible
   - Parse and validate permission string
   - If Unix-like: validate owner/group exist
   - Check current process privilege level

2. **Backup Original Permissions** (if configured)
   - Read original permissions from target
   - For recursive: enumerate and record all permissions
   - Store backup in context for rollback

3. **Permission Application** (Single File/Directory)
   - Apply permission string to target
   - Set owner/group (if provided)
   - Preserve special bits (if configured)
   - Verify changes applied

4. **Recursive Permission Application** (if `Recursive=true`)
   - Enumerate all files and subdirectories
   - Apply permissions bottom-up (files first, then directories)
   - Handle symlinks appropriately (change link, not target)
   - Report progress for large trees
   - Continue on errors (best-effort per file)

5. **Success State**
   - Store in context:
     - `TargetPath` - path permissions were set on
     - `ItemsModified` - count of items changed (1 for single, N for recursive)
     - `BackupPermissions` - original permissions (if backed up)
     - `PermissionsSet` - the permissions that were applied
     - `RecursiveApplied` - whether recursive was used

6. **Logging**
   - Log target path
   - Log original and new permissions
   - Log owner/group changes
   - Log items modified (if recursive)
   - Report success

## Rollback Logic

### Rollback Conditions
- If permission change succeeded but subsequent steps failed
- If installation was cancelled

### Rollback Steps

1. **Restore Backed-Up Permissions**
   - Use stored backup to restore original permissions
   - For recursive: restore all backed-up permissions to original state
   - Restore owner/group to original values
   - Verify restorations completed

2. **Handle Restoration Errors**
   - If restoration fails on some items:
     - Log warning for each failed item
     - Continue restoration for others
   - Best-effort approach

3. **Handle Locked Files**
   - If file is locked:
     - Log warning
     - Skip restoration for that item
     - Continue with others

4. **Verification**
   - After rollback, verify original permissions restored
   - Log rollback completion and any issues

## Edge Cases

### Permission Formats
- **Windows ACL format** - Complex, requires ACL parser
- **Unix rwx format** - Simple but limited
- **Octal format (755)** - Standard Unix notation
- **Symbolic format (u+x)** - Relative changes

### Recursive Scenarios
- **Mix of files and directories** - Apply appropriate permissions
- **Symlinks in tree** - Change link, not target (unless configured)
- **Very large trees** - Report progress frequently
- **Mixed permissions in tree** - Each item tracked separately

### Special Bits
- **setuid bit on executables** - Preserve or clear per policy
- **setgid bit on directories** - Preserve for permission inheritance
- **Sticky bit on shared directories** - Preserve for protection

### Ownership Changes
- **Non-root trying to change owner** - Usually fails on Unix
- **Changing to non-existent user** - Fail with clear error
- **Group change succeeds but owner fails** - Partial change; log result

### Access Control
- **Read-only target** - May still allow permission changes
- **Target in use by process** - May prevent changes on some systems
- **Filesystem doesn't support permission** - Fail or adapt per OS

### Concurrency
- **Permissions changed by another process** - May detect and reapply if needed
- **Target deleted during operation** - Fail gracefully
- **Target moved during operation** - May affect operation

## Success Criteria

✓ Permissions set correctly on target
✓ Owner/group set (if applicable)
✓ All items modified (if recursive)
✓ Original permissions backed up (if requested)
✓ Special bits preserved (if requested)
✓ All operations logged
✓ Rollback restores original permissions

## Error Messages

- `"Target not found: {path}"` - Target doesn't exist
- `"Invalid permission format: {permissions}"` - Malformed permission string
- `"User does not exist: {owner}"` - Owner user not found
- `"Group does not exist: {group}"` - Group not found
- `"Permission denied: {path}"` - Cannot change permissions (privilege)
- `"Cannot resolve permissions on symlink: {path}"` - Symlink issue
- `"Permission change failed: {reason}"` - Generic failure

## Platform-Specific Notes

### Windows
- Uses Windows ACLs (Access Control Lists)
- More complex than Unix permissions
- Format: `DOMAIN\User:PermissionLevel` (e.g., `EVERYONE:Read`)
- No concept of "owner" in traditional sense; uses ACLs
- Requires elevated privileges for some operations
- Supports inheritance and special permissions

### Linux/macOS
- Uses traditional Unix permissions (rwxrwxrwx)
- Octal format (755, 644, etc.) most common
- Separate owner and group concepts
- Requires root/sudo for ownership changes
- File creation subject to umask
- Extended attributes (xattr) available for additional metadata

## Supported Permission Formats

### Unix/Linux/macOS
- **Octal:** `755`, `644`, `700`, `777`
- **Symbolic:** `u+x`, `g-w`, `o=r`, `a+r`
- **Full symbolic:** `u=rwx,g=rx,o=rx`

### Windows
- **ACL format:** `EVERYONE:Full`, `ADMINISTRATORS:Modify`, `USERS:Read`
- **Inheritance:** Can configure inheritance flags

## Related Steps

- **CreateDirectoryStep** - Often used to create, then SetFilePermissionsStep to configure
- **CopyFileStep** - May need permission adjustment post-copy
- **CopyDirectoryStep** - May need permissions adjusted on tree

## Performance Considerations

- **Large directory trees** - Recursive can be slow; report progress
- **Network paths** - Slower than local; provide feedback
- **Symlink handling** - Recursive enumeration may encounter loops; protect against

## Notes

- Always validate permission strings before applying
- Document permission requirements clearly for users
- Consider whether permissions are installation-specific or persistent
- Some permission changes may require elevated privileges
- Rollback must preserve original permission state for safety
