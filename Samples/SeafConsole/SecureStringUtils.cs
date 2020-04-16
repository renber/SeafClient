using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SeafConsole
{
    internal static class SecureStringUtils
    {
        /// <summary>
        ///     Read a password from the console and return it as SecureString
        /// </summary>
        /// <returns></returns>
        public static bool ReadPasswordFromConsole(out SecureString pw)
        {
            pw = new SecureString();

            for (var input = Console.ReadKey(true); input.Key != ConsoleKey.Enter; input = Console.ReadKey(true))
            {
                if (input.Key == ConsoleKey.Backspace && pw.Length > 0)
                    pw.RemoveAt(pw.Length - 1);

                if (input.Key == ConsoleKey.Escape)
                {
                    // cancel
                    pw.Dispose();
                    Console.WriteLine();
                    return false;
                }

                if (!char.IsControl(input.KeyChar))
                    pw.AppendChar(input.KeyChar);
            }

            pw.MakeReadOnly();
            Console.WriteLine();

            return true;
        }

        /// <summary>
        ///     Copy the contents of the given SecureString to a char array
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static char[] SecureStringToCharArray(SecureString value)
        {
            var intPtr = IntPtr.Zero;
            var chars = new char[value.Length];

            try
            {
                intPtr = Marshal.SecureStringToBSTR(value);
                Marshal.Copy(intPtr, chars, 0, value.Length);
                return chars;
            }
            finally
            {
                if (intPtr != IntPtr.Zero)
                    Marshal.ZeroFreeBSTR(intPtr);
            }
        }
    }
}