using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeafClient.Requests;
using System.Net.Http;

namespace SeafClient.Tests
{
    [TestClass]
    public class DirectoryEntryTests : SeafTestClassBase
    {
        [TestMethod]
        public void Test_ListDirectoryEntries_HttpRequest()
        {
            ListDirectoryEntriesRequest req = new ListDirectoryEntriesRequest(FakeToken, FakeRepoId, "/test/subfolder/");

            var httpReq = TestConnection.CreateHttpRequestMessage(DummyServerUri, req);

            Assert.AreEqual(HttpMethod.Get, httpReq.Method);
            String uri = httpReq.RequestUri.ToString();
            Assert.IsTrue(uri.StartsWith(DummyServerUri + "api2/repos/" + FakeRepoId + "/dir/?"));
            Assert.IsTrue(uri.Contains("p=%2Ftest%2Fsubfolder%2F"));
        }
    }
}
