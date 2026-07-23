"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import { signIn, getSession } from "next-auth/react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "../ui/card";
import { Globe } from "lucide-react";
import { Label } from "../ui/label";
import { Input } from "../ui/input";
import { Button } from "../ui/button";
import { Eye, EyeOff, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { ForgotPasswordDialog } from "./forgot-password-dialog";
import { useNavigationLoadingStore } from "@/lib/stores/navigation-loading-store";
import { isMockAuthEnabled } from "@/lib/auth/mock-auth";
import { useLocale } from "@/lib/i18n/locale-provider";
import Link from "next/link";

const USE_MOCKS = isMockAuthEnabled();

const loginSchema = z.object({
  username: z.string().min(3, "Username is required"),
  password: z
    .string()
    .min(USE_MOCKS ? 4 : 6, USE_MOCKS ? "Password is required" : "Password must be at least 6 characters"),
});

type LoginFormData = z.infer<typeof loginSchema>;

const loginAttempts = new Map<string, { count: number; resetTime: number }>();
const MAX_LOGIN_ATTEMPTS = 5;
const RATE_LIMIT_WINDOW = 15 * 60 * 1000;

function checkRateLimit(identifier: string): { allowed: boolean; remainingTime?: number } {
  const now = Date.now();
  const attempt = loginAttempts.get(identifier);

  if (!attempt || now > attempt.resetTime) {
    loginAttempts.set(identifier, { count: 1, resetTime: now + RATE_LIMIT_WINDOW });
    return { allowed: true };
  }

  if (attempt.count >= MAX_LOGIN_ATTEMPTS) {
    const remainingTime = Math.ceil((attempt.resetTime - now) / 1000 / 60);
    return { allowed: false, remainingTime };
  }

  attempt.count++;
  return { allowed: true };
}

function recordSuccessfulLogin(identifier: string) {
  loginAttempts.delete(identifier);
}

const LoginForm = () => {
  const router = useRouter();
  const { chrome } = useLocale();
  const [showPassword, setShowPassword] = useState(false);
  const [showForgotPassword, setShowForgotPassword] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const { setLoading } = useNavigationLoadingStore();

  const {
    control,
    handleSubmit,
    setValue,
    formState: { errors },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      username: USE_MOCKS ? "demo" : "",
      password: USE_MOCKS ? "demo123" : "",
    },
  });

  const onSubmit = async (data: LoginFormData) => {
    if (isSubmitting) return;

    const rateLimitKey = data.username.toLowerCase();
    const rateLimitCheck = checkRateLimit(rateLimitKey);
    if (!rateLimitCheck.allowed) {
      toast.error(
        `Too many login attempts. Please try again in ${rateLimitCheck.remainingTime} minute(s).`,
      );
      return;
    }

    setIsSubmitting(true);
    setLoading(true);

    let timeoutId: NodeJS.Timeout | null = null;
    timeoutId = setTimeout(() => {
      setIsSubmitting(false);
      setLoading(false);
    }, 30000);

    try {
      const response = await signIn("credentials", {
        redirect: false,
        username: data.username,
        password: data.password,
      });

      const session = await getSession();
      const requiresPasswordChange = (session?.user as any)?.requiresPasswordChange === true;
      const username = data.username || (session?.user as any)?.username;

      if (requiresPasswordChange) {
        const usernameParam = encodeURIComponent(username || data.username || "");
        if (timeoutId) clearTimeout(timeoutId);
        setIsSubmitting(false);
        setLoading(false);
        toast.info("Password change required. Please change your password to continue.");
        router.push(`/change-password?username=${usernameParam}`);
        return;
      }

      if (response?.ok && !response.error) {
        const userProfile = (session?.user as any)?.userProfile;
        const isFirstLogin = userProfile?.isFirstLogin === true;

        if (timeoutId) clearTimeout(timeoutId);
        setIsSubmitting(false);
        setLoading(false);

        recordSuccessfulLogin(rateLimitKey);

        if (isFirstLogin) {
          router.push(`/change-password`);
        } else {
          toast.success("Login successful!");
          router.push(`/overview`);
          router.refresh();
        }
      } else {
        const errorMessage = response?.error || "";
        if (
          errorMessage.includes("Password change required") ||
          errorMessage.includes("must change your password")
        ) {
          const usernameParam = encodeURIComponent(data.username || "");
          if (timeoutId) clearTimeout(timeoutId);
          setIsSubmitting(false);
          setLoading(false);
          toast.info("Password change required. Please change your password to continue.");
          router.push(`/change-password?username=${usernameParam}`);
        } else {
          if (timeoutId) clearTimeout(timeoutId);
          setIsSubmitting(false);
          setLoading(false);

          const errorCode = response?.error || "";
          let userMessage: string;
          switch (errorCode) {
            case "CredentialsSignin":
              userMessage =
                "Invalid username or password. Please check your credentials and try again.";
              break;
            case "SessionRequired":
              userMessage = "Your session has expired. Please sign in again.";
              break;
            case "AccessDenied":
              userMessage = "Access denied. Your account may be deactivated.";
              break;
            default:
              userMessage = errorCode || "Login failed. Please try again.";
              break;
          }
          toast.error(userMessage);
        }
      }
    } catch {
      toast.error("An error occurred during login. Please try again.");
    } finally {
      if (timeoutId) clearTimeout(timeoutId);
      setIsSubmitting(false);
      setLoading(false);
    }
  };

  const handleFormSubmit = handleSubmit(onSubmit, () => {
    setIsSubmitting(false);
    setLoading(false);
  });

  return (
    <div className="mx-auto w-full max-w-md space-y-6">
      <Card className="border-0 bg-card shadow-lg">
        <CardHeader className="space-y-1 pb-4">
          <div className="mb-2 flex items-center justify-center">
            <Globe className="h-16 w-16 text-primary" />
          </div>
          <CardTitle className="text-center text-2xl font-bold">{chrome.loginTitle}</CardTitle>
          <CardDescription className="text-center text-muted-foreground">
            {chrome.loginSubtitle}
          </CardDescription>
          {USE_MOCKS && (
            <p className="pt-2 text-center text-xs text-muted-foreground">{chrome.demoHint}</p>
          )}
        </CardHeader>
        <CardContent className="space-y-4">
          <form name="loginForm" onSubmit={handleFormSubmit} noValidate className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="username" className="text-sm font-medium">
                {chrome.username}
              </Label>
              <Controller
                name="username"
                control={control}
                render={({ field }) => (
                  <Input
                    {...field}
                    id="username"
                    type="text"
                    placeholder={chrome.usernamePlaceholder}
                    className="h-10"
                    autoComplete="username"
                    disabled={isSubmitting}
                  />
                )}
              />
              {errors.username && (
                <p className="text-sm text-red-500">{errors.username.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="password" className="text-sm font-medium">
                {chrome.password}
              </Label>
              <div className="relative">
                <Controller
                  name="password"
                  control={control}
                  render={({ field }) => (
                    <Input
                      {...field}
                      id="password"
                      type={showPassword ? "text" : "password"}
                      placeholder={chrome.passwordPlaceholder}
                      className="h-10 pe-10"
                      autoComplete="current-password"
                      disabled={isSubmitting}
                    />
                  )}
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="absolute end-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                  onClick={() => setShowPassword(!showPassword)}
                  aria-label={showPassword ? "Hide password" : "Show password"}
                  disabled={isSubmitting}
                >
                  {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </Button>
              </div>
              {errors.password && (
                <p className="text-sm text-red-500">{errors.password.message}</p>
              )}
            </div>
            <Button type="submit" className="h-10 w-full font-medium" disabled={isSubmitting}>
              {isSubmitting ? (
                <>
                  <Loader2 className="me-2 h-4 w-4 animate-spin" />
                  {chrome.signingIn}
                </>
              ) : (
                chrome.signIn
              )}
            </Button>
            {!USE_MOCKS && (
              <div className="text-center">
                <Button
                  type="button"
                  variant="link"
                  className="text-sm text-muted-foreground hover:text-primary"
                  onClick={() => setShowForgotPassword(true)}
                  disabled={isSubmitting}
                >
                  {chrome.forgotPassword}
                </Button>
              </div>
            )}
            {USE_MOCKS && (
              <Button
                type="button"
                variant="outline"
                className="w-full"
                disabled={isSubmitting}
                onClick={() => {
                  setValue("username", "demo");
                  setValue("password", "demo123");
                  void handleFormSubmit();
                }}
              >
                {chrome.signIn} (demo)
              </Button>
            )}
          </form>
          <div className="text-center">
            <Button asChild variant="link" className="text-sm">
              <Link href="/">{chrome.backHome}</Link>
            </Button>
          </div>
        </CardContent>
      </Card>
      <ForgotPasswordDialog open={showForgotPassword} onOpenChange={setShowForgotPassword} />
    </div>
  );
};

export default LoginForm;
