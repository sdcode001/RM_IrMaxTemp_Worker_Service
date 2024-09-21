using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace eye.analytics.irmaxtemp.Helpers
{
    public class Redis : IRedis
    {
        Socket socket;
        BufferedStream bstream;

        public string Host { get; private set; }
        public int Port { get; private set; }
        public int RetryTimeout { get; set; }
        public int RetryCount { get; set; }
        public int SendTimeout { get; set; }
        public string Password { get; set; }
        // public string ListName { get; set; }
        // public string GatewayName { get; set; }

        public Redis(string host, int port)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Port = port;
            // ListName = listname;
            // GatewayName = gatewayname;
            SendTimeout = -1;
        }

        int db;
        public int Db
        {
            get
            {
                return db;
            }

            set
            {
                db = value;
                SendExpectSuccess("SELECT", db);
            }
        }

        public Redis(string host) : this(host, 6379)
        {
        }

        public Redis() : this("localhost", 6379)
        {
        }

        public class ResponseException : Exception
        {
            public ResponseException(string code) : base("Response error")
            {
                Code = code;
            }

            public string Code { get; private set; }
        }

        void Connect()
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                    SendTimeout = SendTimeout
                };
                socket.Connect(Host, Port);
                if (!socket.Connected)
                {
                    socket.Close();
                    socket = null;
                    return;
                }
                bstream = new BufferedStream(new NetworkStream(socket), 16 * 1024);

                //if (Password != null)
                //    SendExpectSuccess("AUTH", Password);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception Occured");
            }
        }

        public void Set(string key, string value)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            if (value == null)
                throw new ArgumentNullException("value");

            Set(key, Encoding.UTF8.GetBytes(value));
        }

        public void Set(string key, byte[] value)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            if (value == null)
                throw new ArgumentNullException("value");

            if (value.Length > 1073741824)
                throw new ArgumentException("value exceeds 1G", "value");

            if (!SendDataCommand(value, "SET", key))
                throw new Exception("Unable to connect");
            ExpectSuccess();
        }


        public string[] LeftPop(string key, int limit)
        {
            return SendExpectStringArray("LPOP", key, limit);
            //return ReadData();
        }

        public byte[] LeftPop(string key)
        {
            SendCommand("LPOP", key);
            return ReadData();
        }

        public void RightPush(string key, string value)
        {
            RightPush(key, Encoding.UTF8.GetBytes(value));
        }

        public void RightPush(string key, byte[] value)
        {
            SendDataCommand(value, "RPUSH", key);
            ExpectSuccess();
        }

        bool SendCommand(string cmd, params object[] args)
        {
            if (socket == null || !socket.Connected)
                Connect();
            if (socket == null)
                return false;
            if (socket.Connected)
            {
                string resp = $"*{1 + args.Length}\r\n${cmd.Length}\r\n{cmd}\r\n";
                foreach (object arg in args)
                {
                    string argStr = arg.ToString();
                    int argStrLength = Encoding.UTF8.GetByteCount(argStr);
                    resp += $"${argStrLength}\r\n{argStr}\r\n";
                }

                byte[] r = Encoding.UTF8.GetBytes(resp);
                try
                {
                    Log("C", resp);
                    socket.Send(r);
                }
                catch (SocketException)
                {
                    // timeout;
                    socket.Close();
                    socket = null;

                    return false;
                }
                return true;
            }
            return false;
        }

        public string[] SendExpectStringArray(string cmd, params object[] args)
        {
            byte[][] reply = SendExpectDataArray(cmd, args);
            if (reply == null)
                return null;
            string[] keys = new string[reply.Length];
            for (int i = 0; i < reply.Length; i++)
            {
                var replyItem = reply[i];
                if (replyItem != null)
                    keys[i] = Encoding.UTF8.GetString(replyItem);
                else
                    keys[i] = null;
            }
            return keys;
        }
        public byte[][] SendExpectDataArray(string cmd, params object[] args)
        {
            if (!SendCommand(cmd, args))
            {
                return null;
                //throw new Exception("Unable to connect");
            }
            int c = bstream.ReadByte();
            if (c == -1)
            {
                //throw new ResponseException("No more data");
                return null;
            }
            string s = ReadLine();
            Log("S", (char)c + s);
            if (c == '-')
                throw new ResponseException(s.StartsWith("ERR ") ? s[4..] : s);
            if (c == '*')
            {
                if (int.TryParse(s, out int count))
                {
                    byte[][] result = new byte[count][];

                    for (int i = 0; i < count; i++)
                        result[i] = ReadData();

                    return result;
                }
            }
            return null;
        }

        byte[] end_data = new byte[] { (byte)'\r', (byte)'\n' };

        bool SendDataCommand(byte[] data, string cmd, params object[] args)
        {
            string resp = "*" + (1 + args.Length + 1).ToString() + "\r\n";
            resp += "$" + cmd.Length + "\r\n" + cmd + "\r\n";
            foreach (object arg in args)
            {
                string argStr = arg.ToString();
                int argStrLength = Encoding.UTF8.GetByteCount(argStr);
                resp += "$" + argStrLength + "\r\n" + argStr + "\r\n";
            }
            resp += "$" + data.Length + "\r\n";

            return SendDataRESP(data, resp);
        }

        bool SendDataRESP(byte[] data, string resp)
        {
            if (socket == null)
                Connect();
            if (socket == null)
                return false;

            byte[] r = Encoding.UTF8.GetBytes(resp);
            try
            {
                Log("C", resp);
                socket.Send(r);
                if (data != null)
                {
                    socket.Send(data);
                    socket.Send(end_data);
                }
            }
            catch (SocketException)
            {
                // timeout;
                socket.Close();
                socket = null;

                return false;
            }
            return true;
        }

        string ReadLine()
        {
            StringBuilder sb = new StringBuilder();
            int c;

            while ((c = bstream.ReadByte()) != -1)
            {
                if (c == '\r')
                    continue;
                if (c == '\n')
                    break;
                sb.Append((char)c);
            }
            return sb.ToString();
        }

        byte[] ReadData()
        {
            string s = ReadLine();
            Log("S", s);
            if (s.Length == 0)
                throw new ResponseException("Zero length respose");

            char c = s[0];
            if (c == '-')
                throw new ResponseException(s.StartsWith("-ERR ") ? s[5..] : s[1..]);

            if (c == '$')
            {
                if (s == "$-1")
                    return null;

                if (int.TryParse(s[1..], out int n))
                {
                    byte[] retbuf = new byte[n];

                    int bytesRead = 0;
                    do
                    {
                        int read = bstream.Read(retbuf, bytesRead, n - bytesRead);
                        if (read < 1)
                            throw new ResponseException("Invalid termination mid stream");
                        bytesRead += read;
                    }
                    while (bytesRead < n);
                    if (bstream.ReadByte() != '\r' || bstream.ReadByte() != '\n')
                        throw new ResponseException("Invalid termination");
                    return retbuf;
                }
                throw new ResponseException("Invalid length");
            }

            /* don't treat arrays here because only one element works -- use DataArray!
            //returns the number of matches
            if (c == '*') {
                int n;
                if (Int32.TryParse(s.Substring(1), out n)) 
                    return n <= 0 ? new byte [0] : ReadData();

                throw new ResponseException ("Unexpected length parameter" + r);
            }
            */

            throw new ResponseException("Unexpected reply: " + s);
        }

        [Conditional("DEBUG")]
        static void Log(string id, string message)
        {
            //Console.WriteLine(id + ": " + message.Trim().Replace("\r\n", " "));
        }

        void SendExpectSuccess(string cmd, params object[] args)
        {
            if (!SendCommand(cmd, args))
                throw new Exception("Unable to connect");

            ExpectSuccess();
        }

        void ExpectSuccess()
        {
            try
            {
                if (bstream != null)
                {
                    int c = bstream.ReadByte();
                    if (c == -1)
                        throw new ResponseException("No more data");

                    string s = ReadLine();
                    Log("S", (char)c + s);
                    if (c == '-')
                        throw new ResponseException(s.StartsWith("ERR ") ? s[4..] : s);
                }
            }
            catch
            {

            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Redis()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                SendCommand("QUIT");
                ExpectSuccess();
                socket?.Close();
                socket = null;
            }
        }
    }
}
