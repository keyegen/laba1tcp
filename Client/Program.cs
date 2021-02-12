using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Client
{
    class Program
    {
        public static int port = 15465;

        public static string ipAdress = "127.0.0.1";

        private static Client client;

        private static bool typingMessage = false;

        private static int indexMessage = 0;

        private static StringBuilder currentMessage;

        private static string[] users;

        private static bool[] newMessage = new bool[0];

        private static int selectedUser = 0;

        private static bool busy = false;

        private static Dictionary<string, List<string>> chats = new Dictionary<string, List<string>>();

        static void Main(string[] args)
        {
            Console.ResetColor();
            Console.WindowWidth = 85;
            Console.WindowHeight = 31;
            Console.BufferWidth = 85;
            Console.BufferHeight = 31;

            Console.Write("Введите имя пользователя: ");
            Console.CursorTop = 2;
            Console.CursorLeft = 0;
            Console.WriteLine("ArrowUp/ArrowDown - выбор собеседника");
            Console.WriteLine("Enter - открыть чат/отправить сообщение");
            Console.WriteLine("Escape - вернуться к выбору собеседника");
            Console.WriteLine("Ctrl+C/Ctrl+Break - выйти из чата и закрыть программу");

            Console.CursorTop = 0;
            Console.CursorLeft = 26;
            string username;
            bool b;
            do
            {
                b = true;
                username = Console.ReadLine();
                if (username.Length < 10)
                {
                    int i = 0;
                    for (; i < username.Length; i++)
                        if (!char.IsLetterOrDigit(username[i]))
                            break;

                    if (i == username.Length)
                        b = false;
                }

                client = new Client(username);
                b = !client.Connect();

                if (b)
                {
                    Console.Write("Введите допустимое имя пользователя");
                    Console.CursorTop = 0;
                    Console.CursorLeft = 26;
                    Console.Write(new string(' ', username.Length));
                    Console.CursorLeft = 26;
                }
            } while (b);

            Console.Title = "В системе как " + username;
            Console.Clear();
            Console.CursorVisible = false;

            Console.CancelKeyPress += (s, e) =>
            {
                client.SendDisconnect();
                Environment.Exit(0);
            };
            BuildFrame();
            Console.CursorTop = 1;
            Console.CursorLeft = 1;
            Client_UsersRefresh(client.SendRequest());
            client.UsersRefresh += Client_UsersRefresh;
            client.MessageReceived += Client_MessageReceived;
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                while (busy) ;
                busy = true;
                switch(key.Key)
                {
                    case ConsoleKey.UpArrow:
                        if (!typingMessage)
                        {
                            Console.BackgroundColor = ConsoleColor.Black;
                            Console.ForegroundColor = newMessage[selectedUser] ? ConsoleColor.Cyan : ConsoleColor.White;
                            Console.Write(users[selectedUser].PadRight(15));

                            if (--selectedUser < 0)
                                selectedUser = users.Length - 1;

                            Console.CursorTop = 1 + selectedUser;
                            Console.CursorLeft = 1;
                            Console.BackgroundColor = ConsoleColor.White;
                            Console.ForegroundColor = ConsoleColor.Black;
                            Console.Write(users[selectedUser].PadRight(15));
                            Console.CursorLeft = 1;
                        }
                        break;
                    case ConsoleKey.DownArrow:
                        if (!typingMessage)
                        {
                            Console.BackgroundColor = ConsoleColor.Black;
                            Console.ForegroundColor = newMessage[selectedUser] ? ConsoleColor.Cyan : ConsoleColor.White;
                            Console.Write(users[selectedUser].PadRight(15));

                            if (++selectedUser >= users.Length)
                                selectedUser = 0;

                            Console.CursorTop = 1 + selectedUser;
                            Console.CursorLeft = 1;
                            Console.BackgroundColor = ConsoleColor.White;
                            Console.ForegroundColor = ConsoleColor.Black;
                            Console.Write(users[selectedUser].PadRight(15));
                            Console.CursorLeft = 1;
                        }
                        break;
                    case ConsoleKey.LeftArrow:
                        if (!typingMessage)
                            break;

                        if (--indexMessage < 0)
                        {
                            indexMessage = 0;
                            Console.CursorLeft = 17;
                            Console.CursorTop = 24;
                        }
                        else
                            Console.CursorLeft--;
                        break;
                    case ConsoleKey.RightArrow:
                        if (!typingMessage)
                            break;

                        if (++indexMessage > currentMessage.Length)
                        {
                            indexMessage = currentMessage.Length;
                            Console.CursorLeft = 17 + currentMessage.Length % 67;
                            Console.CursorTop = 24 + currentMessage.Length / 67;
                        }
                        else
                            Console.CursorLeft++;
                        break;
                    case ConsoleKey.Enter:
                        if(!typingMessage)
                        {
                            Console.ResetColor();
                            newMessage[selectedUser] = false;
                            WriteChat();
                            typingMessage = true;
                            indexMessage = 0;
                            Console.CursorVisible = true;
                            Console.CursorLeft = 17;
                            Console.CursorTop = 24;
                            currentMessage = new StringBuilder();
                        }
                        else
                        {
                            ClearMessageBox();
                            ClearDialogBox();

                            string[] strs = new string[(int)Math.Ceiling(currentMessage.Length / 67.0)];
                            int i = 0;
                            for (; i * 67 < currentMessage.Length && (i + 1) * 67 < currentMessage.Length; i++)
                                strs[i] = currentMessage.ToString().Substring(i * 67, 67);
                            strs[i * 67] = currentMessage.ToString().Substring(i);

                            if (!chats.ContainsKey(users[selectedUser]))
                                chats.Add(users[selectedUser], new List<string>());
                            chats[users[selectedUser]].Add($"[{username}]:{Environment.NewLine}{string.Join(Environment.NewLine, strs)}");
                            client.SendMessage(users[selectedUser], currentMessage.ToString());
                            WriteChat();

                            Console.CursorLeft = 17;
                            Console.CursorTop = 24;
                            indexMessage = 0;
                            currentMessage = new StringBuilder();
                        }
                        break;
                    case ConsoleKey.Escape:
                        if (typingMessage)
                        {
                            ClearMessageBox();
                            ClearDialogBox();
                            typingMessage = false;
                            Console.CursorVisible = false;
                            Console.CursorLeft = 1;
                            Console.CursorTop = 1 + selectedUser;
                        }
                        break;
                    default:
                        if (typingMessage)
                        {
                            Console.CursorVisible = false;
                            if (key.Key == ConsoleKey.Backspace)
                            {
                                if (indexMessage > 0)
                                    currentMessage.Remove(--indexMessage, 1);
                            }
                            else if (key.Key == ConsoleKey.Delete)
                            {
                                if (indexMessage < currentMessage.Length)
                                    currentMessage.Remove(indexMessage, 1);
                            }
                            else if (currentMessage.Length < 300)
                                currentMessage.Insert(indexMessage++, key.KeyChar);
                            else
                                break;

                            ClearMessageBox();
                            Console.CursorLeft = 17;
                            Console.CursorTop = 24;
                            int i = 0;
                            for (; i < currentMessage.Length && i + 67 < currentMessage.Length; i += 67)
                            {
                                Console.Write(currentMessage.ToString().Substring(i, 67));
                                Console.CursorLeft = 17;
                                Console.CursorTop++;
                            }
                            Console.Write(currentMessage.ToString().Substring(i));

                            Console.CursorLeft = 17 + indexMessage % 67;
                            Console.CursorTop = 24 + indexMessage / 67;
                            Console.CursorVisible = true;
                        }
                        break;
                }
                busy = false;
            }
        }

        private static void Client_MessageReceived(string from, string message)
        {
            while (busy) ;
            busy = true;
            if (!chats.ContainsKey(from))
                chats.Add(from, new List<string>());

            string[] strs = new string[(int)Math.Ceiling(message.Length / 67.0)];

            int i = 0;
            for (; i * 67 < message.Length && (i + 1) * 67 < message.Length; i++)
                strs[i] = message.Substring(i * 67, 67);
            strs[i * 67] = message.Substring(i);

            chats[from].Add($"[{from}]:{Environment.NewLine}{string.Join(Environment.NewLine, strs)}");

            if (!typingMessage || typingMessage && from != users[selectedUser])
            { 
                int ind = Array.IndexOf(users, from);
                if(ind != -1)
                    newMessage[ind] = true;
                busy = false;
                return;
            }

            ClearDialogBox();
            WriteChat();

            busy = false;
        }

        private static void Client_UsersRefresh(string[] users)
        {
            while (busy) ;
            busy = true;
            List<string> newMess = new List<string>();

            for (int i = 0; i < newMessage.Length; i++)
                if (newMessage[i])
                    newMess.Add(users[i]);

            string select = "";
            if (Program.users != null && selectedUser < Program.users.Length)
                select = Program.users[selectedUser];
            Program.users = users;

            newMessage = new bool[users.Length];
            for (int i = 0; i < users.Length; i++)
                newMessage[i] = newMess.IndexOf(users[i]) != -1;

            int top = Console.CursorTop;
            int left = Console.CursorLeft;
            Console.ResetColor();

            Console.CursorVisible = false;
            Console.CursorTop = users.Length + 1;
            for (int i = 0; i < 28 - users.Length; i++)
            {
                Console.CursorLeft = 1;
                Console.Write(new string(' ', 15));
                Console.CursorTop++;
            }
            Console.CursorTop = 1;
            int index = Array.IndexOf(users, select);
            for (int i = 0; i < users.Length && i < 28; i++)
            {
                Console.CursorLeft = 1;
                if (i != index)
                {
                    if (newMessage[i])
                        Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(users[i].PadRight(15));
                    Console.ResetColor();
                }
                else
                {
                    Console.BackgroundColor = ConsoleColor.White;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write(users[i].PadRight(15));
                    Console.ResetColor();
                }
                Console.CursorTop++;
            }

            if (index != -1)
            {
                Console.CursorTop = top;
                Console.CursorLeft = left;
                Console.CursorVisible = typingMessage;
            }
            else
            {
                Console.CursorTop = 1;
                Console.CursorLeft = 1;
                Console.BackgroundColor = ConsoleColor.White;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.Write(users[0].PadRight(15));
                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.White;
                Console.CursorLeft = 1;
                typingMessage = false;
            }

            busy = false;
        }

        private static void BuildFrame()
        {
            Console.Write("┌" + new string('─', 15) + "┬" + new string('─', 67) + "┐");
            for(int i = 0; i < 22; i++)
                Console.Write("│" + new string(' ', 15) + "│" + new string(' ', 67) + "│");
            Console.Write("│" + new string(' ', 15) + "├" + new string('─', 67) + "┤");
            for (int i = 0; i < 5; i++)
                Console.Write("│" + new string(' ', 15) + "│" + new string(' ', 67) + "│");
            Console.Write("└" + new string('─', 15) + "┴" + new string('─', 67) + "┘");
        }

        private static void ClearMessageBox()
        {
            for (int i = 0; i < 5; i++)
            {
                Console.CursorLeft = 17;
                Console.CursorTop = 24 + i;
                Console.Write(new string(' ', 67));
            }
        }

        private static void ClearDialogBox()
        {
            for (int i = 0; i < 22; i++)
            {
                Console.CursorLeft = 17;
                Console.CursorTop = 1 + i;
                Console.Write(new string(' ', 67));
            }
        }

        private static void WriteChat()
        {
            if (!chats.ContainsKey(users[selectedUser]))
                return;

            string[] strs = new string[22];
            int i = 0;
            List<string> chat = chats[users[selectedUser]];
            for (int j = chat.Count - 1; j >= 0; j--)
            {
                string[] temp = chat[j].Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                if (i + temp.Length > 22)
                    break;
                for (int l = 0; l < temp.Length; l++)
                {
                   strs[i++] = temp[temp.Length - 1 - l];
                }
            }

            Console.CursorVisible = false;

            for (int j = 0; j < i; j++)
            {
                Console.CursorLeft = 17;
                Console.CursorTop = i - j;
                Console.Write(strs[j]);
            }
        }
    }
}
