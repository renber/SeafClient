using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeafClient.Requests;
using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Tests
{
    /// <summary>
    /// Contains methods which test the star / unstar file requests
    /// </summary>
    [TestClass]
    public class StarringTests : SeafTestClassBase
    {

        [TestMethod]
        public void Test_ListStarredFiles()
        {
            ListStarredFilesRequest req = new ListStarredFilesRequest(FakeToken);
            HttpResponseMessage response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Content = new StringContent(@"[
            {
                ""repo"": ""99b758e6-91ab-4265-b705-925367374cf0"",
                ""mtime"": 1355198150,
                ""org"": -1,
                ""path"": ""/foo/bar.doc"",
                ""dir"": false,
                ""size"": 0
            },
            {
                ""repo"": ""99b758e6-91ab-4265-b705-925367374cf0"",
                ""mtime"": 1353751237,
                ""org"": -1,
                ""path"": ""/add_folder-blue.png"",
                ""dir"": false,
                ""size"": 3170
            }]");

            var webRequest = this.TestConnection.CreateHttpRequestMessage(DummyServerUri, req);

            Assert.AreEqual(DummyServerUri.ToString() + "api2/starredfiles/", webRequest.RequestUri.ToString());
            Assert.AreEqual(HttpMethod.Get, webRequest.Method);

            Assert.IsTrue(req.WasSuccessful(response));
            var rList = ExecuteSync(() => req.ParseResponseAsync(response));

            Assert.AreEqual(2, rList.Count);
            Assert.AreEqual("bar.doc", rList[0].Name);
            Assert.AreEqual(DirEntryType.File, rList[0].Type);
        }

        [TestMethod]
        public void Test_StarAFile()
        {
            StarFileRequest req = new StarFileRequest(FakeToken, FakeRepoId, "/test/file.txt");
            HttpResponseMessage response = new HttpResponseMessage(System.Net.HttpStatusCode.Created);
            response.Content = new StringContent("\"success\"");

            Assert.AreEqual("api2/starredfiles/", req.CommandUri);
            Assert.AreEqual(HttpAccessMethod.Post, req.HttpAccessMethod);

            var webRequest = this.TestConnection.CreateHttpRequestMessage(DummyServerUri, req);

            Assert.AreEqual(DummyServerUri.ToString() + "api2/starredfiles/", webRequest.RequestUri.ToString());
            Assert.AreEqual(HttpMethod.Post, webRequest.Method);
            string c = ExecuteSync(() => webRequest.Content.ReadAsStringAsync());
            Assert.IsTrue(c.Contains("repo_id=" + FakeRepoId));
            Assert.IsTrue(c.Contains("p=%2Ftest%2Ffile.txt"));

            Assert.IsTrue(req.WasSuccessful(response));
            Assert.IsTrue(ExecuteSync(() => req.ParseResponseAsync(response)));
        }

        [TestMethod]
        public void Test_UnstarAFile()
        {
            UnstarFileRequest req = new UnstarFileRequest(FakeToken, FakeRepoId, "/test/file.txt");
            HttpResponseMessage response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Content = new StringContent("\"success\"");

            Assert.IsTrue(req.CommandUri.StartsWith("api2/starredfiles/"));
            Assert.AreEqual(HttpAccessMethod.Delete, req.HttpAccessMethod);

            var webRequest = this.TestConnection.CreateHttpRequestMessage(DummyServerUri, req);
            
            Assert.AreEqual(HttpMethod.Delete, webRequest.Method);
            string targetUri = webRequest.RequestUri.ToString();
            Assert.IsTrue(targetUri.StartsWith(DummyServerUri.ToString() + "api2/starredfiles/"));
            Assert.IsTrue(targetUri.Contains("repo_id=" + FakeRepoId));
            Assert.IsTrue(targetUri.Contains("p=%2Ftest%2Ffile.txt"));

            Assert.IsTrue(req.WasSuccessful(response));
            Assert.IsTrue(ExecuteSync(() => req.ParseResponseAsync(response)));
        }
    }
}
