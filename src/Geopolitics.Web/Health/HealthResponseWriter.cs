using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Geopolitics.Web.Health;

/// <summary>
/// Writes /health as a short JSON summary: overall status plus each check's status and description.
/// Exception details are deliberately left out, since the endpoint is public.
/// </summary>
public static class HealthResponseWriter
{
    // Relaxed escaping: the response is application/json, never embedded in HTML, so apostrophes stay readable.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(JsonSerializer.Serialize(
            new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                }),
            },
            Options));
    }
}
