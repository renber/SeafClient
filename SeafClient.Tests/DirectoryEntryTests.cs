using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeafClient.Requests;
using System.Net.Http;
using System.Net;
using System.Linq;
using SeafClient.Types;
using System.Collections.Generic;

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
            // test path parameter
            Assert.IsTrue(uri.Contains("p=%2Ftest%2Fsubfolder%2F"));
        }

        [TestMethod]
        public void Test_ListDirectoryEntries_Success()
        {
            ListDirectoryEntriesRequest req = new ListDirectoryEntriesRequest(FakeToken, FakeRepoId, "/test/subfolder/");

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent(@"[{
                            ""id"": ""0000000000000000000000000000000000000000"",
                            ""type"": ""file"",
                            ""name"": ""test1.c"",
                            ""size"": 5431
                          }, {
                            ""id"": ""e4fe14c8cda2206bb9606907cf4fca6b30221cf9"",
                            ""type"": ""dir"",
                            ""name"": ""test_dir""}]");

            Assert.IsTrue(req.WasSuccessful(m));
            IList<SeafDirEntry> result = ExecuteSync(() => req.ParseResponseAsync(m));
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
            ListDirectoryEntriesRequest req = new ListDirectoryEntriesRequest(FakeToken, FakeRepoId, "/test/subfolder/");
            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.NotFound);

            Assert.IsFalse(req.WasSuccessful(m));            
        }

        [TestMethod]
        public void Test_DeleteDirEntry_HttpRequest()
        {
            DeleteDirEntryRequest req = new DeleteDirEntryRequest(FakeToken, FakeRepoId, "/test/subfolder/");

            var httpReq = TestConnection.CreateHttpRequestMessage(DummyServerUri, req);

            Assert.AreEqual(HttpMethod.Delete, httpReq.Method);
            String uri = httpReq.RequestUri.ToString();
            Assert.IsTrue(uri.StartsWith(DummyServerUri + "api2/repos/" + FakeRepoId + "/dir/?"));
            // test path parameter
            Assert.IsTrue(uri.Contains("p=%2Ftest%2Fsubfolder%2F"));
        }

        [TestMethod]
        public void Test_DeleteDirEntry_Success()
        {
            DeleteDirEntryRequest req = new DeleteDirEntryRequest(FakeToken, FakeRepoId, "/test/subfolder/");

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent("\"success\"");

            Assert.IsTrue(req.WasSuccessful(m));
            Assert.IsTrue(ExecuteSync(() => req.ParseResponseAsync(m)));
        }

        [TestMethod]
        public void Test_DeleteDirEntry_Error()
        {
            DeleteDirEntryRequest req = new DeleteDirEntryRequest(FakeToken, FakeRepoId, "/test/subfolder/");

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.BadRequest);            

            Assert.IsFalse(req.WasSuccessful(m));
            Assert.AreEqual(SeafErrorCode.PathDoesNotExist, req.GetSeafError(m).SeafErrorCode);
        }

        [TestMethod]
        public void Test_MoveFileEntry_Success()
        {
            MoveFileRequest req = new MoveFileRequest(FakeToken, FakeRepoId, "/test/file.txt", FakeRepoId, "/newdir/");

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
            m.Content = new StringContent("\"success\"");

            Assert.IsTrue(req.WasSuccessful(m));
        }

        [TestMethod]
        public void Test_MoveFileEntry_Error()
        {
            MoveFileRequest req = new MoveFileRequest(FakeToken, FakeRepoId, "/test/file.txt", FakeRepoId, "/newdir/");

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.Forbidden);            
            Assert.IsFalse(req.WasSuccessful(m));

            m = new HttpResponseMessage(HttpStatusCode.BadRequest);
            Assert.IsFalse(req.WasSuccessful(m));

            // there seems to be a bug in the seafile web api
            // as the server returns NotFound even when the renaming was successful
            // so we cannot test this
            //m = new HttpResponseMessage(HttpStatusCode.NotFound);
            //Assert.IsFalse(req.WasSuccessful(m));

            m = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            Assert.IsFalse(req.WasSuccessful(m));
        }


        [TestMethod]
        public void Test_CopyFileEntry_Success()
        {
            CopyFileRequest req = new CopyFileRequest(FakeToken, FakeRepoId, "/test/file.txt", FakeRepoId, "/newdir/");

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent("\"success\"");

            Assert.IsTrue(req.WasSuccessful(m));
        }

        [TestMethod]
        public void Test_CopyFileEntry_Error()
        {
            CopyFileRequest req = new CopyFileRequest(FakeToken, FakeRepoId, "/test/file.txt", FakeRepoId, "/newdir/");

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.Forbidden);
            Assert.IsFalse(req.WasSuccessful(m));

            m = new HttpResponseMessage(HttpStatusCode.BadRequest);
            Assert.IsFalse(req.WasSuccessful(m));

            m = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            Assert.IsFalse(req.WasSuccessful(m));
        }
    }
}
