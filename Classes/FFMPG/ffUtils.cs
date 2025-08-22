using FFmpeg.AutoGen;
using System.Runtime.InteropServices;
using System.Text;


namespace IPCamPlayer.Classes.FFMPG
{
    public unsafe static class ffUtils
    {
        public static string PtrToStringUTF8(byte* ptr)
        {
            if (ptr == null) return string.Empty;
            return Encoding.UTF8.GetString(MemoryMarshal.AsBytes(new ReadOnlySpan<byte>(ptr, strlen(ptr))));
        }

        public static unsafe string av_errorToString(this int error)
        {
            var bufferSize = 1024;
            var buffer = stackalloc byte[bufferSize];
            if (ffmpeg.av_strerror(error, buffer, (ulong)bufferSize) == 0)
            {
                var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
                return message ?? string.Empty;
            }
            return string.Empty;
        }

        private static int strlen(byte* ptr)
        {
            int length = 0;
            while (ptr[length] != 0) length++;
            return length;
        }
    }
}
