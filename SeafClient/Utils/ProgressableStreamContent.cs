using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SeafClient.Utils
{
    internal enum UploadState
    {
        PendingUpload,
        Uploading,
        PendingResponse
    }

    /// <summary>
    ///     <see cref="HttpContent"/> which has a callback for upload progress
    /// </summary>
    internal class ProgressableStreamContent : HttpContent
    {
        private const int DefaultBufferSize = 4096;
        private readonly int _bufferSize;

        private readonly Stream _content;
        private bool _contentConsumed;

        private readonly Action<float> _progressHandler;
        private long _uploadedBytes;

        public ProgressableStreamContent(Stream content, Action<float> progressHandler) : this(content, DefaultBufferSize, progressHandler)
        {
        }

        public ProgressableStreamContent(Stream content, int bufferSize, Action<float> progressHandler)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            State = UploadState.PendingUpload;

            _content = content;
            _bufferSize = bufferSize;
            _progressHandler = progressHandler;
        }

        public UploadState State { get; set; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            PrepareContent();

            State = UploadState.PendingUpload;

            var buffer = new byte[_bufferSize];
            var size = _content.Length;
            long uploaded = 0;

            State = UploadState.PendingUpload;

            using (_content)
            {
                while (true)
                {
                    var length = _content.Read(buffer, 0, buffer.Length);
                    if (length <= 0) break;

                    _uploadedBytes = uploaded += length;

                    State = UploadState.Uploading;
                    await stream.WriteAsync(buffer, 0, length);

                    _progressHandler?.Invoke(_uploadedBytes * 100 / (float) size);
                }
            }

            State = UploadState.PendingResponse;
        }

        public long ComputeLength()
        {
            long length;

            if (TryComputeLength(out length))
                return length;

            return -1;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _content.Dispose();

            base.Dispose(disposing);
        }


        private void PrepareContent()
        {
            if (_contentConsumed)
            {
                if (_content.CanSeek)
                    _content.Position = 0;
                else
                    throw new InvalidOperationException("SR.net_http_content_stream_already_read");
            }

            _contentConsumed = true;
        }
    }
}