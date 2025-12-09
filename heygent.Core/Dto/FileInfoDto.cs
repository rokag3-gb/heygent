namespace heygent.Core.Dto;

public record FileInfoDto(
    string Name, // "20250717105000_NXT_IF_BusinessDay"
    string FullPath, // "C:\\heygentTestLedgerRoot\\20250717105000_NXT_IF_BusinessDay"
    string Extension,
    long? Length,
    bool IsReadOnly,
    UnixFileMode UnixFileMode,
    string DirectoryName, // "C:\\heygentTestLedgerRoot"
    DirectoryInfoDto DirectoryInfoDto,
    DateTime CreationTime,
    DateTime CreationTimeUtc,
    DateTime LastAccessTime,
    DateTime LastAccessTimeUtc,
    DateTime LastWriteTime,
    DateTime LastWriteTimeUtc
);

public record DirectoryInfoDto(
    string Name, // "heygentTestLedgerRoot"
    string FullPath, // "C:\\heygentTestLedgerRoot"
    UnixFileMode UnixFileMode,
    DateTime CreationTime,
    DateTime CreationTimeUtc,
    DateTime LastAccessTime,
    DateTime LastAccessTimeUtc,
    DateTime LastWriteTime,
    DateTime LastWriteTimeUtc
);