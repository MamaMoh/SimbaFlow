"use client";

import { useRouter } from "next/navigation";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { Button } from "@/components/ui/button";

interface BackButtonProps {
  /** When provided, renders as Link to this href. Otherwise uses router.back(). */
  href?: string;
  /** Button label. Default: "Back" */
  label?: string;
  /** Optional class name for the button. */
  className?: string;
  variant?: "default" | "destructive" | "outline" | "secondary" | "ghost" | "link";
  size?: "default" | "sm" | "lg" | "icon";
}

export function BackButton({ href, label = "Back", className, variant = "ghost", size = "sm" }: BackButtonProps) {
  const router = useRouter();
  const content = (
    <>
      <ArrowLeft className="h-4 w-4" />
      {label}
    </>
  );
  if (href) {
    return (
      <Button variant={variant} size={size} asChild className={className}>
        <Link href={href}>{content}</Link>
      </Button>
    );
  }
  return (
    <Button variant={variant} size={size} onClick={() => router.back()} className={className}>
      {content}
    </Button>
  );
}
