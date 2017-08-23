using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using SeafClient;
using SeafClient.Exceptions;
using SeafClient.Types;

namespace SeafConsole
{
    internal class Program
    {
        private static void Main()
        {
            // prompt the user for seafile server, username & password
            var host = string.Empty;
            var user = string.Empty;

            var validUri = false;
            Uri serverUri = null;

            while (!validUri)
            {
                MiscUtils.GetStringFromConsole("Please enter the Seafile server url", ref host);

                try
                {
                    serverUri = new Uri(host, UriKind.Absolute);
                    validUri = true;
                }
                catch (Exception)
                {
                    Console.WriteLine("Not a valid absolute url (Please include http or https)");
                    host = string.Empty;
                }
            }

            MiscUtils.GetStringFromConsole("Please enter your username", ref user);

            // use secure string to retrieve the password from the user
            SecureString pw;
            do
            {
                // repeat input                
                Console.Write("Please enter your password (input will be hidden): ");
            }
            while (!SecureStringUtils.ReadPasswordFromConsole(out pw));

            var pwBuffer = SecureStringUtils.SecureStringToCharArray(pw);
            pw.Dispose();

            // connect to the seafile server and retrieve some information
            var task = SeafClientDemo(serverUri, user, pwBuffer);
            task.Wait();

            Console.WriteLine();
            Console.WriteLine("Press any key to quit.");
            Console.ReadKey();
        }

        private static async Task SeafClientDemo(Uri host, string user, char[] pw)
        {
            try
            {
                // if the seafile server uses a self-signed certificate we accept it
                // for demonstration purposes
                ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, errors) => true;

                // try to connect to the seafile server using the given credentials
                Console.Write("Connecting...");
                var session = await SeafSession.Establish(host, user, pw);
                Console.WriteLine("OK");
                Console.WriteLine();

                // ping the server
                Console.Write("Pinging the server...");
                if (await session.Ping())
                    Console.WriteLine("OK");
                else
                    Console.WriteLine("Failed");

                // retrieve user account info
                var info = await session.CheckAccountInfo();
                Console.WriteLine("Account info: ");
                Console.WriteLine("User name: " + info.Name);
                Console.WriteLine("Space used: " + MiscUtils.FormatByteSize(info.Usage));
                Console.WriteLine("Space total: " +
                                  (info.HasUnlimitedSpace ? "unlimited" : MiscUtils.FormatByteSize(info.Quota)));
                Console.WriteLine("");

                // create a test library
                // Console.Write("Creating library...");
                // var newLib = await session.CreateLibrary("TestLib", "123");
                // Console.WriteLine("OK");

                // default library
                var defLib = await session.GetDefaultLibrary();
                Console.WriteLine("Default library: " + defLib.Name);

                // retrieve user's libraries & shared libraries
                var libs = await session.ListLibraries();
                libs = libs.Union(await session.ListSharedLibraries()).ToList();
                Console.WriteLine("Libraries:");

                IList<string[]> lines = new List<string[]>();
                lines.Add(new[] { "Modified", "Permission", "Name", "Owner" });

                foreach (var lib in libs)
                {
                    var permission = lib.Permission == SeafPermission.ReadOnly ? "r" : "rw";
                    lines.Add(new[] { lib.Timestamp.ToString(), permission, lib.Name, lib.Owner });
                }

                Console.WriteLine(MiscUtils.PadElementsInLines(lines, 2));
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException != null)
                    throw ex.InnerException;
            }
            catch (SeafException ex)
            {
                Console.WriteLine("Failed");
                Console.WriteLine("Request failed: " + ex.Message);
            }
        }
    }
}