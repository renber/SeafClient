using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeafClient.Requests;
using System.Net.Http;
using System.Net;
using SeafClient.Types;
using System.Collections.Generic;
using System.Globalization;
using SeafClient.Requests.Libraries;

namespace SeafClient.Tests
{
    [TestClass]
    public class LibraryTests : SeafTestClassBase
    {
        [TestMethod]
        public void Test_GetDefaultLibrary_Success()
        {
            GetDefaultLibraryRequest req = new GetDefaultLibraryRequest(FakeToken);

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent(
                @"{
                   ""repo_id"": ""691b3e24-d05e-43cd-a9f2-6f32bd6b800e"",
                   ""exists"": true
                }");

            Assert.IsTrue(req.WasSuccessful(m));
            SimpleLibraryResult result = ExecuteSync(() => req.ParseResponseAsync(m));
            Assert.IsTrue(result.Exists);
            Assert.AreEqual("691b3e24-d05e-43cd-a9f2-6f32bd6b800e", result.LibraryId);
        }

        [TestMethod]
        public void Test_GetLibraryInfo_Success()
        {
            GetLibraryInfoRequest req = new GetLibraryInfoRequest(FakeToken, FakeRepoId);

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent(
                @"{
                    ""encrypted"": false,
                    ""password_need"": null,
                    ""mtime"": null,
                    ""owner"": ""self"",
                    ""id"": ""632ab8a8-ecf9-4435-93bf-f495d5bfe975"",
                    ""size"": 1356155,
                    ""name"": ""org"",
                    ""root"": ""b5227040de360dd22c5717f9563628fe5510cbce"",
                    ""desc"": ""org file"",
                    ""type"": ""repo""
                }");

            Assert.IsTrue(req.WasSuccessful(m));
            SeafLibrary lib = ExecuteSync(() => req.ParseResponseAsync(m));

            Assert.AreEqual("632ab8a8-ecf9-4435-93bf-f495d5bfe975", lib.Id);
            Assert.AreEqual(null, lib.Timestamp);
        }

        [TestMethod]
        public void Test_ListLibraries_HttpRequest()
        {
            ListLibrariesRequest req = new ListLibrariesRequest(FakeToken);

            var httpReq = TestConnection.CreateHttpRequestMessage(DummyServerUri, req);

            Assert.AreEqual(HttpMethod.Get, httpReq.Method);
            Assert.AreEqual(DummyServerUri + "api2/repos/", httpReq.RequestUri.ToString());
        }

        [TestMethod]
        public void Test_ListLibraries_Success()
        {
            ListLibrariesRequest req = new ListLibrariesRequest(FakeToken);

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent(
            @"[{
                ""permission"": ""r"",
                ""encrypted"": false,
                ""mtime"": 1400054900,
                ""owner"": ""user@mail.com"",
                ""id"": ""f158d1dd-cc19-412c-b143-2ac83f352290"",
                ""size"": 0,
                ""name"": ""foo"",
                ""type"": ""repo"",
                ""virtual"": false,
                ""desc"": ""new library"",
                ""root"": ""0000000000000000000000000000000000000000""
            },{
                ""permission"": ""rw"",
                ""encrypted"": false,
                ""mtime"": 1400054802,
                ""owner"": ""user@mail.com"",
                ""id"": ""0536b11a-a5fd-4482-9314-728cb3472f54"",
                ""size"": 0,
                ""name"": ""foo"",
                ""type"": ""repo"",
                ""virtual"": false,
                ""desc"": ""new library"",
                ""root"": ""0000000000000000000000000000000000000000""
            }]");

            Assert.IsTrue(req.WasSuccessful(m));
            IList<SeafLibrary> result = ExecuteSync(() => req.ParseResponseAsync(m));
            Assert.IsNotNull(result);            
            Assert.AreEqual(2, result.Count);            
            Assert.AreEqual("foo", result[0].Name);
            Assert.AreEqual("0536b11a-a5fd-4482-9314-728cb3472f54", result[1].Id);
            Assert.AreEqual(SeafPermission.ReadOnly, result[0].Permission);
            Assert.AreEqual(SeafPermission.ReadAndWrite, result[1].Permission);
            // converted the timestamp 1400054900 using http://www.onlineconversion.com/unix_time.htm
            // note: comparison is done in local time
            Assert.AreEqual(DateTime.Parse("Wed, 14 May 2014 08:08:20 GMT", CultureInfo.InvariantCulture), result[0].Timestamp);
        }

        [TestMethod]
        public void Test_ListSharedLibraries_HttpRequest()
        {
            ListSharedLibrariesRequest req = new ListSharedLibrariesRequest(FakeToken);

            var httpReq = TestConnection.CreateHttpRequestMessage(DummyServerUri, req);

            Assert.AreEqual(HttpMethod.Get, httpReq.Method);
            Assert.AreEqual(DummyServerUri + "api2/shared-repos/", httpReq.RequestUri.ToString());
        }


        [TestMethod]
        public void Test_ListSharedLibraries_Success()
        {
            ListSharedLibrariesRequest req = new ListSharedLibrariesRequest(FakeToken);

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent(@"[{ ""repo_id"": ""7d42522b-1f6f-465d-b9c9-879f8eed7c6c"", ""share_type"": ""personal"", ""permission"": ""r"", ""encrypted"": false, ""user"": ""user@example.com"", ""last_modified"": 1361072500, ""repo_desc"": ""ff"", ""group_id"": 0, ""repo_name"": ""\u6d4b\u8bd5\u4e2d\u6587pdf""},
                                             { ""repo_id"": ""79bb29cd-b683-4844-abaf-433952723ca5"", ""share_type"": ""group"", ""permission"": ""rw"", ""encrypted"": false, ""user"": ""user@example.com"", ""last_modified"": 1359182468, ""repo_desc"": ""test"", ""group_id"": 1, ""repo_name"": ""test_enc""}]");

            Assert.IsTrue(req.WasSuccessful(m));
            IList<SeafSharedLibrary> result = ExecuteSync(() => req.ParseResponseAsync(m));
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("user@example.com", result[0].Owner);
            Assert.AreEqual("\u6d4b\u8bd5\u4e2d\u6587pdf", result[0].Name);
            Assert.AreEqual("79bb29cd-b683-4844-abaf-433952723ca5", result[1].Id);
            Assert.AreEqual(SeafPermission.ReadOnly, result[0].Permission);
            Assert.AreEqual(SeafPermission.ReadAndWrite, result[1].Permission);
            // converted the timestamp 1400054900 using http://www.onlineconversion.com/unix_time.htm
            // note: comparison is done in local time
            Assert.AreEqual(DateTime.Parse("Sun, 17 Feb 2013 03:41:40 GMT", CultureInfo.InvariantCulture), result[0].Timestamp);
        }
    }
}
