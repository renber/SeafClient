using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Utils
{
    /// <summary>
    /// A FormUrlEncodedContent implementation to transmit username and password
    /// which zeroes out the password array as soon as the password has been sent
    /// </summary>
    class CredentialFormContent : FormUrlEncodedContent
    {
        public  string Username {get; set;}
        public byte[] Password { get; set; }

        public CredentialFormContent(string username, byte[] password)
            : base(new KeyValuePair<string,string>[0])
        {
            Username = username;
            Password = password;            
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            try
            {
                byte[] buf = Encoding.UTF8.GetBytes("username=" + Username);
                await stream.WriteAsync(buf, 0, buf.Length);

                buf = Encoding.UTF8.GetBytes("&password=");
                await stream.WriteAsync(buf, 0, buf.Length);

                await stream.WriteAsync(Password, 0, Password.Length);
            }
            finally
            {
                ClearPassword();
            }            
        }

        void ClearPassword()
        {
            for (int i = 0; i < Password.Length; i++)
                Password[i] = 0;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}
