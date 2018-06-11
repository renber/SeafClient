using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeafClient.Requests.Groups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Tests
{
    [TestClass]
    public class GroupTests : SeafTestClassBase
    {
        [TestMethod]
        public void Test_ListGroups_Success()
        {
            var request = new ListGroupsRequest(FakeToken);

            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    @"{
                        ""replynum"": 0,
                        ""groups"": [
                            {
                                ""ctime"": 1398134171327948,
                                ""creator"": ""user@example.com"",
                                ""msgnum"": 0,
                                ""mtime"": 1398231100,
                                ""id"": 1,
                                ""name"": ""lian""
                            },
                            {
                                ""ctime"": 1398134171327948,
                                ""creator"": ""user@example.com"",
                                ""msgnum"": 0,
                                ""mtime"": 0,
                                ""id"": 2,
                                ""name"": ""123""
                            }
                        ]
                    }")
            };

            Assert.IsTrue(request.WasSuccessful(message));
            var result = ExecuteSync(() => request.ParseResponseAsync(message));            
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result.Groups[0].Id);
            Assert.AreEqual("lian", result.Groups[0].Name);
            Assert.AreEqual(1398134171327948u, result.Groups[0].CommitTime.Value);
        }

        [TestMethod]
        public void Test_AddGroup_Success()
        {
            var request = new AddGroupRequest(FakeToken, "new group");

            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""group_id"": 3, ""success"": true}")
            };

            Assert.IsTrue(request.WasSuccessful(message));
            var result = ExecuteSync(() => request.ParseResponseAsync(message));
            Assert.AreEqual(3, result);
        }        
    }
}
