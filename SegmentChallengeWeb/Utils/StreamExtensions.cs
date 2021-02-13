using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SegmentChallengeWeb.Utils {
    public static class StreamExtensions {
        public static async Task<Byte[]> ReadAllBytesAsync(
            this Stream source,
            Int32 bufferSize = 16384,
            CancellationToken cancellationToken = default) {

            var allBytes = new Byte[0];
            var buffer = new Byte[bufferSize];
            Int32 bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, bufferSize, cancellationToken)) > 0) {
                var newAllBytes = new Byte[allBytes.Length + bytesRead];
                Array.Copy(allBytes, newAllBytes, allBytes.Length);
                Array.Copy(buffer, 0, newAllBytes, allBytes.Length, bytesRead);
                allBytes = newAllBytes;
            }

            return allBytes;
        }
    }
}
