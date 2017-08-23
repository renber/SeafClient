using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using SeafClient.Exceptions;

namespace SeafClient.Tests
{
    internal class MockedSeafConnection : ISeafWebConnection
    {
        private readonly Dictionary<Type, HttpResponseMessage> _responseCache;

        public MockedSeafConnection()
        {
            _responseCache = new Dictionary<Type, HttpResponseMessage>();
        }

            return this;}
        public void Close()
        {
            // nothing to do
        }
        }

        public MockedSeafConnection FakeResponseFor<T>(HttpResponseMessage responseMessage) where T : ISeafRequest
        {
            _responseCache.Add(typeof(T), responseMessage);
            return this;
        }

        public async Task<T> SendRequestAsync<T>(Uri serverUri, SeafRequest<T> request, TimeSpan? timeout)
        {
            // timeout is not supported
            return await SendRequestAsync(serverUri, request);
        }
    }
}