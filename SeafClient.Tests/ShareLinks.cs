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
    public class ShareLinks : SeafTestClassBase
    {
        [TestMethod]
        public void Test_GetShareLink_Success()
        {
            var request = new CreateShareLinkRequest(FakeToken, FakeRepoId, "/");

            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
	                                            ""username"": ""lian@lian.com"",
	                                            ""repo_id"": ""c474a093-19dc-4ddf-b0b0-72b33214ba33"",
	                                            ""ctime"": ""2017-04-01T02:35:57+00:00"",
	                                            ""expire_date"": """",
	                                            ""token"": ""6afa667ff2c248378b70"",
	                                            ""view_cnt"": 0,
	                                            ""link"": ""https://cloud.seafile.com/d/6afa667ff2c248378b70/"",
	                                            ""obj_name"": ""/"",
	                                            ""path"": ""/"",
	                                            ""is_dir"": true,
	                                            ""permissions"": {
		                                            ""can_edit"": false,
		                                            ""can_download"": true
	                                            },
	                                            ""is_expired"": false,
	                                            ""repo_name"": ""seacloud.cc.124""
                                            }]")
            };

            Assert.IsTrue(request.WasSuccessful(message));
            var result = ExecuteSync(() => request.ParseResponseAsync(message));

            Assert.AreEqual("/", result.Name);

            //Assert.AreEqual(FakeRepoId, result.LibraryId);
            //Assert.AreEqual("/test/subfolder/foo.py", result.Path);
            
            Assert.AreEqual("6afa667ff2c248378b70", result.Id);

            Assert.AreEqual(true, result.IsDirectory);
            //Assert.AreEqual(22, result.Size);
            //// converted the timestamp 1398148877 using http://www.onlineconversion.com/unix_time.htm
            //// note: comparison is done in local time
            //Assert.AreEqual(DateTime.Parse("Tue, 22 Apr 2014 06:41:17 GMT", CultureInfo.InvariantCulture), result.Timestamp);
        }
    }
}
