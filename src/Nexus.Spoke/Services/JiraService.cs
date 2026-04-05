using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class JiraService(
    IHttpClientFactory httpClientFactory,
    IOptions<SpokeConfiguration> config,
    ILogger<JiraService> logger) : IJiraService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<TicketMetadata?> FetchTicketAsync(string ticketKey, CancellationToken cancellationToken = default)
    {
        var jiraConfig = config.Value.Jira;
        if (string.IsNullOrWhiteSpace(jiraConfig.InstanceUrl) || string.IsNullOrWhiteSpace(jiraConfig.Token))
        {
            logger.LogWarning("Jira not configured — skipping ticket fetch for {Key}", ticketKey);
            return null;
        }

        var baseUrl = jiraConfig.InstanceUrl.TrimEnd('/');
        var url = $"{baseUrl}/rest/api/3/issue/{ticketKey}?fields=summary,description,issuetype,labels,assignee";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{jiraConfig.Email}:{jiraConfig.Token}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to connect to Jira at {Url}", baseUrl);
            throw;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogWarning("Jira ticket {Key} not found", ticketKey);
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
        {
            logger.LogError("Jira authentication failed for {Key} (HTTP {Status})", ticketKey, response.StatusCode);
            throw new UnauthorizedAccessException($"Jira authentication failed (HTTP {(int)response.StatusCode}).");
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var issue = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        var fields = issue.GetProperty("fields");

        var summary = fields.GetProperty("summary").GetString() ?? ticketKey;
        var description = ExtractDescription(fields);
        var acceptanceCriteria = ExtractAcceptanceCriteria(description);
        var issueType = fields.TryGetProperty("issuetype", out var it)
            ? it.GetProperty("name").GetString()
            : null;
        var labels = fields.TryGetProperty("labels", out var lbl)
            ? lbl.EnumerateArray().Select(l => l.GetString()!).ToArray()
            : null;
        var assignee = fields.TryGetProperty("assignee", out var asg) && asg.ValueKind != JsonValueKind.Null
            ? asg.GetProperty("displayName").GetString()
            : null;

        var ticket = new TicketMetadata(ticketKey, summary, description, acceptanceCriteria, issueType, labels, assignee);

        logger.LogInformation("Fetched Jira ticket {Key}: {Summary}", ticketKey, summary);
        return ticket;
    }

    private static string? ExtractDescription(JsonElement fields)
    {
        if (!fields.TryGetProperty("description", out var desc) || desc.ValueKind == JsonValueKind.Null)
            return null;

        // Jira API v3 returns ADF; extract text content recursively
        return ExtractTextFromAdf(desc);
    }

    private static string ExtractTextFromAdf(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? string.Empty;

        var sb = new StringBuilder();

        if (element.TryGetProperty("text", out var text))
            sb.Append(text.GetString());

        if (element.TryGetProperty("content", out var content))
        {
            foreach (var child in content.EnumerateArray())
            {
                var childText = ExtractTextFromAdf(child);
                if (!string.IsNullOrEmpty(childText))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(childText);
                }
            }
        }

        return sb.ToString();
    }

    private static string[]? ExtractAcceptanceCriteria(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;

        var lines = description.Split('\n');
        var criteria = new List<string>();
        var inAcSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("## Acceptance Criteria", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("**Acceptance Criteria**", StringComparison.OrdinalIgnoreCase))
            {
                inAcSection = true;
                continue;
            }

            if (inAcSection && trimmed.StartsWith("##"))
                break;

            if (inAcSection && (trimmed.StartsWith("- [ ]") || trimmed.StartsWith("- [x]") || trimmed.StartsWith("- ")))
            {
                var criterion = trimmed
                    .TrimStart('-', ' ')
                    .TrimStart('[', ' ', ']', 'x')
                    .Trim();
                if (!string.IsNullOrEmpty(criterion))
                    criteria.Add(criterion);
            }
        }

        return criteria.Count > 0 ? criteria.ToArray() : null;
    }
}
