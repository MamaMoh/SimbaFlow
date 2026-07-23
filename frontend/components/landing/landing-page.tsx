"use client";

import { useState } from "react";
import Link from "next/link";
import { signIn } from "next-auth/react";
import { useRouter } from "next/navigation";
import { motion, useReducedMotion } from "framer-motion";
import {
  ArrowRight,
  Building2,
  CheckCircle2,
  Clock3,
  Play,
  ShieldCheck,
  Workflow,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { LanguageSwitcher } from "@/components/i18n/language-switcher";
import { useLocale } from "@/lib/i18n/locale-provider";
import { isMockAuthEnabled } from "@/lib/auth/mock-auth";
import { toast } from "sonner";

const USE_MOCKS = isMockAuthEnabled();

const HERO_IMG = "/images/landing-hero.svg";
const TEAM_IMG = "/images/landing-team.svg";

const CAP_ICONS = [Building2, Clock3, ShieldCheck] as const;

export function LandingPage() {
  const reduce = useReducedMotion();
  const router = useRouter();
  const { locale, landing: t, dir } = useLocale();
  const [demoLoading, setDemoLoading] = useState(false);

  const enterDemo = async () => {
    if (!USE_MOCKS) {
      router.push("/login");
      return;
    }
    setDemoLoading(true);
    try {
      const res = await signIn("credentials", {
        redirect: false,
        username: "demo",
        password: "demo123",
      });
      if (res?.ok) {
        router.push("/overview");
        router.refresh();
      } else {
        toast.error("Could not start demo session");
        router.push("/login");
      }
    } finally {
      setDemoLoading(false);
    }
  };

  const fade = (delay = 0) =>
    reduce
      ? undefined
      : {
          initial: { opacity: 0, y: 16 },
          whileInView: { opacity: 1, y: 0 },
          viewport: { once: true, margin: "-40px" },
          transition: { duration: 0.55, delay },
        };

  return (
    <div
      dir={dir}
      lang={locale}
      className="fixed inset-0 z-50 overflow-y-auto overflow-x-hidden bg-background text-foreground"
    >
      {/* Header */}
      <header className="sticky top-0 z-40 border-b border-border/60 bg-background/90 backdrop-blur-md">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4 md:px-6">
          <Link href="/" className="text-lg font-bold tracking-tight text-foreground">
            Simba<span className="text-primary">Flow</span>
          </Link>
          <nav className="hidden items-center gap-6 text-sm font-medium text-muted-foreground lg:flex">
            <a href="#solutions" className="hover:text-foreground">
              {t.nav.solutions}
            </a>
            <a href="#features" className="hover:text-foreground">
              {t.nav.features}
            </a>
            <a href="#services" className="hover:text-foreground">
              {t.nav.ticketServices}
            </a>
            <a href="#why" className="hover:text-foreground">
              {t.nav.why}
            </a>
          </nav>
          <div className="flex items-center gap-2">
            <LanguageSwitcher variant="compact" />
            <Button asChild variant="ghost" size="sm" className="hidden sm:inline-flex">
              <Link href="/login">{t.login}</Link>
            </Button>
            <Button size="sm" className="font-semibold" disabled={demoLoading} onClick={() => void enterDemo()}>
              {t.enterDemo}
            </Button>
          </div>
        </div>
      </header>

      {/* Hero */}
      <section className="mx-auto grid max-w-6xl gap-10 px-4 py-14 md:px-6 lg:grid-cols-2 lg:items-center lg:py-20">
        <div>
          <motion.div
            {...fade(0)}
            className="mb-5 inline-flex rounded-full border border-border bg-muted px-3 py-1 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground"
          >
            {t.badge}
          </motion.div>
          <motion.h1
            {...fade(0.05)}
            className="text-4xl font-bold leading-[1.1] tracking-tight text-foreground sm:text-5xl lg:text-[3.25rem]"
          >
            {t.heroTitleBefore}{" "}
            <span className="text-primary">{t.heroTitleAccent}</span>
          </motion.h1>
          <motion.p {...fade(0.1)} className="mt-5 max-w-lg text-base leading-relaxed text-muted-foreground sm:text-lg">
            {t.heroSubtitle}
          </motion.p>
          <motion.div {...fade(0.15)} className="mt-8 flex flex-wrap gap-3">
            <Button asChild size="lg" className="gap-2 font-semibold">
              <Link href="/login">
                {t.getStarted}
                <ArrowRight className="h-4 w-4" />
              </Link>
            </Button>
            <Button
              size="lg"
              variant="outline"
              className="gap-2"
              disabled={demoLoading}
              onClick={() => void enterDemo()}
            >
              <Play className="h-4 w-4 fill-current" />
              {t.watchDemo}
            </Button>
          </motion.div>
        </div>

        <motion.div
          {...fade(0.12)}
          className="relative"
        >
          <div className="overflow-hidden rounded-2xl border border-border shadow-lg">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={HERO_IMG} alt="" className="aspect-[5/4] w-full object-cover" />
          </div>
          <div className="absolute bottom-5 start-5 rounded-xl border border-border bg-card px-4 py-3 shadow-md">
            <div className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
              {t.growthLabel}
            </div>
            <div className="text-2xl font-bold text-primary">{t.growthValue}</div>
          </div>
        </motion.div>
      </section>

      {/* Integrations */}
      <section id="services" className="border-y border-border bg-card/40 py-10">
        <div className="mx-auto max-w-6xl px-4 md:px-6">
          <p className="text-center text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            {t.integrationsLabel}
          </p>
          <ul className="mt-6 flex flex-wrap items-center justify-center gap-x-10 gap-y-4">
            {t.integrations.map((name) => (
              <li key={name} className="text-sm font-bold tracking-wide text-muted-foreground/80">
                {name}
              </li>
            ))}
          </ul>
        </div>
      </section>

      {/* Stats */}
      <section className="bg-muted/70 py-12">
        <div className="mx-auto grid max-w-6xl grid-cols-2 gap-8 px-4 md:grid-cols-4 md:px-6">
          {t.stats.map((s) => (
            <div key={s.label} className="text-center">
              <div className="text-3xl font-bold tracking-tight text-foreground md:text-4xl">{s.value}</div>
              <div className="mt-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                {s.label}
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Capabilities */}
      <section id="features" className="py-20">
        <div className="mx-auto max-w-6xl px-4 md:px-6">
          <p className="text-center text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            {t.capabilitiesEyebrow}
          </p>
          <h2 className="mt-2 text-center text-3xl font-bold tracking-tight md:text-4xl">
            {t.capabilitiesTitle}
          </h2>
          <ul className="mt-12 grid gap-5 md:grid-cols-3">
            {t.capabilities.map((cap, i) => {
              const Icon = CAP_ICONS[i]!;
              return (
                <motion.li
                  key={cap.title}
                  {...fade(i * 0.06)}
                  className="rounded-2xl border border-border bg-card p-6 shadow-sm"
                >
                  <span className="mb-4 flex h-11 w-11 items-center justify-center rounded-full bg-muted text-primary">
                    <Icon className="h-5 w-5" strokeWidth={1.75} />
                  </span>
                  <h3 className="text-lg font-semibold">{cap.title}</h3>
                  <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{cap.text}</p>
                </motion.li>
              );
            })}
          </ul>
        </div>
      </section>

      {/* Command center split */}
      <section id="solutions" className="border-t border-border bg-muted/40 py-20">
        <div className="mx-auto grid max-w-6xl items-center gap-12 px-4 md:px-6 lg:grid-cols-2">
          <div className="relative">
            <div className="overflow-hidden rounded-2xl border border-border shadow-lg">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={TEAM_IMG} alt="" className="aspect-[4/3] w-full object-cover" />
            </div>
            <div className="absolute bottom-5 start-5 flex items-center gap-2 rounded-lg border border-border bg-card px-3 py-2 text-xs font-medium shadow-md">
              <CheckCircle2 className="h-4 w-4 text-primary" />
              {t.syncToast}
            </div>
          </div>
          <div id="why">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              {t.commandEyebrow}
            </p>
            <h2 className="mt-2 text-3xl font-bold tracking-tight md:text-4xl">{t.commandTitle}</h2>
            <p className="mt-4 text-muted-foreground">{t.commandBody}</p>
            <ul className="mt-8 space-y-5">
              {t.commandItems.map((item) => (
                <li key={item.title} className="flex gap-3">
                  <Workflow className="mt-0.5 h-5 w-5 shrink-0 text-primary" strokeWidth={1.75} />
                  <div>
                    <div className="font-semibold">{item.title}</div>
                    <p className="mt-0.5 text-sm text-muted-foreground">{item.text}</p>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="bg-primary py-16 text-primary-foreground">
        <div className="mx-auto max-w-6xl px-4 text-center md:px-6">
          <h2 className="text-3xl font-bold tracking-tight md:text-4xl">{t.ctaTitle}</h2>
          <p className="mx-auto mt-3 max-w-xl text-primary-foreground/85">{t.ctaBody}</p>
          <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
            <Button
              size="lg"
              variant="secondary"
              className="font-bold"
              disabled={demoLoading}
              onClick={() => void enterDemo()}
            >
              {t.bookDemo}
            </Button>
            <Button
              asChild
              size="lg"
              variant="outline"
              className="border-primary-foreground/40 bg-transparent text-primary-foreground hover:bg-primary-foreground/10 hover:text-primary-foreground"
            >
              <Link href="/login">{t.login}</Link>
            </Button>
          </div>
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t border-border bg-background">
        <div className="mx-auto grid max-w-6xl gap-10 px-4 py-12 sm:grid-cols-2 lg:grid-cols-4 md:px-6">
          <div>
            <div className="text-lg font-bold">
              Simba<span className="text-primary">Flow</span>
            </div>
            <p className="mt-3 text-sm text-muted-foreground">{t.footerAbout}</p>
          </div>
          <div>
            <h3 className="text-xs font-bold uppercase tracking-wide text-muted-foreground">{t.footerQuick}</h3>
            <ul className="mt-3 space-y-2 text-sm">
              {t.footerLinks.map((l) => (
                <li key={l}>
                  <a href="#features" className="text-foreground/80 hover:text-primary">
                    {l}
                  </a>
                </li>
              ))}
            </ul>
          </div>
          <div>
            <h3 className="text-xs font-bold uppercase tracking-wide text-muted-foreground">{t.footerGov}</h3>
            <ul className="mt-3 space-y-2 text-sm">
              {t.footerGovLinks.map((l) => (
                <li key={l} className="text-foreground/80">
                  {l}
                </li>
              ))}
            </ul>
          </div>
          <div>
            <h3 className="text-xs font-bold uppercase tracking-wide text-muted-foreground">{t.footerLegal}</h3>
            <ul className="mt-3 space-y-2 text-sm">
              {t.footerLegalLinks.map((l) => (
                <li key={l} className="text-foreground/80">
                  {l}
                </li>
              ))}
            </ul>
            <p className="mt-4 text-xs text-muted-foreground">{t.footerPhone}</p>
            <p className="text-xs text-muted-foreground">{t.footerEmail}</p>
          </div>
        </div>
        <div className="border-t border-border py-4 text-center text-xs text-muted-foreground">
          © {new Date().getFullYear()} SimbaFlow — {t.footerRights}
        </div>
      </footer>
    </div>
  );
}
