using SeafClient.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Net.Http.Headers;
using System.Diagnostics;
using SeafClient.Types;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request to update an already existing file using a previously rterieved update link
    /// </summary>
    public class UpdateRequest : SessionRequest<bool>
    {
        Action<float> UploadProgress;

        public string UploadUri { get; set; }

        public string TargetDirectory { get; set; }

        UploadFileInfo file = null;

        public UploadFileInfo File
        {
            get
            {
                return file;
            }
        }

        public override string CommandUri
        {
            get { return UploadUri; }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Custom; }
        }

        /// <summary>
        /// Create an upload request for a single file
        /// </summary>
        /// <param name="authToken"></param>
        /// <param name="uploadUri"></param>
        /// <param name="filename"></param>
        /// <param name="fileContent"></param>
        /// <param name="progressCallback"></param>
        public UpdateRequest(string authToken, string uploadUri, string targetDirectory, string filename, Stream fileContent, Action<float> progressCallback)
            : this(authToken, uploadUri, targetDirectory, progressCallback, new UploadFileInfo(filename, fileContent))
        {
            // --
        }

        /// <summary>
        /// Create an upload request for multiple file
        /// </summary>
        /// <param name="authToken"></param>
        /// <param name="uploadUri"></param>
        /// <param name="filename"></param>
        /// <param name="fileContent"></param>
        /// <param name="progressCallback"></param>
        public UpdateRequest(string authToken, string uploadUri, string targetDirectory, Action<float> progressCallback, UploadFileInfo updateFile)
            : base(authToken)
        {
            UploadUri = uploadUri;
            UploadProgress = progressCallback;
            TargetDirectory = targetDirectory;

            file = updateFile;
        }

        public override async Task<bool> ParseResponseAsync(HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();
            return !String.IsNullOrEmpty(content);
        }

        public override SeafError GetSeafError(System.Net.Http.HttpResponseMessage msg)
        {
            switch (msg.StatusCode)
            {
                case (System.Net.HttpStatusCode)440:
                    return new SeafError(msg.StatusCode, SeafErrorCode.FileNotFound);
                default:
                    return base.GetSeafError(msg);
            }
        }

        public override HttpRequestMessage GetCustomizedRequest(Uri serverUri)
        {
            string boundary = "Upload---------" + Guid.NewGuid().ToString();

            var request = new HttpRequestMessage(HttpMethod.Post, UploadUri);

            // add aditional headers
            foreach (var hi in GetAdditionalHeaders())
                request.Headers.Add(hi.Key, hi.Value);

            var content = new MultipartFormDataContent(boundary);

            // Add file to upload to the request                
            var fileContent = new ProgressableStreamContent(File.FileContent, (p) =>
            {
                if (UploadProgress != null)
                    UploadProgress(p);
            });
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            fileContent.Headers.TryAddWithoutValidation("Content-Disposition", String.Format("form-data; name=\"file\"; filename=\"{0}\"", File.Filename));

            content.Add(fileContent);

            // the parent dir to upload the file to
            string tDir = TargetDirectory;
            if (!tDir.StartsWith("/"))
                tDir = "/" + tDir;
            if (!tDir.EndsWith("/"))
                tDir = tDir + "/";

            var dirContent = new StringContent(tDir + File.Filename, Encoding.UTF8);
            dirContent.Headers.ContentType = null;
            dirContent.Headers.TryAddWithoutValidation("Content-Disposition", @"form-data; name=""target_file""");
            content.Add(dirContent);

            // transmit the content length, for this we use the private method TryComputeLength() called by reflection
            long conLen = 0;
            var func = typeof(MultipartContent).GetTypeInfo().GetDeclaredMethod("TryComputeLength");

            object[] args = new object[] { 0L };
            var r = func.Invoke(content, args);
            if (r is bool && (bool)r)
                conLen = (long)args[0];

            // the seafile-server implementation rejects the content-type if the boundary value is
            // placed inside quotes which is what HttpClient does, so we have to redefine the content-type without using quotes
            // and remove the actual content-type which uses quotes beforehand
            content.Headers.ContentType = null;
            content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + boundary);

            //client.DefaultRequestHeaders.TransferEncodingChunked = true;                
            if (conLen > 0)
            {
                // in order to disable buffering
                // and make the progress work
                content.Headers.Add("Content-Length", conLen.ToString());
            }

            request.Content = content;

            return request;
        }
    }
}
