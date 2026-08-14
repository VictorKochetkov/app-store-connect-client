# AppStoreConnect.Client

Standalone typed .NET REST client for App Store Connect and public TestFlight releases.

The library:

- creates short-lived ES256 App Store Connect API tokens;
- finds an application by bundle identifier;
- reads TestFlight beta-group settings;
- reads valid, non-expired builds distributed through a beta group;
- resolves the latest build currently available through a public TestFlight link;
- accepts an application-managed `HttpClient`, including clients configured with a corporate proxy.

## Installation

```shell
dotnet add package AppStoreConnect.Client
```

## Usage

```csharp
using AppStoreConnect.Client;

var options = new AppStoreConnectClientOptions
{
    KeyId = configuration["AppStoreConnect:KeyId"]!,
    IssuerId = configuration["AppStoreConnect:IssuerId"]!,
    PrivateKey = configuration["AppStoreConnect:PrivateKey"]!,
};

using var httpClient = httpClientFactory.CreateClient("AppStoreConnect");
using var client = new AppStoreConnectClient(httpClient, options);

var release = await client.GetLatestPublicTestFlightBuildAsync(
    bundleId: "com.example.app",
    betaGroupId: "00000000-0000-0000-0000-000000000000");

Console.WriteLine(release?.Version);
```

`PrivateKey` accepts both a multiline `.p8` value and a value containing escaped `\\n` line breaks.
The supplied `HttpClient` remains owned by the caller and is not disposed by the client.

## Build

```shell
dotnet test AppStoreConnect.Client.slnx
dotnet pack src/AppStoreConnect.Client/AppStoreConnect.Client.csproj --configuration Release
```

