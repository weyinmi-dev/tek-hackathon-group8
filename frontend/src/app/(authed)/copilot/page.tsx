"use client";

import { TopBar } from "@/components/TopBar";
import { Copilot } from "@/components/Copilot";
import { ConversationsSidebar } from "@/components/ConversationsSidebar";

export default function CopilotPage() {
  return (
    <>
      <TopBar title="Copilot" sub="Natural language interface · sessions persisted to Postgres · auto-resume on refresh" />
      <div style={{ display: "grid", gridTemplateColumns: "260px 1fr", flex: 1, minHeight: 0, height: "100%" }}>
        <ConversationsSidebar />
        <div style={{ display: "flex", flexDirection: "column", minHeight: 0 }}>
          <Copilot />
        </div>
      </div>
    </>
  );
}
