using System.Net.NetworkInformation;
using System.Net.Sockets;


namespace IPCamPlayer.Helpers
{
    internal static class NetworkHelper
    {
        public static async Task<bool> IsDeviceOnlineAsync(string ip, int timeoutMs = 5000)
        {
            using var ping = new Ping();

            try
            {
                var reply = await ping.SendPingAsync(ip, timeoutMs);
                return reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
                return false;
            }
        }
        public static async Task<bool> IsDeviceTcpPortOpenAsync(string ip, int port = 80, int timeoutMs = 1000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            using var client = new TcpClient();

            try
            {
                var connectTask = client.ConnectAsync(ip, port);
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, cts.Token));
                return completedTask == connectTask && client.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
}
