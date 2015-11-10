using SeafClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace SeafConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            // prompt the user for seafile server, username & password
            string host = "https://raspsea.my-homeip.de:48000";
            string user = "dev@genie-soft.de";

            bool validUri = false;
            Uri serverUri = null;

            while (!validUri)
            {
                MiscUtils.GetStringFromConsole("Please enter the Seafile server url", ref host);                
                         
                try
                {
                    serverUri = new Uri(host, UriKind.Absolute);
                    validUri = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Not a valid absolute url (Please include http or https)");
                    host = "";
                }
            }
            
            MiscUtils.GetStringFromConsole("Please enter your username", ref user);

            // use secure string to retrieve the password from the user
            SecureString pw;            
            do
            {
                // repeat input                
                Console.Write("Please enter your password (input will be hidden): ");                
            } while (!SecureStringUtils.ReadPasswordFromConsole(out pw));            

            char[] pwBuf = SecureStringUtils.SecureStringToCharArray(pw);
            pw.Dispose();            

            // connect to the seafile server and retrieve some information
            var t = SeafClientDemo(serverUri, user, pwBuf);
            t.Wait();

            Console.WriteLine();
            Console.WriteLine("Press any key to quit.");
            Console.ReadKey();
        }

        static async Task SeafClientDemo(Uri host, String user, char[] pw)
        {
            try
            {
                // if the seafile server uses a self-signed certificate we accept it
                // for demonstration purposes
                System.Net.ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, errors) =>
                {
                    return true;
                };

                // try to connect to the seafile server using the given credentials
                Console.Write("Connecting...");
                SeafSession session = await SeafSession.Establish(host, user, pw);
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
                Console.WriteLine("User name: " + info.Nickname);
                Console.WriteLine("Space used: " + MiscUtils.FormatByteSize(info.Usage));
                Console.WriteLine("Space total: " + (info.HasUnlimitedSpace ? "unlimited" : MiscUtils.FormatByteSize(info.Quota)));
                Console.WriteLine("");

                // retrieve user's libraries & shared libraries
                var libs = await session.ListLibraries();
                libs.Union(await session.ListSharedLibraries());
                Console.WriteLine("Libraries:");

                IList<string[]> lines = new List<string[]>();
                lines.Add(new string[] { "Modified", "Permission", "Name", "Owner" });
                foreach (var lib in libs)
                {
                    string permission = lib.Permission == SeafClient.Types.SeafPermission.ReadOnly ? "r" : "rw";
                    lines.Add(new string[] { lib.Timestamp.ToString(), permission, lib.Name, lib.Owner });                    
                }
                Console.WriteLine(MiscUtils.PadElementsInLines(lines, 2));
            } catch (SeafException ex)
            {
                Console.WriteLine("Failed");
                Console.WriteLine("Request failed: " + ex.Message);
            }
        }
    }
}
