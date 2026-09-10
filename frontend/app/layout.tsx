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

export const metadata: Metadata = {
  title: "SimbaFlow",
  description:
    "Labour Export Agency Management System",
  formatDetection: {
    telephone: false,
  },
  // The globe the app signs in under. These pointed at /SimbLogo.svg, which did not exist, so
  // every tab fell back to the browser's blank page icon. The PNG is there for anything that
  // will not take an SVG, and iOS gets a real PNG on a white tile — it ignores SVG touch icons
  // and paints transparency black.
  icons: {
    icon: [
      { url: "/SimbLogo.svg", type: "image/svg+xml" },
      { url: "/favicon-32.png", type: "image/png", sizes: "32x32" },
    ],
    apple: [{ url: "/apple-icon.png", type: "image/png", sizes: "180x180" }],
    shortcut: "/favicon-32.png",
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
          <ThemeProvider>
            <GlobalErrorHandler />
            <NavigationLinkHandler />
            <ApiErrorBoundary>
              <Suspense fallback={<CompilationLoading />}>
                {children}
              </Suspense>
            </ApiErrorBoundary>
            <NavigationLoading />
            <Toaster position="top-right" richColors closeButton />
            <LogoutOverlay />
          </ThemeProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
