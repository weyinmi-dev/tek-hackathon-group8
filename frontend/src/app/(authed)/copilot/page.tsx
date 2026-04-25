"use client";

import { TopBar } from "@/components/TopBar";
import { Copilot } from "@/components/Copilot";

export default function CopilotPage() {
  return (
    <>
      <TopBar title="Copilot" sub="Natural language interface · Azure OpenAI + Semantic Kernel · 3 active skills" />
      <Copilot />
    </>
  );
}
