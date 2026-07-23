"use client";

import { Languages } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { useLocale, type AppLocale } from "@/lib/i18n/locale-provider";

type LanguageSwitcherProps = {
  variant?: "default" | "onPrimary" | "compact";
  className?: string;
  showIcon?: boolean;
};

export function LanguageSwitcher({
  variant = "default",
  className,
  showIcon = true,
}: LanguageSwitcherProps) {
  const { locale, setLocale, chrome } = useLocale();

  const pick = (next: AppLocale) => setLocale(next);

  if (variant === "onPrimary") {
    return (
      <div
        className={cn(
          "flex items-center rounded-md border border-primary-foreground/20 bg-primary-foreground/10 p-0.5 text-xs font-bold",
          className,
        )}
        role="group"
        aria-label={chrome.language}
      >
        {(["en", "ar"] as const).map((code) => (
          <button
            key={code}
            type="button"
            onClick={() => pick(code)}
            className={cn(
              "rounded px-2 py-1 transition",
              locale === code
                ? "bg-secondary text-secondary-foreground"
                : "text-primary-foreground/80 hover:text-primary-foreground",
            )}
          >
            {code === "en" ? "EN" : "ع"}
          </button>
        ))}
        {showIcon && <Languages className="mx-1 hidden h-3.5 w-3.5 opacity-70 sm:block" aria-hidden />}
      </div>
    );
  }

  return (
    <div
      className={cn(
        "inline-flex items-center rounded-md border border-border bg-background p-0.5 text-xs font-bold",
        className,
      )}
      role="group"
      aria-label={chrome.language}
    >
      {showIcon && <Languages className="ms-1.5 h-3.5 w-3.5 text-muted-foreground" aria-hidden />}
      {(["en", "ar"] as const).map((code) => (
        <Button
          key={code}
          type="button"
          size="sm"
          variant={locale === code ? "default" : "ghost"}
          className={cn("h-7 px-2.5 text-xs font-bold", variant === "compact" && "h-6 px-2")}
          onClick={() => pick(code)}
        >
          {code === "en" ? "EN" : "ع"}
        </Button>
      ))}
    </div>
  );
}
