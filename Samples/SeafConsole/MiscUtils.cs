using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafConsole
{
    /// <summary>
    ///     Some misc functions used in this sample application
    /// </summary>
    internal static class MiscUtils
    {
        private static readonly string[] Prefixes = { "Bytes", "KB", "MB", "GB", "TB" };

        /// <summary>
        ///     Format the given byte size with the most appropriate suffix
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static string FormatByteSize(double size)
        {
            var i = 0;

            while (size > 1024.0 && i < Prefixes.Length - 1)
            {
                size /= 1024.0f;
                i++;
            }

            return $"{size:0.##} {Prefixes[i]}";
        }

        /// <summary>
        ///     Shows an input prompt in the console.
        ///     If value is null or empty the user can input a value otherwise
        ///     the existing value will be printed
        /// </summary>
        /// <param name="description"></param>
        /// <param name="value"></param>
        public static void GetStringFromConsole(string description, ref string value)
        {
            Console.Write(description + ": ");
            if (string.IsNullOrEmpty(value))
                value = Console.ReadLine();
            else
                Console.WriteLine(value);
        }

        /// <summary>
        ///     Converts a List of string arrays to a string where each element in each line is correctly padded.
        ///     Make sure that each array contains the same amount of elements!
        ///     - Example without:
        ///     Title Name Street
        ///     Mr. Roman Sesamstreet
        ///     Mrs. Claudia Abbey Road
        ///     - Example with:
        ///     Title   Name      Street
        ///     Mr.     Roman     Sesamstreet
        ///     Mrs.    Claudia   Abbey Road
        ///     <param name="lines">List lines, where each line is an array of elements for that line.</param>
        ///     <param name="padding">Additional padding between each element (default = 1)</param>
        /// </summary>
        /// <see cref="http://stackoverflow.com/questions/4449021/how-can-i-align-text-in-columns-using-console-writeline" />
        public static string PadElementsInLines(IList<string[]> lines, int padding = 1)
        {
            // Calculate maximum numbers for each element accross all lines
            var numElements = lines[0].Length;
            var maxValues = new int[numElements];

            for (var i = 0; i < numElements; i++)
                maxValues[i] = lines.Max(x => x[i].Length) + padding;

            var builder = new StringBuilder();

            // Build the output
            var isFirst = true;
            foreach (var line in lines)
            {
                if (!isFirst)
                    builder.AppendLine();

                isFirst = false;

                for (var i = 0; i < line.Length; i++)
                {
                    var value = line[i];
                    // Append the value with padding of the maximum length of any value for this element
                    builder.Append(value.PadRight(maxValues[i]));
                }
            }

            return builder.ToString();
        }
    }
}