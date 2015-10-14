using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Tests
{
    /// <summary>
    /// Base class for TestClasses which test the SeafClient library
    /// which defines some useful test properties and functions
    /// </summary>
    public class SeafTestClassBase
    {
        public Uri DummyServerUri { get; }
        public string FakeToken { get; }
        public string FakeRepoId { get; }

        public SeafHttpConnection TestConnection { get; }

        /// <summary>
        /// Execute the given async function synchronously
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        protected T ExecuteSync<T>(Func<Task<T>> func)
        {
            var task = func();
            task.Wait();
            return task.Result;
        }

        public SeafTestClassBase()
        {
            DummyServerUri = new Uri("https://www.test.test:4444/", UriKind.Absolute);
            FakeToken = "24fd3c026886e3121b2ca630805ed425c272cb96";
            FakeRepoId = "632ab8a8-ecf9-4435-93bf-f495d5bfe975";
            TestConnection = new SeafHttpConnection();
        }
    }
}
