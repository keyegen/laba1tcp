using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        public static int port = 15465;
        static void Main(string[] args)
        {
            Server server = new Server(Console.WriteLine);
            server.Start();
            Console.WriteLine("введите help или ? для вызова справки");
            while(true)
            {
                string read = Console.ReadLine();
                switch(read)
                {
                    case "close":
                    case "stop":
                    case "exit":
                        server.Stop().ContinueWith((Task t) => Environment.Exit(0));
                        break;
                    case "clear":
                        Console.Clear();
                        break;
                    case "list":
                    case "users":
                    case "online":
                        Console.WriteLine(string.Join(Environment.NewLine, server.GetUsers()));
                        break;
                    case "help":
                    case "?":
                        Console.WriteLine("close/stop/exit - завершить работу сервера");
                        Console.WriteLine("clear - очистить консоль");
                        Console.WriteLine("list/users/online - вывод списка подключенных пользователей");
                        break;
                }
            }
        }
    }
}
