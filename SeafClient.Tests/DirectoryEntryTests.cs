using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeafClient.Requests.Directories;
using SeafClient.Requests.Files;
using SeafClient.Types;

namespace SeafClient.Tests
{
    [TestClass]
    public class DirectoryEntryTests : SeafTestClassBase
    {
        [TestMethod]
        public void Test_ListDirectoryEntries_HttpRequest()
        {
            var request = new ListDirectoryEntriesRequest(FakeToken, FakeRepoId, "/test/subfolder/");
            var httpRequest = TestConnection.CreateHttpRequestMessage(DummyServerUri, request);

            Assert.AreEqual(HttpMethod.Get, httpRequest.Method);
            var uri = httpRequest.RequestUri.ToString();
            Assert.IsTrue(uri.StartsWith(DummyServerUri + "api2/repos/" + FakeRepoId + "/dir/?"));
            // test path parameter
            Assert.IsTrue(uri.Contains("p=%2Ftest%2Fsubfolder%2F"));
        }

        [TestMethod]
        public void Test_ListDirectoryEntries_Success()
        {
            var request = new ListDirectoryEntriesRequest(FakeToken, FakeRepoId, "/test/subfolder/");

            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"[{
                            ""id"": ""0000000000000000000000000000000000000000"",
                            ""type"": ""file"",
                            ""name"": ""test1.c"",
                            ""size"": 5431
                          }, {
                            ""id"": ""e4fe14c8cda2206bb9606907cf4fca6b30221cf9"",
                            ""type"": ""dir"",
                            ""name"": ""test_dir""}]")
            };

            Assert.IsTrue(request.WasSuccessful(message));
            var result = ExecuteSync(() => request.ParseResponseAsync(message));
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result.Count(x => x.Type == DirEntryType.Dir));
            Assert.AreEqual(1, result.Count(x => x.Type == DirEntryType.File));
            Assert.AreEqual("test1.c", result[0].Name);
            Assert.AreEqual(5431, result[0].Size);
            Assert.AreEqual(FakeRepoId, result[0].LibraryId);
            Assert.AreEqual("/test/subfolder/test_dir", result[1].Path);
            Assert.AreEqual("e4fe14c8cda2206bb9606907cf4fca6b30221cf9", result[1].Id);
        }

        [TestMethod]
        public void Test_AccountInfo_Error()
        {
            var request = new ListDirectoryEntriesRequest(FakeToken, FakeRepoId, "/test/subfolder/");
            var message = new HttpResponseMessage(HttpStatusCode.NotFound);

            Assert.IsFalse(request.WasSuccessful(message));
        }

        [TestMethod]
        public void Test_DeleteDirEntry_HttpRequest()
        {
            var request = new DeleteDirEntryRequest(FakeToken, FakeRepoId, "/test/subfolder/");
            var httpRequest = TestConnection.CreateHttpRequestMessage(DummyServerUri, request);

            Assert.AreEqual(HttpMethod.Delete, httpRequest.Method);
            var uri = httpRequest.RequestUri.ToString();
            Assert.IsTrue(uri.StartsWith(DummyServerUri + "api2/repos/" + FakeRepoId + "/dir/?"));
            // test path parameter
            Assert.IsTrue(uri.Contains("p=%2Ftest%2Fsubfolder%2F"));
        }

        [TestMethod]
        public void Test_DeleteDirEntry_Success()
        {
            var request = new DeleteDirEntryRequest(FakeToken, FakeRepoId, "/test/subfolder/");
            var message = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"success\"") };

            Assert.IsTrue(request.WasSuccessful(message));
            Assert.IsTrue(ExecuteSync(() => request.ParseResponseAsync(message)));
        }

        [TestMethod]
        public void Test_DeleteDirEntry_Error()
        {
            var request = new DeleteDirEntryRequest(FakeToken, FakeRepoId, "/test/subfolder/");
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest);

            Assert.IsFalse(request.WasSuccessful(message));
            Assert.AreEqual(SeafErrorCode.PathDoesNotExist, request.GetSeafError(message).SeafErrorCode);
        }

        [TestMethod]
        public void Test_MoveFileEntry_Success()
        {
            var request = new MoveFileRequest(FakeToken, FakeRepoId, "/test/file.txt", FakeRepoId, "/newdir/");
            var message = new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Content = new StringContent("\"success\"") };

            Assert.IsTrue(request.WasSuccessful(message));
        }

        [TestMethod]
        public void Test_MoveFileEntry_Error()
        {
            var request = new MoveFileRequest(FakeToken, FakeRepoId, "/test/file.txt", FakeRepoId, "/newdir/");

            var message = new HttpResponseMessage(HttpStatusCode.Forbidden);
            Assert.IsFalse(request.WasSuccessful(message));

            message = new HttpResponseMessage(HttpStatusCode.BadRequest);
            Assert.IsFalse(request.WasSuccessful(message));

            // there seems to be a bug in the seafile web api
            // as the server returns NotFound even when the renaming was successful
            // so we cannot test this

            // message = new HttpResponseMessage(HttpStatusCode.NotFound);
            // Assert.IsFalse(req.WasSuccessful(m));

            message = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            Assert.IsFalse(request.WasSuccessful(message));
        }


        [TestMethod]
        public void Test_CopyFileEntry_Success()
        {
            var request = new CopyFileRequest(FakeToken, FakeRepoId, "/test/file.txt", FakeRepoId, "/newdir/");
            var message = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"success\"") };

            Assert.IsTrue(request.WasSuccessful(message));
        }

        [TestMethod]
        public void Test_CopyFileEntry_Error()
        {
            var request = new CopyFileRequest(FakeToken, FakeRepoId, "/test/file.txt", FakeRepoId, "/newdir/");

            var message = new HttpResponseMessage(HttpStatusCode.Forbidden);
            Assert.IsFalse(request.WasSuccessful(message));

            message = new HttpResponseMessage(HttpStatusCode.BadRequest);
            Assert.IsFalse(request.WasSuccessful(message));

            message = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            Assert.IsFalse(request.WasSuccessful(message));
        }
    }
}