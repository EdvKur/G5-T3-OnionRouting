using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OnionRouting
{
    public class Log
    {
        private static object locker = new object();
    
        static Log()
        {
            Console.WindowWidth = 140;
            Console.WindowHeight = 50;

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void info(string format, params object[] args)
        {
            lock (locker)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("INFO  {0} | ", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
                Console.WriteLine(format, args);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        public static void error(string format, params object[] args)
        {
            lock (locker)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("ERROR {0} | ", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
                Console.WriteLine(format, args);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }


    }
}
