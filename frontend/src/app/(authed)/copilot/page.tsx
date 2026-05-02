"use client";

import { Suspense, useEffect, useRef } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { observer } from "mobx-react-lite";
import { TopBar } from "@/components/TopBar";
import { Copilot } from "@/components/Copilot";
import { ConversationsSidebar } from "@/components/ConversationsSidebar";
import { useChatStore } from "@/lib/stores/StoreProvider";

export default function CopilotPage() {
  return (
    <>
      <TopBar
        title="Copilot"
        sub="Natural language interface · sessions persisted to Postgres · auto-resume on refresh"
      />
      <Suspense fallback={null}>
        <CopilotPageBody />
      </Suspense>
    </>
  );
}

const CopilotPageBody = observer(function CopilotPageBody() {
  return (
    <>
      <PrefilledQueryHandler />
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "260px 1fr",
          flex: 1,
          minHeight: 0,
          height: "100%",
        }}
      >
        <ConversationsSidebar />
        <div style={{ display: "flex", flexDirection: "column", minHeight: 0 }}>
          <Copilot />
        </div>
      </div>
    </>
  );
});

// Sends a preset query when the user lands here from /map or /alerts via ?q=.
// Strips the param after sending so a refresh doesn't re-fire it. Guarded by
// a ref so React StrictMode's double-effect can't double-send.
function PrefilledQueryHandler() {
  const router = useRouter();
  const params = useSearchParams();
  const chat = useChatStore();
  const sentRef = useRef<string | null>(null);

  useEffect(() => {
    const q = params.get("q");
    if (!q || !chat.hasHydrated || chat.sending) return;
    if (sentRef.current === q) return;
    sentRef.current = q;
    void chat.ask(q);
    router.replace("/copilot");
  }, [params, chat, router]);

  return null;
}
