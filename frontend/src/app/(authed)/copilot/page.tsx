"use client";

import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { TopBar } from "@/components/TopBar";
import { Copilot } from "@/components/Copilot";
import { ConversationsSidebar } from "@/components/ConversationsSidebar";

function CopilotInner() {
  const params = useSearchParams();
  const q = params.get("q") ?? undefined;
  return <Copilot initialQuery={q} />;
}

export default function CopilotPage() {
  return (
    <>
      <TopBar title="Copilot" sub="Natural language interface · Azure OpenAI + Semantic Kernel · 3 active skills" />
      <Suspense fallback={null}>
        <CopilotInner />
      </Suspense>
    </>
  );
}
