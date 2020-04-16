using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeafClient.Requests.Files;
using SeafClient.Types;

namespace SeafClient.Tests
{
    [TestClass]
    public class FileTests : SeafTestClassBase
    {
        [TestMethod]
        public void Test_GetFileDetail_Success()
        {
            var request = new GetFileDetailRequest(FakeToken, FakeRepoId, "/test/subfolder/foo.py");

            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                                            ""id"": ""013d3d38fed38b3e8e26b21bb3463eab6831194f"",
                                            ""mtime"": 1398148877,
                                            ""type"": ""file"",
                                            ""name"": ""foo.py"",
                                            ""size"": 22
                                            }]")
            };

            Assert.IsTrue(request.WasSuccessful(message));
            var result = ExecuteSync(() => request.ParseResponseAsync(message));

            Assert.AreEqual("foo.py", result.Name);
            Assert.AreEqual(FakeRepoId, result.LibraryId);
            Assert.AreEqual("/test/subfolder/foo.py", result.Path);
            Assert.AreEqual("013d3d38fed38b3e8e26b21bb3463eab6831194f", result.Id);
            Assert.AreEqual(DirEntryType.File, result.Type);
            Assert.AreEqual(22, result.Size);
            // converted the timestamp 1398148877 using http://www.onlineconversion.com/unix_time.htm
            // note: comparison is done in local time
            Assert.AreEqual(DateTime.Parse("Tue, 22 Apr 2014 06:41:17 GMT", CultureInfo.InvariantCulture), result.Timestamp);
        }
    }
}