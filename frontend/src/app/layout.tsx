import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "TelcoPilot — AI-Native Telco Operations",
  description: "Natural language network intelligence for the Lagos metro NOC.",
};

// Runs before hydration so a stored "light" preference is applied without a dark-mode flash.
// :root in globals.css already supplies dark values, so absence of a class === dark.
const themeBootstrap = `
try {
  var t = localStorage.getItem('tp-theme');
  if (t === 'light' || t === 'dark') document.body.className = 'theme-' + t;
} catch (e) {}
`;

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
      <body suppressHydrationWarning>
        <script dangerouslySetInnerHTML={{ __html: themeBootstrap }} />
        {children}
      </body>
    </html>
  );
}
