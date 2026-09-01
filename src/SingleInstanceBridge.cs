using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace DropOrb
{
    internal sealed class SingleInstanceBridge : IDisposable
    {
        private const string PipeName = "DropOrb.Desktop.Mvp.Pipe";
        private readonly Thread serverThread;
        private volatile bool stopping;

        public event Action<string[]> ArgumentsReceived;

        public SingleInstanceBridge()
        {
            serverThread = new Thread(Listen) { IsBackground = true, Name = "DropOrb single-instance bridge" };
        }

        public void Start()
        {
            serverThread.Start();
        }

        public static bool Forward(string[] arguments)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                    {
                        client.Connect(500);
                        using (var writer = new BinaryWriter(client, System.Text.Encoding.UTF8, true))
                        {
                            writer.Write(arguments.Length);
                            foreach (var argument in arguments) writer.Write(argument ?? string.Empty);
                            writer.Flush();
                        }
                    }
                    return true;
                }
                catch (TimeoutException) { Thread.Sleep(120); }
                catch (IOException) { Thread.Sleep(120); }
                catch (UnauthorizedAccessException) { Thread.Sleep(120); }
            }
            return false;
        }

        private void Listen()
        {
            while (!stopping)
            {
                try
                {
                    using (var server = CreateServer())
                    {
                        server.WaitForConnection();
                        using (var reader = new BinaryReader(server, System.Text.Encoding.UTF8, true))
                        {
                            var count = Math.Max(0, Math.Min(reader.ReadInt32(), 256));
                            var arguments = new string[count];
                            for (var index = 0; index < count; index++) arguments[index] = reader.ReadString();
                            var handler = ArgumentsReceived;
                            if (handler != null)
                            {
                                try { handler(arguments); }
                                catch (ObjectDisposedException) { }
                                catch (InvalidOperationException) { }
                            }
                        }
                    }
                }
                catch (IOException) { if (!stopping) Thread.Sleep(100); }
                catch (ObjectDisposedException) { }
            }
        }

        private static NamedPipeServerStream CreateServer()
        {
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,
                PipeAccessRights.FullControl, AccessControlType.Allow));
            return new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                PipeOptions.None, 0, 0, security);
        }

        public void Dispose()
        {
            stopping = true;
            try
            {
                using (var wake = new NamedPipeClientStream(".", PipeName, PipeDirection.Out)) wake.Connect(100);
            }
            catch { }
        }
    }
}
