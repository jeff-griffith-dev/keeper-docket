using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Docket.Tests.Contract;

/// <summary>
/// Base class for all contract tests. Provides HTTP helpers and
/// a shared HttpClient against the ContractTestFactory.
/// </summary>
[Collection("ContractTests")]
public abstract class ContractTestBase
{
    protected readonly HttpClient Client;

    protected static readonly Guid UnknownId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected ContractTestBase(ContractTestFactory factory)
    {
        Client = factory.CreateClient();
    }

    protected async Task<(HttpResponseMessage Response, T? Body)> GetAsync<T>(string url)
    {
        var response = await Client.GetAsync(url);
        T? body = default;
        if (response.IsSuccessStatusCode)
            body = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return (response, body);
    }

    protected async Task<HttpResponseMessage> PostAsync(string url, object body)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await Client.PostAsync(url, content);
    }

    protected async Task<(HttpResponseMessage Response, T? Body)> PostAsync<T>(string url, object body)
    {
        var response = await PostAsync(url, body);
        T? result = default;
        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return (response, result);
    }

    protected async Task<HttpResponseMessage> PatchAsync(string url, object body)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await Client.PatchAsync(url, content);
    }

    protected async Task<HttpResponseMessage> DeleteAsync(string url)
        => await Client.DeleteAsync(url);

    protected void ShouldBeSuccess(HttpResponseMessage response, string because = "")
        => response.IsSuccessStatusCode.Should().BeTrue(
            string.IsNullOrEmpty(because)
                ? $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} returned {(int)response.StatusCode}"
                : because);

    protected void ShouldBe(HttpResponseMessage response, HttpStatusCode expected, string because = "")
        => response.StatusCode.Should().Be(expected, because);
}

/// <summary>
/// xUnit collection fixture — all contract test classes share one factory instance,
/// which means one database for the entire contract suite.
/// </summary>
[CollectionDefinition("ContractTests")]
public class ContractTestCollection : ICollectionFixture<ContractTestFactory> { }
