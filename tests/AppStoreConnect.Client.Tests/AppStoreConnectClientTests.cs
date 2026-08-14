using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AppStoreConnect.Client.Tests;

[TestClass]
public sealed class AppStoreConnectClientTests
{
    private static readonly DateTimeOffset CurrentTime = new (2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task GetLatestPublicTestFlightBuildAsyncReturnsOnlyBuildAvailableToExternalUsers()
    {
        var requests = new List<CapturedRequest>();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requests.Add(Capture(request));

            return request.RequestUri?.AbsolutePath switch
            {
                "/v1/apps" => JsonResponse(
                    """
                    {
                      "data": [
                        {
                          "type": "apps",
                          "id": "app-id",
                          "attributes": {
                            "bundleId": "com.example.app",
                            "name": "Example",
                            "sku": "EXAMPLE",
                            "primaryLocale": "ru"
                          }
                        }
                      ]
                    }
                    """),
                "/v1/betaGroups/public-group" => JsonResponse(
                    """
                    {
                      "data": {
                        "type": "betaGroups",
                        "id": "public-group",
                        "attributes": {
                          "name": "Public release",
                          "isInternalGroup": false,
                          "publicLinkEnabled": true
                        }
                      }
                    }
                    """),
                "/v1/builds" => JsonResponse(BuildsResponse),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }));
        using var client = CreateClient(httpClient);

        var result = await client.GetLatestPublicTestFlightBuildAsync(
            "com.example.app",
            "public-group");

        Assert.IsNotNull(result);
        Assert.AreEqual("build-public", result.Id);
        Assert.AreEqual("14.0.5", result.Version);
        Assert.AreEqual("105", result.BuildNumber);
        Assert.AreEqual("IN_BETA_TESTING", result.ExternalBuildState);
        Assert.AreEqual(3, requests.Count);
        Assert.IsTrue(Uri.UnescapeDataString(requests[0].Uri.Query)
            .Contains("filter[bundleId]=com.example.app", StringComparison.Ordinal));
        Assert.IsTrue(Uri.UnescapeDataString(requests[2].Uri.Query)
            .Contains("filter[betaGroups]=public-group", StringComparison.Ordinal));

        foreach (var request in requests)
        {
            AssertJwt(request.Authorization);
        }
    }

    [TestMethod]
    public async Task GetLatestPublicTestFlightBuildAsyncRejectsNonPublicGroup()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            request.RequestUri?.AbsolutePath switch
            {
                "/v1/apps" => JsonResponse(
                    """
                    { "data": [{ "id": "app-id", "attributes": { "bundleId": "com.example.app" } }] }
                    """),
                "/v1/betaGroups/private-group" => JsonResponse(
                    """
                    {
                      "data": {
                        "id": "private-group",
                        "attributes": {
                          "name": "Internal",
                          "isInternalGroup": true,
                          "publicLinkEnabled": false
                        }
                      }
                    }
                    """),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            }));
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLatestPublicTestFlightBuildAsync("com.example.app", "private-group"));

        StringAssert.Contains(exception.Message, "not an external group with an enabled public link");
    }

    [TestMethod]
    public async Task GetAppAsyncReturnsNullWhenApplicationDoesNotExist()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            JsonResponse("""{ "data": [] }""")));
        using var client = CreateClient(httpClient);

        var result = await client.GetAppAsync("com.example.missing");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UnsuccessfulResponseThrowsTypedApiException()
    {
        const string responseBody = """{ "errors": [{ "status": "401" }] }""";
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            JsonResponse(responseBody, HttpStatusCode.Unauthorized)));
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<AppStoreConnectApiException>(() =>
            client.GetAppAsync("com.example.app"));

        Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.AreEqual(responseBody, exception.ResponseBody);
        Assert.AreEqual("https://api.appstoreconnect.apple.com/v1/apps", exception.RequestUri?.GetLeftPart(UriPartial.Path));
    }

    [TestMethod]
    public void ConstructorRejectsTokenLifetimeAboveAppleLimit()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)));
        var options = new AppStoreConnectClientOptions
        {
            KeyId = "KEY-ID",
            IssuerId = "issuer-id",
            PrivateKey = CreatePrivateKey(),
            TokenLifetime = TimeSpan.FromMinutes(21),
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AppStoreConnectClient(httpClient, options));
    }

    /// <summary>
    /// Creates a client with deterministic credentials and time.
    /// </summary>
    /// <param name="httpClient">HTTP pipeline used by the client.</param>
    /// <returns>Configured client.</returns>
    private static AppStoreConnectClient CreateClient(HttpClient httpClient)
        => new (httpClient, CreateOptions(), new FixedTimeProvider(CurrentTime));

    /// <summary>
    /// Creates valid client options with an escaped multiline private key.
    /// </summary>
    /// <returns>Client options.</returns>
    private static AppStoreConnectClientOptions CreateOptions()
        => new ()
        {
            KeyId = "KEY-ID",
            IssuerId = "issuer-id",
            PrivateKey = CreatePrivateKey(),
        };

    /// <summary>
    /// Creates an escaped multiline private key for tests.
    /// </summary>
    /// <returns>Escaped PKCS#8 private key.</returns>
    private static string CreatePrivateKey()
    {
        using var algorithm = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return algorithm.ExportPkcs8PrivateKeyPem()
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates the authorization token generated for a request.
    /// </summary>
    /// <param name="authorization">Authorization header value.</param>
    private static void AssertJwt(string? authorization)
    {
        Assert.IsNotNull(authorization);
        StringAssert.StartsWith(authorization, "Bearer ");
        var sections = authorization["Bearer ".Length..].Split('.');
        Assert.AreEqual(3, sections.Length);

        using var header = JsonDocument.Parse(DecodeBase64Url(sections[0]));
        Assert.AreEqual("ES256", header.RootElement.GetProperty("alg").GetString());
        Assert.AreEqual("KEY-ID", header.RootElement.GetProperty("kid").GetString());

        using var payload = JsonDocument.Parse(DecodeBase64Url(sections[1]));
        Assert.AreEqual("issuer-id", payload.RootElement.GetProperty("iss").GetString());
        Assert.AreEqual("appstoreconnect-v1", payload.RootElement.GetProperty("aud").GetString());
        Assert.AreEqual(CurrentTime.ToUnixTimeSeconds(), payload.RootElement.GetProperty("iat").GetInt64());
        Assert.AreEqual(
            CurrentTime.AddMinutes(10).ToUnixTimeSeconds(),
            payload.RootElement.GetProperty("exp").GetInt64());
        Assert.AreEqual(64, DecodeBase64Url(sections[2]).Length);
    }

    /// <summary>
    /// Decodes an unpadded Base64Url value.
    /// </summary>
    /// <param name="value">Base64Url value.</param>
    /// <returns>Decoded bytes.</returns>
    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        return Convert.FromBase64String(base64);
    }

    /// <summary>
    /// Captures request data needed by assertions.
    /// </summary>
    /// <param name="request">HTTP request.</param>
    /// <returns>Immutable request snapshot.</returns>
    private static CapturedRequest Capture(HttpRequestMessage request)
        => new (
            request.RequestUri ?? throw new InvalidOperationException("Request URI is missing."),
            request.Headers.Authorization?.ToString());

    /// <summary>
    /// Creates a JSON HTTP response.
    /// </summary>
    /// <param name="content">JSON response body.</param>
    /// <param name="statusCode">HTTP status code.</param>
    /// <returns>HTTP response.</returns>
    private static HttpResponseMessage JsonResponse(
        string content,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new (statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };

    private const string BuildsResponse =
        """
        {
          "data": [
            {
              "type": "builds",
              "id": "build-approved",
              "attributes": {
                "version": "106",
                "uploadedDate": "2026-08-13T12:00:00Z",
                "expired": false,
                "processingState": "VALID"
              },
              "relationships": {
                "preReleaseVersion": { "data": { "id": "version-approved" } },
                "buildBetaDetail": { "data": { "id": "detail-approved" } }
              }
            },
            {
              "type": "builds",
              "id": "build-public",
              "attributes": {
                "version": "105",
                "uploadedDate": "2026-08-12T12:00:00Z",
                "expired": false,
                "processingState": "VALID"
              },
              "relationships": {
                "preReleaseVersion": { "data": { "id": "version-public" } },
                "buildBetaDetail": { "data": { "id": "detail-public" } }
              }
            }
          ],
          "included": [
            {
              "type": "preReleaseVersions",
              "id": "version-approved",
              "attributes": { "version": "15.0.3", "platform": "IOS" }
            },
            {
              "type": "buildBetaDetails",
              "id": "detail-approved",
              "attributes": { "externalBuildState": "BETA_APPROVED" }
            },
            {
              "type": "preReleaseVersions",
              "id": "version-public",
              "attributes": { "version": "14.0.5", "platform": "IOS" }
            },
            {
              "type": "buildBetaDetails",
              "id": "detail-public",
              "attributes": { "externalBuildState": "IN_BETA_TESTING" }
            }
          ]
        }
        """;

    private sealed record CapturedRequest(Uri Uri, string? Authorization);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responseFactory(request));
        }
    }
}
