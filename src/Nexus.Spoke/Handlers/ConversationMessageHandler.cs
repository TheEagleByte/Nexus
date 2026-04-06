using System.Text.Json;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Handlers;

public class ConversationMessageHandler(
    IConversationRunner conversationRunner,
    IHubConnectionService hubConnection,
    ILogger<ConversationMessageHandler> logger) : ICommandHandler
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string CommandType => "conversation.message";

    public async Task HandleAsync(CommandEnvelope command, CancellationToken cancellationToken)
    {
        var message = DeserializePayload(command.Payload);
        if (message is null)
        {
            logger.LogError("Failed to deserialize ConversationUserMessage from command payload");
            return;
        }

        logger.LogInformation("Handling conversation message for conversation {ConversationId}",
            message.ConversationId);

        try
        {
            // Use conversationId as the CC session resume identifier
            var ccSessionId = message.ConversationId.ToString();
            var response = await conversationRunner.InvokeAsync(ccSessionId, message.Content, null, cancellationToken);

            // Mirror response back to hub
            await hubConnection.SendAsync("MessageFromSpokeConversation",
                new ConversationSpokeMessage(message.ConversationId, response, DateTimeOffset.UtcNow),
                cancellationToken);

            logger.LogInformation("Conversation response sent for {ConversationId} ({Length} chars)",
                message.ConversationId, response.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process conversation message for {ConversationId}",
                message.ConversationId);

            // Send error message back to hub as assistant response
            await hubConnection.SendAsync("MessageFromSpokeConversation",
                new ConversationSpokeMessage(
                    message.ConversationId,
                    $"[Error] Failed to process message: {ex.Message}",
                    DateTimeOffset.UtcNow),
                CancellationToken.None);
        }
    }

    private static ConversationUserMessage? DeserializePayload(object payload)
    {
        if (payload is ConversationUserMessage msg)
            return msg;

        if (payload is JsonElement element)
            return JsonSerializer.Deserialize<ConversationUserMessage>(element.GetRawText(), DeserializeOptions);

        var json = JsonSerializer.Serialize(payload);
        return JsonSerializer.Deserialize<ConversationUserMessage>(json, DeserializeOptions);
    }
}
