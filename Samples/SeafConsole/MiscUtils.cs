using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafConsole
{
    /// <summary>
    /// Some misc functions used in this sample application
    /// </summary>
    static class MiscUtils
    {
        static string[] prefixes = new string[] { "Bytes", "KB", "MB", "GB", "TB" };

        /// <summary>
        /// Format the given byte size with the most appropriate suffix
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static String FormatByteSize(double size)
        {
            int i = 0;

            while (size > 1024.0 && i < prefixes.Length - 1)
            {
                size /= 1024.0f;
                i++;
            }

            return String.Format("{0:0.##} {1}", size, prefixes[i]);
        }

        /// <summary>
        /// Shows an input prompt in the console.
        /// If value is null or empty the user can input a value otherwise
        /// the existing value will be printed
        /// </summary>
        /// <param name="description"></param>
        /// <param name="value"></param>
        public static void GetStringFromConsole(string description, ref string value)
        {
            Console.Write(description + ": ");
            if (String.IsNullOrEmpty(value))
            {
                value = Console.ReadLine();                
            }
            else
                Console.WriteLine(value);
        }

        /// <summary>
        /// Converts a List of string arrays to a string where each element in each line is correctly padded.
        /// Make sure that each array contains the same amount of elements!
        /// - Example without:
        /// Title Name Street
        /// Mr. Roman Sesamstreet
        /// Mrs. Claudia Abbey Road
        /// - Example with:
        /// Title   Name      Street
        /// Mr.     Roman     Sesamstreet
        /// Mrs.    Claudia   Abbey Road
        /// <param name="lines">List lines, where each line is an array of elements for that line.</param>
        /// <param name="padding">Additional padding between each element (default = 1)</param>
        /// </summary>
        /// <see cref="http://stackoverflow.com/questions/4449021/how-can-i-align-text-in-columns-using-console-writeline"/> 
        public static string PadElementsInLines(IList<string[]> lines, int padding = 1)
        {
            // Calculate maximum numbers for each element accross all lines
            var numElements = lines[0].Length;
            var maxValues = new int[numElements];
            for (int i = 0; i < numElements; i++)
            {
                maxValues[i] = lines.Max(x => x[i].Length) + padding;
            }
            var sb = new StringBuilder();
            // Build the output
            bool isFirst = true;
            foreach (var line in lines)
            {
                if (!isFirst)
                {
                    sb.AppendLine();
                }
                isFirst = false;
                for (int i = 0; i < line.Length; i++)
                {
                    var value = line[i];
                    // Append the value with padding of the maximum length of any value for this element
                    sb.Append(value.PadRight(maxValues[i]));
                }
            }
            return sb.ToString();
        }

    }
}
