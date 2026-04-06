import { fetchConversation } from "@/lib/api";
import { ConversationView } from "./conversation-view";

export default async function ConversationPage({
  params,
}: {
  params: Promise<{ conversationId: string }>;
}) {
  const { conversationId } = await params;

  let conversation;
  try {
    conversation = await fetchConversation(conversationId);
  } catch {
    return (
      <div className="flex items-center justify-center h-full">
        <p className="text-muted-foreground">Conversation not found</p>
      </div>
    );
  }

  return <ConversationView initialConversation={conversation} />;
}
