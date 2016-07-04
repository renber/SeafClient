using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Utils
{
    /// <summary>
    /// A HttpContent implementation to transmit form data (key-value pairs)
    /// which zeroes out the value arrays as soon as the content has been written to a request stream
    /// (Implementation of FormUrlEncodedContent which does not leak values in memory after the request has been sent)
    /// </summary>
    class CredentialFormContent : HttpContent
    {
        List<KeyValuePair<string, char[]>> FormData;

        public CredentialFormContent(params KeyValuePair<string, char[]>[] formData)
            : base()
        {
            this.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");

            FormData = new List<KeyValuePair<string, char[]>>();
            foreach(var kv in formData)
            {
                var pair = new KeyValuePair<string, char[]>(kv.Key, new char[kv.Value.Length]);
                Array.Copy(kv.Value, pair.Value, kv.Value.Length);
                FormData.Add(pair);
            }            
        }        

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            try
            {
                await WriteFormDataToStream(stream);
            }
            finally
            {
                ClearPassword();
            }
        }        

        async Task WriteFormDataToStream(Stream stream)
        {
            byte[] andBuf = Encoding.UTF8.GetBytes("&");

            for (int i = 0; i < FormData.Count; i++)
            {
                var pair = FormData[i];
                // enumerate parameters with &
                if (i > 0)
                    await stream.WriteAsync(andBuf, 0, andBuf.Length);

                // write key=value
                await WritePairToStream(stream, pair);
            }
        }

        async Task WritePairToStream(Stream stream, KeyValuePair<String, char[]> pair)
        {
            byte[] eqValue = Encoding.UTF8.GetBytes("=");
            byte[] utf8Value = null;
            byte[] encodedValue = null;
            try
            {
                // write key (and escape it first)
                utf8Value = Encoding.UTF8.GetBytes(pair.Key);
                encodedValue = WebUtility.UrlEncodeToBytes(utf8Value, 0, utf8Value.Length);
                await stream.WriteAsync(encodedValue, 0, encodedValue.Length);
                // write '='                
                await stream.WriteAsync(eqValue, 0, eqValue.Length);
                Array.Clear(utf8Value, 0, utf8Value.Length);

                // write value (and escape it first)
                utf8Value = Encoding.UTF8.GetBytes(pair.Value);
                encodedValue = WebUtility.UrlEncodeToBytes(utf8Value, 0, utf8Value.Length);
                await stream.WriteAsync(encodedValue, 0, encodedValue.Length);
            } finally
            {                
                if (encodedValue != null)
                    Array.Clear(encodedValue, 0, encodedValue.Length);
                if (utf8Value != null)
                    Array.Clear(utf8Value, 0, utf8Value.Length);
            }
        }

        void ClearPassword()
        {
            foreach(var pair in FormData)
                Array.Clear(pair.Value, 0, pair.Value.Length);
        }

        protected override bool TryComputeLength(out long length)
        { 
            length = -1;
            return false;           
        }
    }
}
