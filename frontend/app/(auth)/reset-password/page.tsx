"use client";

import { Suspense, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

/**
 * Landing page for the link in a password-reset email.
 *
 * The email and token arrive in the query string; the user only chooses a new password.
 */
function ResetPasswordForm() {
  const params = useSearchParams();
  const router = useRouter();
  const email = params.get("email") ?? "";
  const token = params.get("token") ?? "";

  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);

  const linkIsUsable = email.length > 0 && token.length > 0;

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (password !== confirm) {
      toast.error("The two passwords do not match.");
      return;
    }
    setBusy(true);
    try {
      const res = await fetch("/api/auth/reset-password", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, token, newPassword: password, confirmPassword: confirm }),
      });
      const body = await res.json().catch(() => null);
      if (res.ok && body?.isSuccess !== false) {
        toast.success("Password changed. You can sign in now.");
        router.push("/login");
      } else {
        toast.error(body?.error || "Could not reset the password.");
      }
    } catch {
      toast.error("Could not reach the server. Try again.");
    } finally {
      setBusy(false);
    }
  };

  if (!linkIsUsable) {
    return (
      <div className="w-full max-w-sm rounded-lg border bg-card p-6 shadow-sm">
        <h1 className="text-lg font-semibold">This link is incomplete</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Open the link exactly as it appears in the email, or request a new one from the sign-in page.
        </p>
        <Button asChild className="mt-4 w-full bg-green-800 hover:bg-green-900">
          <Link href="/login">Back to sign in</Link>
        </Button>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} className="w-full max-w-sm rounded-lg border bg-card p-6 shadow-sm">
      <h1 className="text-lg font-semibold">Choose a new password</h1>
      <p className="mt-1 text-sm text-muted-foreground">for {email}</p>

      <div className="mt-5 space-y-4">
        <div className="space-y-1.5">
          <Label htmlFor="new-password">New password</Label>
          <Input
            id="new-password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
          <p className="text-xs text-muted-foreground">
            At least 8 characters, with an uppercase letter, a lowercase letter, a number and a symbol.
          </p>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="confirm-password">Confirm password</Label>
          <Input
            id="confirm-password"
            type="password"
            autoComplete="new-password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            required
          />
        </div>

        <Button type="submit" disabled={busy} className="w-full bg-green-800 hover:bg-green-900">
          {busy ? "Saving…" : "Set new password"}
        </Button>

        <Link href="/login" className="block text-center text-sm text-muted-foreground underline-offset-2 hover:underline">
          Back to sign in
        </Link>
      </div>
    </form>
  );
}

export default function ResetPasswordPage() {
  return (
    <div className="flex min-h-screen items-center justify-center p-6">
      <Suspense fallback={<div className="text-sm text-muted-foreground">Loading…</div>}>
        <ResetPasswordForm />
      </Suspense>
    </div>
  );
}
