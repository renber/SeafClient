using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeafClient.Requests.StarredFiles;
using SeafClient.Types;

namespace SeafClient.Tests
{
    /// <summary>
    ///     Contains methods which test the star / unstar file requests
    /// </summary>
    [TestClass]
    public class StarringTests : SeafTestClassBase
    {
        [TestMethod]
        public void Test_ListStarredFiles()
        {
            var request = new ListStarredFilesRequest(FakeToken);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"[{
                    ""repo"": ""99b758e6-91ab-4265-b705-925367374cf0"",
                    ""mtime"": 1355198150,
                    ""org"": -1,
                    ""path"": ""/foo/bar.doc"",
                    ""dir"": false,
                    ""size"": 0
                    },{
                    ""repo"": ""99b758e6-91ab-4265-b705-925367374cf0"",
                    ""mtime"": 1353751237,
                    ""org"": -1,
                    ""path"": ""/add_folder-blue.png"",
                    ""dir"": false,
                    ""size"": 3170
                    }]")
            };

            var webRequest = TestConnection.CreateHttpRequestMessage(DummyServerUri, request);

            Assert.AreEqual(DummyServerUri + "api2/starredfiles/", webRequest.RequestUri.ToString());
            Assert.AreEqual(HttpMethod.Get, webRequest.Method);

            Assert.IsTrue(request.WasSuccessful(response));
            var resultEntries = ExecuteSync(() => request.ParseResponseAsync(response));

            Assert.AreEqual(2, resultEntries.Count);
            Assert.AreEqual("bar.doc", resultEntries[0].Name);
            Assert.AreEqual(DirEntryType.File, resultEntries[0].Type);
        }

        [TestMethod]
        public void Test_StarAFile()
        {
            var request = new StarFileRequest(FakeToken, FakeRepoId, "/test/file.txt");
            var response = new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("\"success\"") };

            Assert.AreEqual("api2/starredfiles/", request.CommandUri);
            Assert.AreEqual(HttpAccessMethod.Post, request.HttpAccessMethod);

            var webRequest = TestConnection.CreateHttpRequestMessage(DummyServerUri, request);

            Assert.AreEqual(DummyServerUri + "api2/starredfiles/", webRequest.RequestUri.ToString());
            Assert.AreEqual(HttpMethod.Post, webRequest.Method);
            var result = ExecuteSync(() => webRequest.Content.ReadAsStringAsync());
            Assert.IsTrue(result.Contains("repo_id=" + FakeRepoId));
            Assert.IsTrue(result.Contains("p=%2Ftest%2Ffile.txt"));

            Assert.IsTrue(request.WasSuccessful(response));
            Assert.IsTrue(ExecuteSync(() => request.ParseResponseAsync(response)));
        }

        [TestMethod]
        public void Test_UnstarAFile()
        {
            var request = new UnstarFileRequest(FakeToken, FakeRepoId, "/test/file.txt");
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"success\"") };

            Assert.IsTrue(request.CommandUri.StartsWith("api2/starredfiles/"));
            Assert.AreEqual(HttpAccessMethod.Delete, request.HttpAccessMethod);

            var webRequest = TestConnection.CreateHttpRequestMessage(DummyServerUri, request);

            Assert.AreEqual(HttpMethod.Delete, webRequest.Method);
            var targetUri = webRequest.RequestUri.ToString();
            Assert.IsTrue(targetUri.StartsWith(DummyServerUri + "api2/starredfiles/"));
            Assert.IsTrue(targetUri.Contains("repo_id=" + FakeRepoId));
            Assert.IsTrue(targetUri.Contains("p=%2Ftest%2Ffile.txt"));

            Assert.IsTrue(request.WasSuccessful(response));
            Assert.IsTrue(ExecuteSync(() => request.ParseResponseAsync(response)));
        }
    }
}