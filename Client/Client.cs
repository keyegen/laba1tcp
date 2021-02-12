using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    public class Client
    {
        #region Ответы сервера
        const int SuccessHB = 255;//HB = Heartbeat
        const int SuccessDC = 254;//DC = Disconnect
        const int SuccessRQ = 253;//RQ = Request
        const int SuccessSM = 252;//SM = Send Message
        const int SuccessRM = 251;//RM = Receive Message
        const int ErrorCUNF = 250;//CUNF = Current User Not Found
        const int ErrorFUNF = 249;//FUNF = "From User" Not Found
        #endregion

        Task receiver;

        Task heartbeats;

        CancellationTokenSource cancellationToken;

        byte? answer = null;

        string[] users = null;

        public string username;

        Socket socket;

        public event MessageReceiveHandler MessageReceived;

        public event UsersRefreshHandler UsersRefresh;

        public Client(string username)
        {
            this.username = username;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public bool Connect()
        {
            bool connected = false;
            Task.Run(() => socket.Connect(Program.ipAdress, Program.port))
                .ContinueWith((Task t) =>
                {
                    cancellationToken = new CancellationTokenSource();
                    receiver = new Task(Receiver);
                    receiver.Start();
                    if (SendHeartbeat() == 0)
                    {
                        connected = true;
                        Thread.Sleep(500);
                        heartbeats = new Task(Heartbeats);
                        heartbeats.Start();
                    }
                    else
                        cancellationToken.Cancel();
                }).Wait();
            return connected;
        }

        private void Receiver()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] buffer = new byte[1024];
                int count;
                byte? answer = null;
                if ((count = socket.Receive(buffer)) > 0)
                {
                    switch(buffer[0])
                    {
                        case SuccessHB:
                        case SuccessDC:
                        case SuccessSM:
                            answer = 0;
                            break;
                        case SuccessRQ:
                            answer = 0;
                            users = Encoding.UTF8.GetString(buffer, 1, count - 1).Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            break;
                        case SuccessRM:
                            answer = 0;
                            string text = Encoding.UTF8.GetString(buffer, 1, count - 1);
                            string[] data = text.Split(new char[] { '\n' }, 2, StringSplitOptions.RemoveEmptyEntries);
                            MessageReceived(data[0], data[1]);
                            break;
                        case ErrorCUNF:
                        case ErrorFUNF:
                            answer = 1;
                            break;
                        default:
                            socket.Send(buffer, 0, count, SocketFlags.None);
                            Thread.Sleep(50);
                            break;
                    }
                    while (this.answer != null) ;
                    this.answer = answer;
                    Thread.Sleep(100);
                }
            }
        }

        private void Heartbeats()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SendHeartbeat();
                UsersRefresh?.Invoke(SendRequest());
                Thread.Sleep(10000);
            }
        }

        public int SendHeartbeat()
        {
            byte[] username = Encoding.UTF8.GetBytes(this.username);
            byte[] buffer = new byte[username.Length + 1];
            buffer[0] = 0;
            for (int i = 0; i < username.Length; i++)
                buffer[i + 1] = username[i];
            socket.Send(buffer);
            while (answer == null) ;
            buffer[0] = (byte)answer;
            answer = null;
            return buffer[0];
        }

        public int SendDisconnect()
        {
            byte[] username = Encoding.UTF8.GetBytes(this.username);
            byte[] buffer = new byte[username.Length + 1];
            buffer[0] = 1;
            for (int i = 0; i < username.Length; i++)
                buffer[i + 1] = username[i];
            socket.Send(buffer);
            while (answer == null) ;
            buffer[0] = (byte)answer;
            answer = null;
            socket.Disconnect(true); 
            cancellationToken.Cancel();
            return buffer[0];
        }

        public string[] SendRequest()
        {
            byte[] buffer = new byte[1024];
            socket.Send(new byte[] { 2 });
            while (answer == null) ;
            answer = null;
            return users;
        }

        public int SendMessage(string to, string message)
        {
            byte[] from = Encoding.UTF8.GetBytes(this.username);
            byte[] username = Encoding.UTF8.GetBytes(to);
            byte[] mess = Encoding.UTF8.GetBytes(message);
            byte[] buffer = new byte[from.Length + username.Length + mess.Length + 3];
            buffer[0] = 3;
            for (int i = 0; i < from.Length; i++)
                buffer[i + 1] = from[i];
            buffer[from.Length + 1] = (byte)'\n';
            for (int i = 0; i < username.Length; i++)
                buffer[i + from.Length + 2] = username[i];
            buffer[from.Length + username.Length + 2] = (byte)'\n';
            for (int i = 0; i < mess.Length; i++)
                buffer[i + from.Length + username.Length + 3] = mess[i];
            socket.Send(buffer);
            while (answer == null) ;
            buffer[0] = (byte)answer;
            answer = null;
            return buffer[0];
        }
    }

    public delegate void MessageReceiveHandler(string from, string message);

    public delegate void UsersRefreshHandler(string[] users);
}
