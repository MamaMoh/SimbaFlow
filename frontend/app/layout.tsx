import type React from "react";
import type { Metadata } from "next";
import { Suspense } from "react";
import { GeistSans } from "geist/font/sans";
import { GeistMono } from "geist/font/mono";
import { ThemeProvider } from "@/components/ui/theme-provider";
import "./globals.css";
import { Toaster } from "@/components/ui/sonner";
import { AuthProvider } from "@/components/auth/auth-provider";
import { LogoutOverlay } from "@/components/auth/logout-overlay";
import { CompilationLoading } from "@/components/loading/progress-loading";
import { NavigationLoading } from "@/components/loading/immediate-loading";
import { GlobalErrorHandler } from "@/components/error/error-handler";
import { ApiErrorBoundary } from "@/components/error/api-error-boundary";
import { NavigationLinkHandler } from "@/components/navigation-link-handler";
import { LocaleProvider } from "@/lib/i18n/locale-provider";

export const metadata: Metadata = {
  title: "SimbaFlow",
  description: "Labour Export Agency Management System",
  formatDetection: {
    telephone: false,
  },
  icons: {
    icon: [
      { url: "/SimbLogo.svg", type: "image/svg+xml" },
      { url: "/SimbLogo.svg", type: "image/svg+xml", sizes: "any" },
    ],
    apple: [{ url: "/SimbLogo.svg", type: "image/svg+xml" }],
    shortcut: "/SimbLogo.svg",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="ethiopian" suppressHydrationWarning>
      <head>
        <script src="/theme-init.js" suppressHydrationWarning />
      </head>
      <body
        className={`${GeistSans.className} ${GeistSans.variable} ${GeistMono.variable} h-screen bg-background font-sans antialiased overflow-hidden`}
        suppressHydrationWarning
      >
        <AuthProvider>
          <LocaleProvider>
            <ThemeProvider>
              <GlobalErrorHandler />
              <NavigationLinkHandler />
              <ApiErrorBoundary>
                <Suspense fallback={<CompilationLoading />}>{children}</Suspense>
              </ApiErrorBoundary>
              <NavigationLoading />
              <Toaster />
              <LogoutOverlay />
            </ThemeProvider>
          </LocaleProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
