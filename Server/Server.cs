using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace Server
{
    class Server
    {
        #region Запросы клиентов
        const int Heartbeat = 0;
        const int Disconnect = 1;
        const int Request = 2;
        const int SendMessage = 3;
        #endregion

        Task heartbeats;

        CancellationTokenSource cancellationToken;

        TcpListener server;
        Status status;
        Action<string> logger;
        Dictionary<User, Socket> users;

        public Server(Action<string> logger)
        {
            status = Status.Waiting;
            server = new TcpListener(IPAddress.Any, Program.port);
            this.logger = logger;
            users = new Dictionary<User, Socket>();
        }

        public async Task Start()
        {
            status = Status.Working;
            logger("Запуск сервера");
            server.Start(15);
            cancellationToken = new CancellationTokenSource();
            heartbeats = new Task(TimeoutKick);
            heartbeats.Start();
            while (true)
            {
                try
                {
                    if (status == Status.Working)
                    {
                        Socket s = await server.AcceptSocketAsync();
                        Task t = new Task(() => Handler(s));
                        t.Start();
                    }
                    else
                        break;
                }
                catch(Exception e)
                {
                    if(!(e is ObjectDisposedException))
                        logger($"Исключение: {e}");
                }
            }
        }

        public async Task Stop()
        {
            status = Status.Stoping;
            while (server.Pending()) ;
            server.Stop();
            logger("Остановка сервера");
        }

        private void TimeoutKick()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(30000);
                if (cancellationToken.IsCancellationRequested)
                    break;
                foreach (User u in users.Keys)
                {
                    if ((DateTime.Now - u.lastTime).TotalSeconds > 30)
                    {
                        users.Remove(u);
                        logger($"Превышение времени ожидания для {u.username}");
                    }
                }
            }
        }

        private void Handler(Socket socket)
        {
            while (status == Status.Working)
            {
                var message = GetMessage(socket);
                if (message == null)
                    continue;
                switch (message.messageType)
                {
                    case Message.MessageType.Heartbeat:
                        HandleHeartbeat(socket, message.content);
                        break;
                    case Message.MessageType.Disconect:
                        if (HandleDisconect(socket, message.content))
                            return;
                        break;
                    case Message.MessageType.Request:
                        HandleRequest(socket);
                        break;
                    case Message.MessageType.Send:
                        HandleSend(socket, message.content);
                        break;
                }
            }
        }

        private Message GetMessage(Socket socket)
        {
            byte[] buffer = new byte[1024];
            int count;
            if((count = socket.Receive(buffer)) > 0)
            {
                switch(buffer[0])
                {
                    case Heartbeat:
                        return new Message(Message.MessageType.Heartbeat, Encoding.UTF8.GetString(buffer, 1, count - 1));
                    case Disconnect:
                        return new Message(Message.MessageType.Disconect, Encoding.UTF8.GetString(buffer, 1, count - 1));
                    case Request:
                        return new Message(Message.MessageType.Request, "");
                    case SendMessage:
                        return new Message(Message.MessageType.Send, Encoding.UTF8.GetString(buffer, 1, count - 1));
                    default:
                        socket.Send(buffer, 0, count, SocketFlags.None);
                        Thread.Sleep(50);
                        break;
                }
            }
            return null;
        }

        private void HandleHeartbeat(Socket socket, string content)
        {
            User current = null;
            foreach (var u in users.Keys)
            {
                if (u.username == content)
                {
                    current = u;
                    break;
                }
            }

            if (current == null)
            {
                logger($"Подключение {content}");
                users.Add(new User(content, DateTime.Now), socket);
                socket.Send(new byte[1] { 255 });
            }
            else if (users[current] == socket)
            {
                logger($"Heartbeat от {content}");
                current.lastTime = DateTime.Now;
                socket.Send(new byte[1] { 255 });
            }
            else
            {
                logger($"Совпадение {content}");
                socket.Send(new byte[1] { 250 });
            }
        }

        private bool HandleDisconect(Socket socket, string content)
        {
            User current = null;
            foreach (var u in users.Keys)
            {
                if (u.username == content)
                {
                    current = u;
                    break;
                }
            }
            if (current != null && socket == users[current])
            {
                logger($"Отключение {content}");
                users.Remove(current);
                socket.Send(new byte[1] { 254 });
                socket.Disconnect(true);
                return true;
            }
            else
            {
                logger($"Ошибка отключения {content}");
                socket.Send(new byte[1] { 250 });
                return false;
            }
        }

        private void HandleRequest(Socket socket)
        {
            logger($"Запрос списка");
            StringBuilder list = new StringBuilder();
            foreach (var key in this.users.Keys)
                list.Append(key.username + '\n');
            byte[] users = Encoding.UTF8.GetBytes(list.ToString());
            byte[] buffer = new byte[users.Length + 1];
            buffer[0] = 253;
            for (int i = 0; i < users.Length; i++)
                buffer[i + 1] = users[i];
            socket.Send(buffer);
        }

        private void HandleSend(Socket socket, string content)
        {
            string[] strs = content.Split(new char[] { '\n' }, 3, StringSplitOptions.RemoveEmptyEntries);
            User from = null;
            User to = null;
            foreach(var u in users.Keys)
            {
                if (u.username == strs[0])
                    from = u;
                if (u.username == strs[1])
                    to = u;
            }

            if(from != null && to != null && users[from] == socket)
            {
                logger($"Сообщение {from.username} -> {to.username}");
                byte[] fromBuff = Encoding.UTF8.GetBytes(strs[0]);
                byte[] messBuff = Encoding.UTF8.GetBytes(strs[2]);
                byte[] buffer = new byte[fromBuff.Length + messBuff.Length + 2];
                buffer[0] = 251;
                for (int i = 0; i < fromBuff.Length; i++)
                    buffer[i + 1] = fromBuff[i];
                buffer[fromBuff.Length + 1] = (byte)'\n';
                for (int i = 0; i < messBuff.Length; i++)
                    buffer[fromBuff.Length + i + 2] = messBuff[i];
                users[to].Send(buffer);
                socket.Send(new byte[1] { 252 });
            }
            else
            {
                logger($"Ощибка сообщения {from.username} -> {to.username}");
                if(from != null)
                    socket.Send(new byte[1] { 250 });
                else
                    socket.Send(new byte[1] { 249 });

            }
        }

        public string[] GetUsers()
        {
            string[] strs = new string[users.Count];
            int i = 0;
            foreach (User u in users.Keys)
                strs[i++] = u.username;
            return strs;
        }

        private class Message
        {
            public MessageType messageType;

            public string content;

            public Message(MessageType messageType, string content)
            {
                this.messageType = messageType;
                this.content = content;
            }

            public enum MessageType
            {
                Heartbeat,
                Disconect,
                Request,
                Send,
            }
        }

        private class User
        {
            public string username;

            public DateTime lastTime;

            public User(string username, DateTime lastTime)
            {
                this.username = username;
                this.lastTime = lastTime;
            }
        }
    }

    enum Status
    {
        Waiting,
        Working,
        Stoping
    }
}
