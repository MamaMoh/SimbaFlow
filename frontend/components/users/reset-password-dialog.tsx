"use client";

import { useEffect, useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Check,
  Copy,
  Eye,
  EyeOff,
  Info,
  KeyRound,
  RefreshCw,
  X,
} from "lucide-react";
import { toast } from "sonner";

export interface ResetPasswordTarget {
  id: string;
  username: string;
  fullName?: string;
}

/**
 * The password policy, stated once and used twice: to tick the checklist the administrator reads
 * while typing, and to decide whether Reset is even offered.
 *
 * These mirror ResetUserPasswordValidator on the API. The server is still the authority — this
 * only spares someone a round trip to be told what could have been said as they typed.
 */
const RULES: { label: string; test: (value: string) => boolean }[] = [
  { label: "At least 8 characters", test: (v) => v.length >= 8 },
  { label: "An uppercase letter", test: (v) => /[A-Z]/.test(v) },
  { label: "A lowercase letter", test: (v) => /[a-z]/.test(v) },
  { label: "A digit", test: (v) => /[0-9]/.test(v) },
  { label: "A special character", test: (v) => /[^a-zA-Z0-9]/.test(v) },
];

/** Ambiguous glyphs are left out, because this password gets read aloud or copied by hand. */
const UPPER = "ABCDEFGHJKLMNPQRSTUVWXYZ";
const LOWER = "abcdefghijkmnopqrstuvwxyz";
const DIGITS = "23456789";
const SYMBOLS = "!@#$%^&*-_=+";

function suggestPassword(length = 16): string {
  const all = UPPER + LOWER + DIGITS + SYMBOLS;
  const bytes = new Uint32Array(length);
  crypto.getRandomValues(bytes);

  // One of each class first, so the result always satisfies the policy however the shuffle falls.
  const chars = [
    pick(UPPER, bytes[0]),
    pick(LOWER, bytes[1]),
    pick(DIGITS, bytes[2]),
    pick(SYMBOLS, bytes[3]),
  ];
  for (let i = 4; i < length; i++) chars.push(pick(all, bytes[i]));

  // Fisher-Yates, so the guaranteed characters do not always sit in the first four positions.
  const shuffle = new Uint32Array(chars.length);
  crypto.getRandomValues(shuffle);
  for (let i = chars.length - 1; i > 0; i--) {
    const j = shuffle[i] % (i + 1);
    [chars[i], chars[j]] = [chars[j], chars[i]];
  }
  return chars.join("");
}

function pick(set: string, random: number): string {
  return set[random % set.length];
}

interface ResetPasswordDialogProps {
  user: ResetPasswordTarget | null;
  onOpenChange: (open: boolean) => void;
  onReset?: () => void;
}

export function ResetPasswordDialog({
  user,
  onOpenChange,
  onReset,
}: ResetPasswordDialogProps) {
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [reveal, setReveal] = useState(false);
  const [copied, setCopied] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const open = !!user;

  // A password left sitting in component state after the dialog closes is a password on screen at
  // the next person to open it — clear everything on each open.
  useEffect(() => {
    if (!open) return;
    setPassword("");
    setConfirm("");
    setReveal(false);
    setCopied(false);
    setError(null);
    setSubmitting(false);
  }, [open]);

  const checks = useMemo(() => RULES.map((rule) => rule.test(password)), [password]);
  const meetsPolicy = checks.every(Boolean);
  const mismatch = confirm.length > 0 && confirm !== password;
  const canSubmit = meetsPolicy && confirm === password && !submitting;

  const handleGenerate = () => {
    const generated = suggestPassword();
    setPassword(generated);
    setConfirm(generated);
    setReveal(true);
    setCopied(false);
    setError(null);
  };

  const handleCopy = async () => {
    if (!password) return;
    try {
      await navigator.clipboard.writeText(password);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      toast.error("Could not copy — select the password and copy it manually.");
    }
  };

  const handleSubmit = async () => {
    if (!user || !canSubmit) return;
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(`/api/proxy/users/${user.id}/password`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ newPassword: password }),
      });
      const result = await res.json().catch(() => null);

      if (!res.ok || !result?.isSuccess) {
        setError(result?.error || "Could not reset the password. Please try again.");
        return;
      }

      toast.success(`Password reset for ${user.username}`, {
        description: "They will be asked to choose a new one at their next sign-in.",
      });
      onReset?.();
      onOpenChange(false);
    } catch {
      setError("Could not reach the server. Check your connection and try again.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <KeyRound className="h-4 w-4 text-muted-foreground" />
            Reset password
          </DialogTitle>
          <DialogDescription>
            Set a new password for{" "}
            <span className="font-medium text-foreground">
              {user?.fullName?.trim() || user?.username}
            </span>
            {user?.fullName?.trim() && (
              <span className="text-muted-foreground"> ({user.username})</span>
            )}
            .
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label htmlFor="new-password">New password</Label>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-7 px-2 text-xs"
                onClick={handleGenerate}
              >
                <RefreshCw className="mr-1 h-3 w-3" />
                Generate
              </Button>
            </div>
            <div className="relative">
              <Input
                id="new-password"
                type={reveal ? "text" : "password"}
                value={password}
                autoComplete="new-password"
                spellCheck={false}
                className="pr-16 font-mono"
                onChange={(e) => {
                  setPassword(e.target.value);
                  setError(null);
                }}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && canSubmit) handleSubmit();
                }}
              />
              <div className="absolute right-1 top-1/2 flex -translate-y-1/2 items-center">
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="h-7 w-7"
                  aria-label={reveal ? "Hide password" : "Show password"}
                  onClick={() => setReveal((v) => !v)}
                >
                  {reveal ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="h-7 w-7"
                  aria-label="Copy password"
                  disabled={!password}
                  onClick={handleCopy}
                >
                  {copied ? (
                    <Check className="h-3.5 w-3.5 text-green-600" />
                  ) : (
                    <Copy className="h-3.5 w-3.5" />
                  )}
                </Button>
              </div>
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="confirm-password">Confirm password</Label>
            <Input
              id="confirm-password"
              type={reveal ? "text" : "password"}
              value={confirm}
              autoComplete="new-password"
              spellCheck={false}
              className="font-mono"
              onChange={(e) => {
                setConfirm(e.target.value);
                setError(null);
              }}
              onKeyDown={(e) => {
                if (e.key === "Enter" && canSubmit) handleSubmit();
              }}
            />
            {mismatch && (
              <p className="text-xs text-destructive">The two passwords do not match.</p>
            )}
          </div>

          <ul className="grid grid-cols-2 gap-x-4 gap-y-1.5">
            {RULES.map((rule, i) => (
              <li
                key={rule.label}
                className={`flex items-center gap-1.5 text-xs ${
                  checks[i] ? "text-green-700" : "text-muted-foreground"
                }`}
              >
                {checks[i] ? (
                  <Check className="h-3 w-3 shrink-0" />
                ) : (
                  <X className="h-3 w-3 shrink-0 opacity-40" />
                )}
                {rule.label}
              </li>
            ))}
          </ul>

          <div className="flex gap-2 rounded-md border bg-muted/40 p-3 text-xs text-muted-foreground">
            <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" />
            <p>
              Share this password with them over a channel you trust. They will be asked to choose
              their own at the next sign-in, and any lockout on the account is cleared.
            </p>
          </div>

          {error && (
            <p className="rounded-md border border-destructive/30 bg-destructive/5 p-2.5 text-xs text-destructive">
              {error}
            </p>
          )}
        </div>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            disabled={submitting}
            onClick={() => onOpenChange(false)}
          >
            Cancel
          </Button>
          <Button type="button" disabled={!canSubmit} onClick={handleSubmit}>
            {submitting ? "Resetting..." : "Reset password"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
