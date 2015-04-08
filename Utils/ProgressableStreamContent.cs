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

    enum UploadState { PendingUpload, Uploading, PendingResponse }

    /// <summary>
    /// HttpContent which has a callback for upload progress
    /// </summary>
    class ProgressableStreamContent : HttpContent
    {
        private const int defaultBufferSize = 4096;

        private Stream content;
        private int bufferSize;
        private bool contentConsumed;

        public UploadState State { get; set; }
        long UploadedBytes = 0;

        Action<float> ProgressHandler;

        public ProgressableStreamContent(Stream content, Action<float> progressHandler) : this(content, defaultBufferSize, progressHandler) { }

        public ProgressableStreamContent(Stream content, int bufferSize, Action<float> progressHandler)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("bufferSize");
            }

            State = UploadState.PendingUpload;

            this.content = content;
            this.bufferSize = bufferSize;
            this.ProgressHandler = progressHandler;
        }

        protected async override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {            
            PrepareContent();

            State = UploadState.PendingUpload;

            var buffer = new Byte[this.bufferSize];
            var size = content.Length;
            long uploaded = 0;

            State = UploadState.PendingUpload;

            using (content) while (true)
                {
                    var length = content.Read(buffer, 0, buffer.Length);
                    if (length <= 0) break;

                    UploadedBytes = uploaded += length;                

                    State = UploadState.Uploading;
                    await stream.WriteAsync(buffer, 0, length);

                    if (ProgressHandler != null)                    
                        ProgressHandler(UploadedBytes * 100 / (float)size);                    
                }

            State = UploadState.PendingResponse;
        }

        public long ComputeLength()
        {
            long length;
            if (TryComputeLength(out length))
                return length;
            else
                return -1;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = content.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                content.Dispose();
            }
            base.Dispose(disposing);
        }


        private void PrepareContent()
        {
            if (contentConsumed)
            {
                // If the content needs to be written to a target stream a 2nd time, then the stream must support
                // seeking (e.g. a FileStream), otherwise the stream can't be copied a second time to a target 
                // stream (e.g. a NetworkStream).
                if (content.CanSeek)
                {
                    content.Position = 0;
                }
                else
                {
                    throw new InvalidOperationException("SR.net_http_content_stream_already_read");
                }
            }

            contentConsumed = true;
        }
    }
}
