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

        [TestMethod]
        public void Test_BulkAddGroupMember_Success()
        {
            var request = new BulkAddGroupMemberRequest(FakeToken, 42, new List<string> { "new-member-1@email.com", "new-member-2@email.com","new-member-3@email.com", "new-member-4@email.com" });

            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
            @"{
            ""failed"":[
            {
                    ""error_msg"": ""Invalid email"",
                ""email"": ""new-member-3@email.com""
            },
            {
                    ""error_msg"": ""Is already group member"",
                ""email"": ""new-member-4@email.com""
            }],
             ""success"":[
                    {
                ""login_id"": """",
                        ""name"": ""new-member-1"",
                        ""avatar_url"": ""https://cloud.seafile.com/media/avatars/default.png"",
                        ""is_admin"": false,
                        ""contact_email"": ""new-member-1@email.com"",
                        ""email"": ""new-member-1@email.com""
            },
            {
                ""login_id"": """",
                ""name"": ""new-member-2"",
                ""avatar_url"": ""https://cloud.seafile.com/media/avatars/default.png"",
                ""is_admin"": false,
                ""contact_email"": ""new-member-2@email.com"",
                ""email"": ""new-member-2@email.com""
            }]}")
                };

            Assert.IsTrue(request.WasSuccessful(message));
            var result = ExecuteSync(() => request.ParseResponseAsync(message));

            Assert.AreEqual(2, result.Failed.Length);
            Assert.AreEqual(2, result.Success.Length);

            Assert.AreEqual("new-member-3@email.com", result.Failed.First().Email);
            Assert.AreEqual("Invalid email", result.Failed.First().ErrorMessage);

            Assert.AreEqual("new-member-2@email.com", result.Success.Skip(1).First().Email);
            Assert.AreEqual("new-member-2", result.Success.Skip(1).First().Name);
        }
    }
}
