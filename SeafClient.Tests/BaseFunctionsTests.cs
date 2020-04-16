using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeafClient.Requests;
using SeafClient.Requests.UserAccountInfo;
using SeafClient.Types;

namespace SeafClient.Tests
{
    // Tests have been created based on the Seafile web api documentation
    // see http://manual.seafile.com/develop/web_api.html

    [TestClass]
    public class BaseFunctionsTests : SeafTestClassBase
    {
        [TestMethod]
        public void Test_Authentication_HttpRequest()
        {
            // in actual production code you should SecureString to retrieve the char array from a password
            // do not store passwords in a standard string
            var pwd = "mypw".ToCharArray();
            var request = new AuthRequest("TestUser@test.com", pwd);

            // check the created http request message
            var httpRequest = TestConnection.CreateHttpRequestMessage(DummyServerUri, request);

            Assert.AreEqual(HttpMethod.Post, httpRequest.Method);
            Assert.AreEqual(DummyServerUri + "api2/auth-token/", httpRequest.RequestUri.ToString());
            var postContent = ExecuteSync(() => httpRequest.Content.ReadAsStringAsync());
            Assert.AreEqual("username=" + WebUtility.UrlEncode("TestUser@test.com") + "&password=mypw", postContent);

            // ensure that the password array has been cleared
            var nullArray = new char[4];
            Array.Clear(nullArray, 0, 4);
            Assert.IsTrue(nullArray.SequenceEqual(pwd));
        }

        [TestMethod]
        public void Test_Authentication_Success()
        {
            var request = new AuthRequest("", new char[0]);

            // test sample response message            
            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ \"token\": \"" + FakeToken + "\" }")
            };

            Assert.IsTrue(request.WasSuccessful(message));
            var result = ExecuteSync(() => request.ParseResponseAsync(message));
            Assert.IsNotNull(result);
            Assert.AreEqual(FakeToken, result.Token);
        }

        [TestMethod]
        public void Test_Authentication_Error()
        {
            var request = new AuthRequest("", new char[0]);

            // test sample response message            
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest);

            Assert.IsFalse(request.WasSuccessful(message));
            Assert.AreEqual(SeafErrorCode.InvalidCredentials, request.GetSeafError(message).SeafErrorCode);
        }

        [TestMethod]
        public void Test_AccountInfo_HttpRequest()
        {
            var request = new AccountInfoRequest(FakeToken);
            var httpRequest = TestConnection.CreateHttpRequestMessage(DummyServerUri, request);

            Assert.AreEqual(HttpMethod.Get, httpRequest.Method);
            Assert.AreEqual(DummyServerUri + "api2/account/info/", httpRequest.RequestUri.ToString());
        }

        [TestMethod]
        public void Test_AccountInfo_Success()
        {
            var req = new AccountInfoRequest(FakeToken);

            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"usage\": 26038531,\"total\": 104857600,\"email\": \"user@example.com\"}")
            };

            Assert.IsTrue(req.WasSuccessful(message));
            var result = ExecuteSync(() => req.ParseResponseAsync(message));
            Assert.IsNotNull(result);
            Assert.IsFalse(result.HasUnlimitedSpace);
            Assert.AreEqual(26038531, result.Usage);
            Assert.AreEqual(104857600, result.Quota);
            Assert.AreEqual("user@example.com", result.Email);
        }

        [TestMethod]
        public void Test_AccountInfo_Error()
        {
            var request = new AccountInfoRequest("");
            var message = new HttpResponseMessage(HttpStatusCode.Forbidden);

            Assert.IsFalse(request.WasSuccessful(message));
            Assert.AreEqual(SeafErrorCode.InvalidToken, request.GetSeafError(message).SeafErrorCode);
        }

        [TestMethod]
        public void Test_GetUserAvatar_HttpRequest()
        {
            var request = new UserAvatarRequest(FakeToken, "user@mail.com", 112);
            var httpRequest = TestConnection.CreateHttpRequestMessage(DummyServerUri, request);

            Assert.AreEqual(HttpMethod.Get, httpRequest.Method);
            Assert.AreEqual(DummyServerUri + "api2/avatars/user/user@mail.com/resized/112/", httpRequest.RequestUri.ToString());
        }

        [TestMethod]
        public void Test_GetUserAvatar_Success()
        {
            var request = new UserAvatarRequest(FakeToken, "user@mail.com", 112);

            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                        ""url"": ""http://127.0.0.1:8000/media/avatars/default.png"",
                        ""is_default"": true,
                        ""mtime"": 1311012500}")
            };

            Assert.IsTrue(request.WasSuccessful(message));
            var result = ExecuteSync(() => request.ParseResponseAsync(message));
            Assert.AreEqual("http://127.0.0.1:8000/media/avatars/default.png", result.Url);
            Assert.IsTrue(result.IsDefault);
            Assert.AreEqual(DateTime.Parse("Mon, 18 Jul 2011 18:08:20 GMT", CultureInfo.InvariantCulture), result.Timestamp);
        }

        [TestMethod]
        public void Test_GetUserAvatar_Error()
        {
            var request = new AccountInfoRequest("");
            var message = new HttpResponseMessage(HttpStatusCode.Forbidden);

            Assert.IsFalse(request.WasSuccessful(message));
        }

        [TestMethod]
        public void Test_SessionFromToken()
        {
            var serverInfoMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                                            ""version"": ""4.0.6"",
                                            ""features"": [
                                            ""seafile-basic"",
                                            ]}")
            };

            // From Token automatically checks the token using the CheckAccountInfo() command
            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"usage\": 26038531,\"total\": 104857600,\"email\": \"user@example.com\"}")
            };

            var mockedConnection = new MockedSeafConnection();
            mockedConnection.FakeResponseFor<GetServerInfoRequest>(serverInfoMessage);
            mockedConnection.FakeResponseFor<AccountInfoRequest>(message);            
            var session = ExecuteSync(() => SeafSession.FromToken(mockedConnection, new Uri("http://www.example.com"), FakeToken));

            Assert.IsNotNull(session);
            Assert.AreEqual(FakeToken, session.AuthToken);
            Assert.AreEqual("user@example.com", session.Username);
        }

        [TestMethod]
        public void Test_SessionFromUsernameAndToken()
        {
            var session = SeafSession.FromToken(new Uri("http://www.example.com"), "user@example.com", FakeToken, FakeServerVersion);

            Assert.IsNotNull(session);
            Assert.AreEqual(FakeToken, session.AuthToken);
            Assert.AreEqual("user@example.com", session.Username);
        }

        [TestMethod]
        public void Test_Sessionless_Ping()
        {
            var connection = new MockedSeafConnection();
            var message = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"pong\"") };
            connection.FakeResponseFor<PingRequest>(message);

            var result = ExecuteSync(() => SeafSession.Ping(connection, new Uri("http://www.example.com")));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Test_GetServerInfo()
        {
            var connection = new MockedSeafConnection();
            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                                            ""version"": ""4.0.6"",
                                            ""features"": [
                                            ""seafile-basic"",
                                            ]}")
            };

            connection.FakeResponseFor<GetServerInfoRequest>(message);

            var serverInfo = ExecuteSync(() => SeafSession.GetServerInfo(connection, new Uri("http://www.example.com")));

            Assert.AreEqual("4.0.6", serverInfo.Version);
            Assert.IsTrue(serverInfo.Features.Contains("seafile-basic"));
        }
    }
}