import type { Metadata } from "next";
import { Cta } from "@/components/marketing/cta";
import { Features } from "@/components/marketing/features";
import { Hero } from "@/components/marketing/hero";
import { Pricing } from "@/components/marketing/pricing";
import { SiteFooter } from "@/components/marketing/site-footer";
import { SiteNav } from "@/components/marketing/site-nav";

export const metadata: Metadata = {
  title: "SimbaFlow — Labour export agency management",
  description:
    "One pipeline for a labour export agency: candidate intake, embassy and Tasheer processing, government clearances, travel and commission settlement.",
  openGraph: {
    title: "SimbaFlow — Labour export agency management",
    description:
      "One pipeline for candidate intake, embassy processing, government clearances, travel and commission settlement.",
    type: "website",
  },
};

/**
 * Four sections: what it is, what it does, what it costs, how to get in touch.
 *
 * It ran to eight — a corridor marquee, a stage-by-stage walk through the pipeline, a security
 * page and an FAQ, each about as long as this whole page is now. Everything there was true and
 * almost none of it was being read; what survives is what somebody needs in order to decide to
 * write to us, and the rest is that conversation.
 */
export default function LandingPage() {
  return (
    <>
      <SiteNav />
      <main>
        <Hero />
        <Features />
        <Pricing />
        <Cta />
      </main>
      <SiteFooter />
    </>
  );
}
