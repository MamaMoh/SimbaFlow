import type { Metadata } from "next";
import { Cta } from "@/components/marketing/cta";
import { Faq } from "@/components/marketing/faq";
import { Features } from "@/components/marketing/features";
import { Hero } from "@/components/marketing/hero";
import { Pricing } from "@/components/marketing/pricing";
import { Security } from "@/components/marketing/security";
import { SiteFooter } from "@/components/marketing/site-footer";
import { SiteNav } from "@/components/marketing/site-nav";
import { Ticker } from "@/components/marketing/ticker";
import { Workflow } from "@/components/marketing/workflow";

export const metadata: Metadata = {
  title: "SimbaFlow — Labour export agency management",
  description:
    "SimbaFlow runs the full overseas deployment lifecycle for labour export agencies: candidate intake, embassy and Tasheer processing, government clearances, travel logistics and commission settlement — on one configurable pipeline.",
  openGraph: {
    title: "SimbaFlow — Labour export agency management",
    description:
      "One pipeline for candidate intake, embassy processing, government clearances, travel and commission settlement.",
    type: "website",
  },
};

export default function LandingPage() {
  return (
    <>
      <SiteNav />
      <main>
        <Hero />
        <Ticker />
        <Features />
        <Workflow />
        <Security />
        <Pricing />
        <Faq />
        <Cta />
      </main>
      <SiteFooter />
    </>
  );
}
