﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNet.WebHooks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.WebHooks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.WebHooks
{
    public class WebHookEndToEndTests : IClassFixture<WebHookEndToEndTests.TestFixture>
    {
        private readonly TestFixture _fixture;

        public WebHookEndToEndTests(TestFixture fixture)
        {
            _fixture = fixture;
            WebHookTestFunctions.InvokeData = null;
        }

        [Fact]
        public async Task ImplicitRoute_Succeeds()
        {
            await VerifyWebHook("WebHookTestFunctions/ImplicitRoute");
        }

        [Fact]
        public async Task ExplicitRoute_Succeeds()
        {
            await VerifyWebHook("test/hook");
        }

        [Fact]
        public async Task BindToString_Succeeds()
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/BindToString", new StringContent(testData));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string body = (string)WebHookTestFunctions.InvokeData;
            Assert.Equal(testData, body);
        }

        [Fact]
        public async Task CustomResponse_Success_ReturnsExpectedResponse()
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/CustomResponse_Success", new StringContent(testData));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            WebHookContext context = (WebHookContext)WebHookTestFunctions.InvokeData;
            string result = await context.Response.Content.ReadAsStringAsync();
            Assert.Equal("Custom Response Data", result);
        }

        [Fact]
        public async Task CustomResponse_Failure_ReturnsExpectedResponses()
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/CustomResponse_Failure", new StringContent(testData));
            Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);

            WebHookContext context = (WebHookContext)WebHookTestFunctions.InvokeData;
            string result = await context.Response.Content.ReadAsStringAsync();
            Assert.Equal("Does Not Compute!", result);
        }

        [Fact]
        public async Task CustomResponse_NoResponseSet_ReturnsExpectedResponses()
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/CustomResponse_NoResponseSet", new StringContent(testData));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task BindToStream_Succeeds()
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/BindToStream", new StringContent(testData));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string body = (string)WebHookTestFunctions.InvokeData;
            Assert.Equal(testData, body);
        }

        [Fact]
        public async Task BindToPoco_Succeeds()
        {
            string testData = Guid.NewGuid().ToString();
            TestPoco poco = new TestPoco
            {
                A = Guid.NewGuid().ToString(),
                B = Guid.NewGuid().ToString()
            };
            HttpResponseMessage response = await _fixture.Client.PostAsJsonAsync("WebHookTestFunctions/BindToPoco_WithModelBinding", poco);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            object[] results = (object[])WebHookTestFunctions.InvokeData;
            TestPoco resultPoco = (TestPoco)results[0];
            Assert.Equal(poco.A, resultPoco.A);
            Assert.Equal(poco.B, resultPoco.B);

            // verify model binding
            Assert.Equal(poco.A, (string)results[1]);
            Assert.Equal(poco.B, (string)results[2]);
        }

        [Fact]
        public async Task BindToPoco_UsingFields_Succeeds()
        {
            string testData = Guid.NewGuid().ToString();
            TestPoco_Fields poco = new TestPoco_Fields
            {
                A = Guid.NewGuid().ToString(),
                B = Guid.NewGuid().ToString()
            };
            HttpResponseMessage response = await _fixture.Client.PostAsJsonAsync("WebHookTestFunctions/BindToPoco_Fields", poco);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            TestPoco_Fields resultPoco = (TestPoco_Fields)WebHookTestFunctions.InvokeData;
            Assert.Equal(poco.A, resultPoco.A);
            Assert.Equal(poco.B, resultPoco.B);
        }

        [Fact]
        public async Task BindToPoco_EmptyBody_Succeeds()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "WebHookTestFunctions/BindToPoco");
            HttpResponseMessage response = await _fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            TestPoco resultPoco = (TestPoco)WebHookTestFunctions.InvokeData;
            Assert.Null(resultPoco);
        }

        [Fact]
        public async Task BindToPoco_FromUri_Succeeds()
        {
            string a = Guid.NewGuid().ToString();
            string b = Guid.NewGuid().ToString();
            string uri = string.Format("WebHookTestFunctions/BindToPoco_FromUri?A={0}&B={1}&Foo=Bar", a, b);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri);
            HttpResponseMessage response = await _fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            TestPoco resultPoco = (TestPoco)WebHookTestFunctions.InvokeData;
            Assert.Equal(a, resultPoco.A);
            Assert.Equal(b, resultPoco.B);

            // show that casing of property names doesn't matter
            uri = string.Format("WebHookTestFunctions/BindToPoco_FromUri?a={0}&b={1}&Foo=Bar", a, b);
            request = new HttpRequestMessage(HttpMethod.Post, uri);
            response = await _fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            resultPoco = (TestPoco)WebHookTestFunctions.InvokeData;
            Assert.Equal(a, resultPoco.A);
            Assert.Equal(b, resultPoco.B);
        }

        [Fact]
        public async Task InvalidHttpMethod_ReturnsMethodNotAllowed()
        {
            HttpResponseMessage response = await _fixture.Client.GetAsync("WebHookTestFunctions/BindToString");
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }

        [Fact]
        public async Task InvalidRoute_ReturnsNotFound()
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/DNE", new StringContent(testData));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task FunctionThrows_ReturnsInternalServerError()
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/Throw", new StringContent(testData));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task InvokeNonWebHook_Succeeds()
        {
            TestPoco poco = new TestPoco
            {
                A = Guid.NewGuid().ToString(),
                B = Guid.NewGuid().ToString()
            };
            JObject body = new JObject();
            JsonSerializer serializer = new JsonSerializer();
            StringWriter sr = new StringWriter();
            serializer.Serialize(sr, poco);
            body.Add("poco", sr.ToString());
            string json = body.ToString();

            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/NonWebHook", new StringContent(json));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            TestPoco result = (TestPoco)WebHookTestFunctions.InvokeData;
            Assert.Equal(poco.A, result.A);
            Assert.Equal(poco.B, result.B);
        }

        [Fact]
        public async Task InvokeNonWebHook_InvalidBody_ReturnsInternalServerError()
        {
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/NonWebHook", new StringContent("*92 kkdlinvalid"));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task GitHubReceiverIntegration_Succeeds()
        {
            string testData = "{ \"data\": \"Test GitHub Data\" }";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "github/issues");
            request.Headers.Add("X-Hub-Signature", "sha1=e8ccc2d25c6bc5e6b522f9f230188347baa291ab");
            request.Headers.Add("User-Agent", "GitHub-Hookshot/d74c40b");
            request.Headers.Add("X-GitHub-Event", "issue_comment");
            request.Content = new StringContent(testData);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response = await _fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string result = (string)WebHookTestFunctions.InvokeData;
            Assert.Equal(testData, result);
        }

        private async Task VerifyWebHook(string route)
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync(route, new StringContent(testData));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            HttpRequestMessage request = (HttpRequestMessage)WebHookTestFunctions.InvokeData;
            string body = await request.Content.ReadAsStringAsync();
            Assert.Equal(testData, body);
        }

        [Fact]
        public async Task DashboardStringInvoke_Succeeds()
        {
            // test the Dashboard invoke path which takes an invoke string in the
            // following format
            string testData = Guid.NewGuid().ToString();
            JObject content = new JObject();
            content.Add("url", string.Format("{0}{1}", _fixture.BaseUrl, "WebHookTestFunctions/ImplicitRoute"));
            content.Add("body", testData);
            string json = content.ToString();

            var args = new { request = json };
            await _fixture.Host.CallAsync(typeof(WebHookTestFunctions).GetMethod("ImplicitRoute"), args);

            HttpRequestMessage request = (HttpRequestMessage)WebHookTestFunctions.InvokeData;
            string body = await request.Content.ReadAsStringAsync();
            Assert.Equal(testData, body);
        }

        [Fact]
        public async Task DirectInvoke_Succeeds()
        {
            string testData = Guid.NewGuid().ToString();
            HttpRequestMessage request = new HttpRequestMessage
            {
                RequestUri = new Uri(string.Format("{0}{1}", _fixture.BaseUrl, "WebHookTestFunctions/CustomResponse_Success")),
                Method = HttpMethod.Get,
                Content = new StringContent(testData)
            };
            WebHookContext webHookContext = new WebHookContext(request);

            MethodInfo method = typeof(WebHookTestFunctions).GetMethod("CustomResponse_Success");
            await _fixture.Host.CallAsync(method, new { context = webHookContext });

            HttpResponseMessage response = webHookContext.Response;
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            string body = await response.Content.ReadAsStringAsync();
            Assert.Equal("Custom Response Data", body);
        }

        public static class WebHookTestFunctions
        {
            public static object InvokeData { get; set; }

            public static void ImplicitRoute([WebHookTrigger] HttpRequestMessage request)
            {
                InvokeData = request;
            }

            public static void ExplicitRoute([WebHookTrigger("test/hook")] HttpRequestMessage request)
            {
                InvokeData = request;
            }

            public static void BindToString([WebHookTrigger] string body)
            {
                InvokeData = body;
            }

            public static void BindToStream([WebHookTrigger] Stream body)
            {
                string result = new StreamReader(body).ReadToEnd();
                InvokeData = result;
            }

            public static void BindToPoco([WebHookTrigger] TestPoco poco)
            {
                InvokeData = poco;
            }

            public static void BindToPoco_Fields([WebHookTrigger] TestPoco_Fields poco)
            {
                InvokeData = poco;
            }

            public static void BindToPoco_WithModelBinding([WebHookTrigger] TestPoco poco, string a, string b)
            {
                InvokeData = new object[] { poco, a, b };
            }

            public static void BindToPoco_FromUri([WebHookTrigger(FromUri = true)] TestPoco poco)
            {
                InvokeData = poco;
            }

            public static void NonWebHook([QueueTrigger("testqueue")] TestPoco poco)
            {
                InvokeData = poco;
            }

            public static void Throw([WebHookTrigger] HttpRequestMessage request)
            {
                throw new Exception("Kaboom!");
            }

            public static void CustomResponse_Success([WebHookTrigger] WebHookContext context)
            {
                InvokeData = context;

                context.Response = new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent("Custom Response Data")
                };
            }

            public static void CustomResponse_Failure([WebHookTrigger] WebHookContext context)
            {
                InvokeData = context;

                context.Response = new HttpResponseMessage(HttpStatusCode.NotImplemented)
                {
                    Content = new StringContent("Does Not Compute!")
                };
 
                throw new Exception("Kaboom!");
            }

            public static void CustomResponse_NoResponseSet([WebHookTrigger] WebHookContext context)
            {
                InvokeData = context;

                // intentionally don't set a response
            }

            public static void GitHubReceiverIntegration([WebHookTrigger("github/issues")] string body)
            {
                InvokeData = body;
            }
        }

        public class TestFixture : IDisposable
        {
            public TestFixture()
            {
                int testPort = 43000;

                BaseUrl = string.Format("http://localhost:{0}/", testPort);
                Client = new HttpClient();
                Client.BaseAddress = new Uri(BaseUrl);

                JobHostConfiguration config = new JobHostConfiguration
                {
                    TypeLocator = new ExplicitTypeLocator(typeof(WebHookTestFunctions))
                };

                WebHooksConfiguration webHooksConfig = new WebHooksConfiguration(testPort);
                webHooksConfig.UseReceiver<GitHubWebHookReceiver>();
                config.UseWebHooks(webHooksConfig);

                Host = new JobHost(config);
                Host.Start();
            }

            public HttpClient Client { get; private set; }

            public JobHost Host { get; private set; }

            public string BaseUrl { get; private set; }

            public void Dispose()
            {
                Host.Stop();
                Host.Dispose();
            }
        }

        public class TestPoco
        {
            public string A { get; set; }
            public string B { get; set; }
        }

        /// <summary>
        /// Demonstrate that binding to a POCO using fields (rather than properties)
        /// works. While this works, one thing that won't work is binding data coming
        /// from such types being used in other attributes. This is because currently
        /// BindingDataProvider only supports Properties.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate")]
        public class TestPoco_Fields
        {
            public string A;
            public string B;
        }
    }
}
