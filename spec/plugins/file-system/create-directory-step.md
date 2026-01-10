# CreateDirectoryStep Specification

## Business Need

Installers need to create directory structures as foundational operations before placing files into those directories. This includes creating the main application directory, configuration directories, data directories, and other organizational structures.

**Use Cases:**
- Create main application installation directory
- Create subdirectories for application structure (bin, config, data, etc.)
- Create directory hierarchy for multi-tier applications
- Create dedicated directories for plugins or extensions
- Create data directories for runtime files
- Create temporary working directories during installation

## Parameters

The step accepts configuration through InstallationContext properties:

- `DirectoryPath` (string, required) - Full path to directory to create
- `CreateParentDirectories` (bool, default: true) - Create all parent directories if needed
- `AllowIfAlreadyExists` (bool, default: false) - Don't fail if directory already exists
- `SetPermissions` (bool, default: false) - Set specific permissions after creation
- `Permissions` (string, default: null) - Permission string (e.g., "755" on Unix, "EVERYONE:Full" on Windows)
- `Owner` (string, default: null) - Set owner/user (Unix-like systems)
- `Group` (string, default: null) - Set group ownership (Unix-like systems)
- `BackupExistingPermissions` (bool, default: false) - Backup existing permissions before changing

## Validation Cases

### Path Validation
- Directory path must be valid and absolute (or relative to InstallationPath)
- Path must not exceed OS limits (260 chars on Windows, varies on Unix)
- Path characters must be valid for the OS
- Path must not reference invalid locations (e.g., raw device paths)

### Parent Directory Validation**
- Parent directories must exist or be creatable (if `CreateParentDirectories=true`)
- Parent directory must be writable
- Parent directory path must be accessible

### Existence Validation**
- If directory exists:
  - If `AllowIfAlreadyExists=false`, validation fails
  - If `AllowIfAlreadyExists=true`, validation passes
- If directory doesn't exist, it can be created

### Permissions Validation** (if setting permissions)
- Permission string must be valid for the platform
- Permission string must be parseable
- Requested permissions must be assignable
- If setting owner/group, those must exist on system

### Space Validation**
- Minimal space needed (just for directory metadata)
- Parent volume must be accessible

## Execution Logic

### Sequential Steps

1. **Pre-Create Validation**
   - Validate directory path
   - Check if directory already exists:
     - If exists and `AllowIfAlreadyExists=false`, fail
     - If exists and `AllowIfAlreadyExists=true`, skip creation
   - Validate parent directory is accessible
   - Check disk space availability

2. **Parent Directory Creation** (if needed)
   - Create parent directories recursively (if `CreateParentDirectories=true`)
   - Verify each parent created successfully
   - Handle errors (stop if parent creation fails)

3. **Directory Creation**
   - Create the target directory
   - Verify directory created successfully
   - Set default permissions (OS defaults or specified)

4. **Permission Assignment** (if configured)
   - Set specific permissions (if provided)
   - Set owner/group (if provided and supported)
   - Backup original permissions (if configured)
   - Verify permission changes applied

5. **Success State**
   - Store in context:
     - `CreatedDirectory` - actual path created
     - `ParentDirectoriesCreated` - count of parent directories created
     - `DirectoryAlreadyExisted` - true if directory existed before
     - `PermissionsSet` - true if permissions were changed
     - `OriginalPermissions` - backup of previous permissions (if changed)

6. **Logging**
   - Log directory path
   - Log if created fresh or already existed
   - Log parent directories created (if any)
   - Log permissions set
   - Report success

## Rollback Logic

### Rollback Conditions
- If permission setting failed after creation
- If subsequent steps failed after directory creation
- If installation was cancelled

### Rollback Steps

1. **Restore Permissions** (if permissions were changed)
   - Restore original permissions from backup
   - Restore original owner/group
   - Verify restoration completed
   - Log restoration

2. **Remove Created Directory** (if newly created)
   - If directory was empty when created (or becomes empty after other rollbacks):
     - Delete directory
   - If directory has contents:
     - Log warning
     - Leave directory in place (other step rollbacks may need it)

3. **Remove Parent Directories** (if created and now empty)
   - Walk back up created parent directory chain
   - Remove only empty directories
   - Stop at first non-empty or pre-existing directory

4. **Handle Locked Directories**
   - If directory is locked or in use:
     - Log warning
     - Continue rollback
     - Leave directory in place

5. **Verification**
   - After rollback, verify permissions restored (if changed)
   - Log rollback completion

## Edge Cases

### Existing Directories
- **Directory exists, AllowIfAlreadyExists=false** - Fail validation
- **Directory exists, AllowIfAlreadyExists=true** - Skip creation
- **Directory partially created from parent creation** - Skip that level

### Deep Nesting
- **Very deep paths (50+ levels)** - Handle stack limits
- **Path exceeds OS limits** - Fail with clear error
- **Permission changes needed at multiple levels** - Apply consistently

### Special Cases
- **Creating in root directory** - Requires elevated permissions
- **Creating on different volume/mount point** - Handle correctly
- **Creating in read-only parent** - Fail with permission error
- **Parent directory is actually a file** - Fail with clear error

### Permissions
- **Setting contradictory permissions** - Fail or handle per policy
- **Owner/group don't exist** - Fail with clear error
- **Insufficient privilege to set permissions** - Log warning, continue
- **Platform doesn't support requested permissions** - Adapt to platform

### Concurrency
- **Directory created by another process** - Detect and skip creation
- **Parent directory deleted during operation** - Fail with clear error
- **Permissions changed by another process** - Detect and reapply if needed

## Success Criteria

✓ Directory created at specified path
✓ All parent directories created (if needed)
✓ Directory permissions set correctly (if requested)
✓ Directory accessible for subsequent operations
✓ All operations logged
✓ Rollback removes created directory and restores permissions
✓ Handles both simple and complex paths

## Error Messages

- `"Invalid directory path: {path}"` - Invalid characters or format
- `"Directory path too long: {path}"` - Exceeds OS limits
- `"Directory already exists: {path}. Set AllowIfAlreadyExists to true to allow."` - Exists
- `"Cannot create parent directory: {path}"` - Parent creation failed
- `"Parent is a file, not a directory: {path}"` - Path conflict
- `"Permission denied: {path}"` - Cannot create due to permissions
- `"Invalid permission string: {permissions}"` - Bad permission format
- `"User/group does not exist: {owner}/{group}"` - Permission target not found
- `"Insufficient disk space"` - No space available
- `"Directory creation failed: {reason}"` - Generic creation failure

## Platform-Specific Notes

### Windows
- Handles UNC paths and network shares
- Supports long paths (\\?\ prefix)
- Permissions use Windows ACLs (more complex)
- No separate owner concept; uses user and groups
- 260 character limit without long path support

### Linux/macOS
- Respects Unix permissions (rwxrwxrwx)
- Separate owner and group concepts
- More straightforward permission model
- Case-sensitive file systems
- No practical path length limit
- Default umask applies to new directories

## Related Steps

- **CopyFileStep** - Often precedes this to place files in created directory
- **CopyDirectoryStep** - May precede this or follow
- **SetFilePermissionsStep** - For more advanced permission management
- **DeleteDirectoryStep** - Cleanup during rollback

## Decision Points

**When to use CreateDirectoryStep vs creating directory in other steps:**
- Use dedicated CreateDirectoryStep for explicit directory structure creation
- Use automatic directory creation in Copy/Move steps for convenience
- Use CreateDirectoryStep when permissions must be set
- Use CreateDirectoryStep when directory structure itself is significant
