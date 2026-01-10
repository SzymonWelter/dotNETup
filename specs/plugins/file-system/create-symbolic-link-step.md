# CreateSymbolicLinkStep Specification

## Business Need

Installers need to create symbolic links to organize applications, enable shortcuts to different file locations, and support cross-platform link creation. Symbolic links are useful for managing multiple versions, creating aliases, and implementing soft dependencies.

**Use Cases:**
- Create shortcut to main executable
- Create link to configuration files in centralized location
- Create version-agnostic links to versioned libraries
- Create links for backward compatibility
- Link shared libraries from central repository
- Create convenience links for commonly used paths

## Parameters

The step accepts configuration through InstallationContext properties:

- `LinkPath` (string, required) - Full path where symlink will be created
- `TargetPath` (string, required) - Path that symlink points to (can be relative or absolute)
- `OverwriteIfExists` (bool, default: false) - Overwrite existing link/file
- `CreateParentDirectories` (bool, default: true) - Create parent directories if needed
- `UseRelativePath` (bool, default: false) - Create relative symlink (target is relative to link)
- `BackupExisting` (bool, default: false) - Back up existing link before overwriting
- `VerifyTarget` (bool, default: false) - Fail if target doesn't exist

## Validation Cases

### Path Validation
- LinkPath must be valid and absolute (or relative to InstallationPath)
- LinkPath must not already exist (unless `OverwriteIfExists=true`)
- LinkPath must not exceed OS path limits
- LinkPath parent directory must exist or be creatable

### Target Validation**
- TargetPath must be valid
- If `VerifyTarget=true`, target must exist
- If `UseRelativePath=false`, target can be absolute or relative
- If `UseRelativePath=true`, target path is relative to link directory

### Platform Validation**
- Windows: require appropriate OS version (Vista+ for native symlinks)
- Unix-like: require appropriate permissions
- Link creation must be supported on platform

### Permissions Validation**
- Current process must have privilege to create symlink
- Parent directory of link must be writable
- If target verification needed, target must be readable

### Environment Validation**
- Sufficient disk space
- Parent directory accessible
- No circular symbolic link references

## Execution Logic

### Sequential Steps

1. **Pre-Creation Validation**
   - Validate link path
   - Validate target path
   - Check if link already exists:
     - If exists and `OverwriteIfExists=false`, fail
     - If exists and `OverwriteIfExists=true`, backup (if configured)
   - If `VerifyTarget=true`, verify target exists/is accessible
   - Check privilege level (especially Windows)

2. **Parent Directory Creation** (if needed)
   - Create parent directories (if `CreateParentDirectories=true`)
   - Verify parent created successfully

3. **Backup Existing Link** (if applicable)
   - If link exists and `BackupExisting=true`:
     - Read link target (for information)
     - Rename existing link to backup
     - Store backup path

4. **Symbolic Link Creation**
   - Create symlink from LinkPath to TargetPath
   - If `UseRelativePath=true`, convert to relative path
   - Handle platform differences (Windows vs Unix)

5. **Verification**
   - Verify symlink created successfully
   - Verify symlink points to correct target
   - Verify symlink is readable

6. **Success State**
   - Store in context:
     - `LinkPath` - path of created symlink
     - `TargetPath` - what symlink points to
     - `IsRelative` - whether link is relative
     - `BackupPath` - backup location (if created)
     - `TargetExists` - whether target exists at time of creation

7. **Logging**
   - Log link and target paths
   - Log if relative or absolute
   - Log if backup was created
   - Log privilege level used
   - Report success

## Rollback Logic

### Rollback Conditions
- If symlink creation succeeded but subsequent steps failed
- If installation was cancelled

### Rollback Steps

1. **Remove Created Symlink**
   - Delete the symlink created
   - Verify removal (don't follow link; delete link itself)

2. **Restore Backup** (if backup exists)
   - Rename backup back to original link path
   - Verify restoration
   - Log restoration

3. **Restore Parent Directories** (if created)
   - Remove parent directories if empty
   - Walk back up only removing empty directories
   - Stop at first non-empty directory

4. **Handle Locked Links**
   - If link is locked or in use:
     - Log warning
     - Continue rollback
     - Link may remain but note in logs

5. **Verification**
   - After rollback, verify symlink removed
   - Log rollback completion

## Edge Cases

### Relative vs Absolute Paths
- **Absolute link to absolute target** - Works across any location
- **Relative link to relative target** - Works within directory structure
- **Absolute link to relative target** - Usually not recommended
- **Relative link to absolute target** - Possible but uncommon
- **Computing relative path** - Must handle correctly from link directory

### Target Existence
- **Target doesn't exist, VerifyTarget=false** - Link created (may be "broken" link)
- **Target doesn't exist, VerifyTarget=true** - Fail validation
- **Target exists at creation, deleted later** - Link becomes "broken"
- **Target path changes** - Links with relative paths adjust automatically

### Cross-Volume/Mount-Point Links
- **Windows: link on C: pointing to D:** - Supported with absolute paths
- **Unix: link on one mount point to another** - Fully supported
- **Relative paths across boundaries** - May not work as expected

### Circular References
- **Link A → B, Link B → A** - Valid but circular
- **Link A → A** - Valid but circular (self-reference)
- **Deep chain of links** - Valid but slow traversal
- **Must detect and warn or prevent** - Per configuration

### Special Cases
- **Link already exists** - Handle per configuration
- **Link points to another symlink** - Both valid; transitive
- **Target is a directory** - Symlink to directory is valid
- **Target is a file** - Symlink to file is valid

### Permissions
- **Creating link requires elevated privileges (Windows)** - May need admin
- **Target file is read-only** - Doesn't prevent link creation
- **Parent directory is read-only** - Prevents link creation

### Concurrency
- **Link created by another process** - Detect and skip/fail per config
- **Target deleted by another process** - May cause "broken" link
- **Parent directory deleted** - Fail gracefully

## Success Criteria

✓ Symlink created at LinkPath
✓ Symlink points to TargetPath correctly
✓ Link is readable and traversable
✓ Relative paths computed correctly (if used)
✓ Backup created (if configured)
✓ All operations logged
✓ Rollback removes symlink and restores backup

## Error Messages

- `"Invalid link path: {path}"` - Invalid characters or format
- `"Link path already exists: {path}. Set OverwriteIfExists to true."` - Link exists
- `"Target not found: {path}"` - Target doesn't exist (VerifyTarget=true)
- `"Cannot create parent directory: {path}"` - Parent creation failed
- `"Permission denied: Create symlink requires elevated privileges"` - On Windows with UAC
- `"Insufficient disk space"` - No space for link metadata
- `"Symlink creation not supported on this platform"` - Platform limitation
- `"Symlink creation failed: {reason}"` - Generic failure

## Platform-Specific Notes

### Windows
- Requires Windows Vista or later for native symlinks
- Requires either:
  - Administrator privileges, OR
  - Developer mode enabled (Windows 10+)
- Uses `CreateSymbolicLink` Windows API
- Supports both file and directory symlinks
- Absolute paths recommended over relative on Windows
- UNC paths supported for both link and target

### Linux/macOS
- Full support for symlinks (ancient feature)
- No privilege elevation required for creation
- Relative paths fully supported
- Symlinks across filesystems fully supported
- Case-sensitive file systems matter for relative paths
- Extended attributes on symlinks work

### Cross-Platform Considerations
- **Portable links:** Use relative paths where possible
- **Absolute links:** More reliable but less portable
- **Windows junction points:** Alternative to symlinks (directory junctions)
- **Symlink trails:** Relative paths may not work across platforms

## Related Steps

- **CreateDirectoryStep** - Often used before to create link parent directory
- **CreateJunctionStep** - Alternative for directory links on Windows
- **DeleteFileStep** - Used internally to remove existing links

## Performance Considerations

- **Creating symlinks is very fast** - Minimal disk I/O
- **Following symlinks has minimal overhead** - OS handles transparently
- **Relative path computation** - Quick for simple paths, slower for deep nesting

## Notes

- Symlinks are powerful but can be confusing; document well
- On Windows, document privilege requirements clearly
- Consider whether broken links are acceptable
- Relative links are more portable but harder to understand
- Absolute links are simpler but less portable
- Rollback must preserve link state, not follow it
