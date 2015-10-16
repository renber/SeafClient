using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Tests
{
    class MockedSeafConnection : ISeafWebConnection        
    {
        private Dictionary<Type, HttpResponseMessage> responseCache;

        public MockedSeafConnection()
        {
            responseCache = new Dictionary<Type, HttpResponseMessage>();
        }

        public MockedSeafConnection FakeResponseFor<T>(HttpResponseMessage responseMessage)
            where T : ISeafRequest
        {            
            responseCache.Add(typeof(T), responseMessage);
            return this;
        }

        public async Task<T> SendRequestAsync<T>(Uri serverUri, SeafRequest<T> request)            
        {
            HttpResponseMessage response;            
            if (responseCache.TryGetValue(request.GetType(), out response))
            {
                if (request.WasSuccessful(response))
                    return await request.ParseResponseAsync(response);
                else
                    throw new SeafException(request.GetSeafError(response));
            } else
                throw new Exception("No mocked response for the request available.");
        }
    }
}
