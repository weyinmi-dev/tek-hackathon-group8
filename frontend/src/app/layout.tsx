import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "TelcoPilot — AI-Native Telco Operations",
  description: "Natural language network intelligence for the Lagos metro NOC.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="" />
        <link
          href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;600;700&family=Geist:wght@300;400;500;600;700&display=swap"
          rel="stylesheet"
        />
      </head>
      <body className="theme-dark">{children}</body>
    </html>
  );
}
