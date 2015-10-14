using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace SeafConsole
{
    static class SecureStringUtils
    {
        /// <summary>
        /// Read a password from the console and return it as SecureString
        /// </summary>
        /// <returns></returns>
        public static bool ReadPasswordFromConsole(out SecureString secStr)
        {
            secStr = new SecureString();

            for (ConsoleKeyInfo c = Console.ReadKey(true); c.Key != ConsoleKey.Enter; c = Console.ReadKey(true))
            {
                if (c.Key == ConsoleKey.Backspace && secStr.Length > 0)
                    secStr.RemoveAt(secStr.Length - 1);

                if (c.Key == ConsoleKey.Escape)
                {
                    // cancel
                    secStr.Dispose();
                    Console.WriteLine();
                    return false;
                }

                if (!Char.IsControl(c.KeyChar))
                    secStr.AppendChar(c.KeyChar);
            }

            secStr.MakeReadOnly();
            Console.WriteLine();
            return true;
        }

        /// <summary>
        /// Copy the contents of the given SecureString to a char array        
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static char[] SecureStringToCharArray(SecureString s)
        {
            IntPtr p = IntPtr.Zero;
            char[] chars = new char[s.Length];

            try
            {
                p = Marshal.SecureStringToBSTR(s);
                Marshal.Copy(p, chars, 0, s.Length);
                return chars;
            }
            finally
            {
                if (p != IntPtr.Zero)
                    Marshal.ZeroFreeBSTR(p);
            }

        }
    }
}
