using System;
using System.IO;

namespace SHCDESE.IO;

public static class ZipUtil
{
    public static readonly byte[] ZipHeaderSignature = [0x50, 0x4B, 0x03, 0x04];
    public static readonly byte[] EocdHeaderSignature = [0x50, 0x4B, 0x05, 0x06];

    /// <summary>
    /// Searches a binary file for the End of Central Directory (EOCD) header (50 4B 05 06)
    /// starting from the end of the file.
    /// </summary>
    /// <remarks>
    /// This method is a reliable way to validate a ZIP file, as the EOCD record is
    /// required. It reads the file in chunks from the end to keep memory usage low.
    /// </remarks>
    /// <param name="filePath">The path to the binary file to search.</param>
    /// <returns>The zero-based index of the start of the EOCD header, or -1 if not found.</returns>
    public static long FindEocdHeaderIndex(string filePath)
    {
        const int bufferSize = 4096;
        byte[] buffer = new byte[bufferSize];

        using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fs.Length < EocdHeaderSignature.Length)
            return -1;

        // Position the stream at the end. The maximum possible comment length is 65535 bytes.
        // We dont need to scan the entire file, just the end portion.
        long readPosition = fs.Length;

        while (readPosition > 0)
        {
            // Determine the position to start reading from
            long startReadAt = Math.Max(0, readPosition - bufferSize);
            fs.Position = startReadAt;

            int bytesRead = fs.Read(buffer, 0, bufferSize);
            if (bytesRead == 0)
                break;

            // Search the buffer backwards for the signature
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer, 0, bytesRead);
            int indexInChunk = span.LastIndexOf(EocdHeaderSignature);

            if (indexInChunk != -1)
            {
                // Return the absolute position in the file
                return startReadAt + indexInChunk;
            }

            // Move the read position back, with a small overlap to catch signatures
            // that span across chunk boundaries.
            readPosition = startReadAt + (EocdHeaderSignature.Length - 1);

            // If we have reached the beginning of the file in the last chunk
            if (startReadAt == 0)
                break;
        }

        return -1;
    }


    /// <summary>
    /// Searches a binary file for the first occurrence of the ZIP header (50 4B 03 04)
    /// starting from the beginning of the file.
    /// </summary>
    /// <remarks>
    /// This method reads the file in chunks to keep memory usage low, making it suitable
    /// for very large files. It handles cases where the signature might span across two chunks.
    /// </remarks>
    /// <param name="filePath">The path to the binary file to search.</param>
    /// <returns>The zero-based index of the start of the header in the file, or -1 if not found.</returns>
    public static long FindFirstZipHeaderIndex(string filePath)
    {
        const int bufferSize = 8192;
        byte[] buffer = new byte[bufferSize];

        using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fs.Length < ZipHeaderSignature.Length)
            return -1;

        long currentReadPosition = 0;
        while (true)
        {
            fs.Position = currentReadPosition;

            int bytesRead = fs.Read(buffer, 0, bufferSize);
            if (bytesRead == 0)
                break;

            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer, 0, bytesRead);
            int indexInChunk = span.IndexOf(ZipHeaderSignature);

            if (indexInChunk != -1)
                return currentReadPosition + indexInChunk;

            if (bytesRead < bufferSize)
                break;

            // Prepare for the next read. We advance the position by the buffer size,
            // minus a small overlap. This ensures that if the signature was split
            // exactly between two chunks (e.g., "...PK" at the end of chunk 1 and
            // "03 04..." at the start of chunk 2), it will be found in the next read.
            currentReadPosition += (bufferSize - (ZipHeaderSignature.Length - 1));
        }


        return -1; // Return -1 if the header was not found after searching the entire file.
    }

    public static int ReadOnlyFile(string filePath, int offset, byte[] buffer)
    {
        using FileStream fs = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
        );
        fs.Position = offset;
        return fs.Read(buffer, 0, buffer.Length);
    }
}
