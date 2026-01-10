# ExtractArchiveStep Specification

## Business Need

Installers frequently need to extract archive files to distribute application files, libraries, and resources. Supporting multiple archive formats (ZIP, TAR, 7Z) provides flexibility in how installation packages are distributed and compressed.

**Use Cases:**
- Extract application binaries from ZIP archive
- Extract multi-language resource packs
- Extract configuration templates
- Extract documentation and help files
- Extract plugin bundles
- Extract compressed library collections
- Stage files for further installation steps

## Parameters

The step accepts configuration through InstallationContext properties:

- `ArchivePath` (string, required) - Full path to archive file to extract
- `DestinationDirectory` (string, required) - Directory to extract contents to
- `ArchiveFormat` (string, required) - Format of archive (ZIP, TAR, TAR.GZ, TAR.BZ2, 7Z)
- `CreateDestinationIfNeeded` (bool, default: true) - Create destination directory if missing
- `OverwriteExistingFiles` (bool, default: false) - Overwrite existing files in destination
- `PreserveFileAttributes` (bool, default: true) - Preserve file permissions/attributes
- `PreserveTimestamps` (bool, default: true) - Preserve original file timestamps
- `BackupExisting` (bool, default: false) - Back up existing files before overwriting
- `PreserveFolderStructure` (bool, default: true) - Recreate folder structure from archive
- `SingleFolder` (string, default: null) - If archive has single root folder, extract contents only
- `IncludeFilter` (string[], default: null) - Only extract files matching patterns
- `ExcludeFilter` (string[], default: null) - Skip files matching patterns
- `SkipCorruptedFiles` (bool, default: false) - Continue if some files corrupt
- `VerifyArchiveIntegrity` (bool, default: false) - Test archive before extracting

## Validation Cases

### Archive Validation
- Archive file must exist
- Archive file must be readable
- Archive path must be absolute (or relative to InstallationPath)
- Archive format must be specified and supported
- Archive format must match file extension (or allow override)
- If `VerifyArchiveIntegrity=true`, archive must pass integrity check

### Destination Validation**
- Destination directory must be writable or creatable
- Destination path must be valid and absolute
- Path length must not exceed OS limits for extracted files
- Sufficient disk space for entire extracted content
- No circular references (archive not inside destination)

### Filter Validation**
- Include and exclude filters must be valid patterns
- Patterns must not be contradictory
- Filter syntax must be valid glob patterns

### File Conflict Validation**
- If `OverwriteExistingFiles=false`:
  - Check for file conflicts in destination
  - Fail if conflicts found
- If `OverwriteExistingFiles=true`:
  - Verify destination files are writable

### Environment Validation**
- Sufficient disk space for full extraction
- Destination parent directory accessible
- Archive not corrupted (if verification enabled)
- Temp space available for extraction

## Execution Logic

### Sequential Steps

1. **Pre-Extraction Validation**
   - Verify archive file exists and is readable
   - Validate archive format against file
   - If `VerifyArchiveIntegrity=true`, test archive integrity
   - Create destination directory (if needed and allowed)
   - Check total uncompressed size against disk space
   - Enumerate archive contents and apply filters
   - Check for file conflicts

2. **Archive Format Detection**
   - Detect format from extension and/or file header
   - Select appropriate extraction library
   - Verify format support on current platform

3. **Pre-Extraction Analysis**
   - List all files in archive
   - Apply include/exclude filters
   - Compute total uncompressed size
   - Check for suspicious entries (path traversal attempts)
   - Detect single-root-folder pattern (if SingleFolder configured)

4. **Backup Existing Files** (if configured)
   - Identify files that will be overwritten
   - Create backup copies in backup location
   - Store backup manifest for rollback
   - Verify all backups created

5. **Extraction Operation**
   - For each file in archive (in order):
     - Extract to temporary location (optional)
     - Verify extraction completed
     - Check extracted file integrity (if possible)
     - Move/copy to final destination
     - Set attributes/permissions (if configured)
     - Set timestamps (if configured)
     - Report progress (file count, bytes extracted)

6. **Special Handling**
   - Handle `SingleFolder` extraction (extract contents, skip root folder)
   - Handle nested archives (extract only, don't recursively extract)
   - Handle symlinks in archive (preserve or skip per platform)
   - Handle large files (stream extraction, not memory-based)

7. **Post-Extraction Verification**
   - Verify all files extracted to destination
   - Verify file counts match expected
   - Verify total size reasonable
   - Check for extraction errors or warnings

8. **Success State**
   - Store in context:
     - `DestinationDirectory` - where extracted
     - `FilesExtracted` - count of files extracted
     - `DirectoriesCreated` - count of directories created
     - `TotalBytesExtracted` - total size of extracted content
     - `BackupLocations` - map of backups (if created)
     - `ExtractionDuration` - time taken
     - `ArchiveFormat` - detected format
     - `FilteredOutCount` - files filtered out by include/exclude

9. **Logging**
   - Log archive path and format
   - Log destination and structure info
   - Log file count and total size
   - Log filtered files (if any)
   - Log backups created
   - Report progress during extraction
   - Log any warnings or issues

## Rollback Logic

### Rollback Conditions
- If extraction succeeded but subsequent steps failed
- If installation was cancelled

### Rollback Steps

1. **Restore Backed-Up Files** (if created)
   - For each backed-up file, restore from backup location
   - Delete backup files
   - Verify restoration completed
   - Log restorations

2. **Remove Extracted Files**
   - Delete all files extracted
   - Delete empty subdirectories created
   - Preserve non-empty directories that existed before
   - Work bottom-up to handle deletions correctly

3. **Handle Locked Files**
   - If any file is locked:
     - Log warning
     - Retry with delay
     - Continue rollback (best-effort)

4. **Verification**
   - After rollback, destination should be in original state
   - Log rollback completion

## Edge Cases

### Archive Contents Issues
- **Archive contains root folder** - Handle per SingleFolder configuration
- **Archive has no root folder** - Extract directly to destination
- **Archive with absolute paths** - Convert to relative (security)
- **Archive with path traversal attempts** - Reject extraction
- **Archive with duplicate filenames** - Handle per OS convention

### Compression Formats
- **ZIP** - Standard, widely supported
- **TAR** - Unix-standard, supports symlinks and permissions
- **TAR.GZ** - TAR with gzip compression
- **TAR.BZ2** - TAR with bzip2 compression
- **7Z** - High compression, less common
- **Other formats** - May require additional libraries

### Special Files in Archive
- **Symlinks** - Preserve (Unix) or skip (Windows)
- **Directory symlinks** - Handle carefully
- **Hard links** - Not preserved in most archive formats
- **Special files (devices, pipes)** - Skip on systems that don't support
- **Very long filenames** - Validate against OS limits
- **Files with unusual permissions** - Handle gracefully

### File Conflicts
- **File exists, OverwriteExistingFiles=false** - Skip or fail per policy
- **File exists, OverwriteExistingFiles=true, BackupExisting=false** - Overwrite
- **File exists, OverwriteExistingFiles=true, BackupExisting=true** - Backup then overwrite
- **Directory vs file conflict** - Fail with clear error

### Large Archives
- **Large archives (>1GB)** - Stream extraction, not memory-based
- **Many files (100,000+)** - Report progress frequently
- **Large individual files** - Monitor for progress
- **Archive with compression** - Handle correctly

### Filter Scenarios
- **Include filter only** - Extract matching files only
- **Exclude filter only** - Extract all except matching files
- **Both filters** - Apply both (must match include AND not match exclude)
- **Empty result** - If filters result in no files, still create directory

### Corruption Handling
- **Corrupted archive header** - Fail with clear error
- **Corrupted file entry** - If `SkipCorruptedFiles=true`, skip; otherwise fail
- **Partial extraction** - Log what succeeded; rollback removes partial

### Encoding Issues
- **Non-ASCII filenames** - Handle Unicode correctly
- **Different locale encodings** - Detect and handle appropriately
- **Invalid character sequences** - Replace with safe characters

## Success Criteria

✓ All non-filtered files extracted to destination
✓ Directory structure preserved (or handled per config)
✓ File count and byte count accurate
✓ Attributes/timestamps preserved (if requested)
✓ Existing files handled per configuration
✓ Filters applied correctly
✓ Archive format correctly identified
✓ All operations logged
✓ Rollback removes extracted files and restores backups
✓ Handles all supported archive formats

## Error Messages

- `"Archive file not found: {path}"` - Archive doesn't exist
- `"Archive is corrupted"` - Integrity check failed
- `"Unsupported archive format: {format}"` - Format not supported
- `"Archive format mismatch: Expected {format}, detected {actual}"` - Format inconsistent
- `"Cannot create destination directory: {path}"` - Permission denied
- `"Insufficient disk space. Need {bytes}, available {bytes}"` - Not enough space
- `"Path traversal attempt detected in archive: {path}"` - Security issue
- `"File already exists: {path}. Set OverwriteExistingFiles to allow overwrite."` - Conflict
- `"Corrupted file in archive: {file}"` - File extraction failed
- `"Invalid filter pattern: {pattern}"` - Malformed glob pattern
- `"Extraction failed: {reason}"` - Generic extraction failure

## Platform-Specific Notes

### Windows
- Supports ZIP natively (through .NET)
- TAR/7Z may require additional libraries
- Long path support (\\?\) for paths exceeding 260 chars
- File attributes preserved on NTFS
- Symlinks in archives handled carefully (may not create real symlinks)

### Linux/macOS
- TAR is native format; full support including symlinks
- ZIP fully supported
- 7Z requires 7z utility or library
- Full Unix permission preservation
- Case-sensitive file systems matter
- No practical path length limits

## Archive Format Support Matrix

| Format | Windows | Linux | macOS | Notes |
|--------|---------|-------|-------|-------|
| ZIP | Native | Native | Native | Universal format |
| TAR | Library | Native | Native | Unix standard |
| TAR.GZ | Library | Native | Native | Compressed TAR |
| TAR.BZ2 | Library | Native | Native | Better compression |
| 7Z | Library | Library | Library | High compression |

## Related Steps

- **CreateDirectoryStep** - Often run before to ensure destination exists
- **SetFilePermissionsStep** - Often run after to adjust extracted file permissions
- **DeleteDirectoryStep** - Used to clean up extraction directory during rollback
- **CopyFileStep** - Alternative if archive extraction not needed

## Performance Considerations

- **Streaming extraction** - Extract to temp, then move (safer, larger disk requirement)
- **Direct extraction** - Extract directly to destination (faster, riskier if fails)
- **Compression ratio** - 7Z best, ZIP middle, TAR uncompressed
- **Large files** - May need special handling for progress reporting

## Notes

- Archive integrity verification adds time but improves reliability
- Symlink handling differs significantly across platforms
- Consider backup before extraction for important operations
- Path traversal attempts in archives must be detected and rejected
- Non-ASCII filenames may cause issues; consider validation
- Consider whether archive format consistency matters (ZIP vs 7Z)
