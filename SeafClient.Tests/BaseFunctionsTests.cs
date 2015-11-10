using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeafClient.Requests;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Text;
using SeafClient.Types;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
            char[] pwdArray = "mypw".ToCharArray();
            AuthRequest req = new AuthRequest("TestUser@test.com", pwdArray);

            // check the created http request message
            var httpReq = TestConnection.CreateHttpRequestMessage(DummyServerUri, req);

            Assert.AreEqual(HttpMethod.Post, httpReq.Method);
            Assert.AreEqual(DummyServerUri + "api2/auth-token/", httpReq.RequestUri.ToString());
            string postContent = ExecuteSync(() => httpReq.Content.ReadAsStringAsync());
            Assert.AreEqual("username=" + WebUtility.UrlEncode("TestUser@test.com") + "&password=mypw", postContent);

            // ensure that the password array has been cleared
            char[] nullArray = new char[4];
            Array.Clear(nullArray, 0, 4);
            Assert.IsTrue(nullArray.SequenceEqual(pwdArray));
        }

        [TestMethod]
        public void Test_Authentication_Success()
        {
            AuthRequest req = new AuthRequest("", new char[0]);

            // test sample response message            
            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent("{ \"token\": \"" + FakeToken + "\" }");

            Assert.IsTrue(req.WasSuccessful(m));
            AuthResponse result = ExecuteSync(() => req.ParseResponseAsync(m));
            Assert.IsNotNull(result);
            Assert.AreEqual(FakeToken, result.Token);
        }

        [TestMethod]
        public void Test_Authentication_Error()
        {
            AuthRequest req = new AuthRequest("", new char[0]);

            // test sample response message            
            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.BadRequest);            

            Assert.IsFalse(req.WasSuccessful(m));            
            Assert.AreEqual(SeafErrorCode.InvalidCredentials, req.GetSeafError(m).SeafErrorCode);
        }

        [TestMethod]
        public void Test_AccountInfo_HttpRequest()
        {
            AccountInfoRequest req = new AccountInfoRequest(FakeToken);

            var httpReq = TestConnection.CreateHttpRequestMessage(DummyServerUri, req);

            Assert.AreEqual(HttpMethod.Get, httpReq.Method);
            Assert.AreEqual(DummyServerUri + "api2/account/info/", httpReq.RequestUri.ToString());
        }

        [TestMethod]
        public void Test_AccountInfo_Success()
        {
            AccountInfoRequest req = new AccountInfoRequest(FakeToken);

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent("{\"usage\": 26038531,\"total\": 104857600,\"email\": \"user@example.com\"}");

            Assert.IsTrue(req.WasSuccessful(m));
            AccountInfo result = ExecuteSync(() => req.ParseResponseAsync(m));
            Assert.IsNotNull(result);
            Assert.IsFalse(result.HasUnlimitedSpace);
            Assert.AreEqual(26038531, result.Usage);
            Assert.AreEqual(104857600, result.Quota);
            Assert.AreEqual("user@example.com", result.Email);
        }

        [TestMethod]
        public void Test_AccountInfo_Error()
        { 
            AccountInfoRequest req = new AccountInfoRequest("");

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.Forbidden);

            Assert.IsFalse(req.WasSuccessful(m));
            Assert.AreEqual(SeafErrorCode.InvalidToken, req.GetSeafError(m).SeafErrorCode);
        }

        [TestMethod]
        public void Test_GetUserAvatar_HttpRequest()
        {
            UserAvatarRequest req = new UserAvatarRequest(FakeToken, "user@mail.com", 112);

            var httpReq = TestConnection.CreateHttpRequestMessage(DummyServerUri, req);

            Assert.AreEqual(HttpMethod.Get, httpReq.Method);
            Assert.AreEqual(DummyServerUri + "api2/avatars/user/user@mail.com/resized/112/", httpReq.RequestUri.ToString());
        }

        [TestMethod]
        public void Test_GetUserAvatar_Success()
        {
            UserAvatarRequest req = new UserAvatarRequest(FakeToken, "user@mail.com", 112);

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent(@"{
                        ""url"": ""http://127.0.0.1:8000/media/avatars/default.png"",
                        ""is_default"": true,
                        ""mtime"": 1311012500}");

            Assert.IsTrue(req.WasSuccessful(m));
            var r = ExecuteSync(() => req.ParseResponseAsync(m));
            Assert.AreEqual("http://127.0.0.1:8000/media/avatars/default.png", r.Url);
            Assert.IsTrue(r.IsDefault);
            Assert.AreEqual(DateTime.Parse("Mon, 18 Jul 2011 18:08:20 GMT", CultureInfo.InvariantCulture), r.Timestamp);
        }

        [TestMethod]
        public void Test_GetUserAvatar_Error()
        {
            AccountInfoRequest req = new AccountInfoRequest("");

            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.Forbidden);

            Assert.IsFalse(req.WasSuccessful(m));            
        }

        [TestMethod]
        public void Test_SessionFromToken()
        {
            // From Token automatically checks the token using the CheckAccountInfo() command
            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent("{\"usage\": 26038531,\"total\": 104857600,\"email\": \"user@example.com\"}");
            var mockedConnection = new MockedSeafConnection();
            mockedConnection.FakeResponseFor<AccountInfoRequest>(m);
            var session = ExecuteSync(() => SeafSession.FromToken(mockedConnection, new Uri("http://www.example.com"), FakeToken));            

            Assert.IsNotNull(session);
            Assert.AreEqual(FakeToken, session.AuthToken);
            Assert.AreEqual("user@example.com", session.Username);
        }

        [TestMethod]
        public void Test_SessionFromUsernameAndToken()
        {                        
            var session = SeafSession.FromToken(new Uri("http://www.example.com"), "user@example.com", FakeToken);

            Assert.IsNotNull(session);
            Assert.AreEqual(FakeToken, session.AuthToken);
            Assert.AreEqual("user@example.com", session.Username);
        }

        [TestMethod]
        public void Test_Sessionless_Ping()
        {
            var c = new MockedSeafConnection();
            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent("\"pong\"");
            c.FakeResponseFor<PingRequest>(m);

            var b = ExecuteSync( () => SeafSession.Ping(c, new Uri("http://www.example.com")));

            Assert.IsTrue(b);            
        }

        [TestMethod]
        public void Test_GetServerInfo()
        {
            var c = new MockedSeafConnection();
            HttpResponseMessage m = new HttpResponseMessage(HttpStatusCode.OK);
            m.Content = new StringContent(@"{
                                            ""version"": ""4.0.6"",
                                            ""features"": [
                                            ""seafile-basic"",
                                            ]}");
            c.FakeResponseFor<GetServerInfoRequest>(m);

            var sInfo = ExecuteSync(() => SeafSession.GetServerInfo(c, new Uri("http://www.example.com")));

            Assert.AreEqual("4.0.6", sInfo.Version);
            Assert.IsTrue(sInfo.Features.Contains("seafile-basic"));
        }
    }
}
