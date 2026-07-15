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
// import Image from "next/image";
import { Globe } from "lucide-react";
import { Label } from "../ui/label";
import { Input } from "../ui/input";
import { Button } from "../ui/button";
import { Eye, EyeOff, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { ForgotPasswordDialog } from "./forgot-password-dialog";
import { useNavigationLoadingStore } from "@/lib/stores/navigation-loading-store";

const loginSchema = z.object({
  username: z.string().min(3, "Username is required"),
  password: z.string().min(6, "Password must be at least 6 characters"),
});

type LoginFormData = z.infer<typeof loginSchema>;

/**
 * Rate limiting configuration for login attempts
 * Tracks login attempts per username to prevent brute force attacks
 */
const loginAttempts = new Map<string, { count: number; resetTime: number }>();
const MAX_LOGIN_ATTEMPTS = 5;
const RATE_LIMIT_WINDOW = 15 * 60 * 1000; // 15 minutes in milliseconds

/**
 * Checks if login attempt is within rate limit constraints
 * @param identifier - Username or identifier for rate limiting
 * @returns Object indicating if request is allowed and remaining lockout time if blocked
 */
function checkRateLimit(identifier: string): { allowed: boolean; remainingTime?: number } {
  const now = Date.now();
  const attempt = loginAttempts.get(identifier);
  
  if (!attempt || now > attempt.resetTime) {
    // Initialize or reset attempt tracking for new time window
    loginAttempts.set(identifier, { count: 1, resetTime: now + RATE_LIMIT_WINDOW });
    return { allowed: true };
  }
  
  if (attempt.count >= MAX_LOGIN_ATTEMPTS) {
    // Calculate remaining lockout time in minutes
    const remainingTime = Math.ceil((attempt.resetTime - now) / 1000 / 60);
    return { allowed: false, remainingTime };
  }
  
  // Increment attempt counter
  attempt.count++;
  return { allowed: true };
}

/**
 * Clears rate limit tracking for successful login
 * @param identifier - Username or identifier to clear from rate limit tracking
 */
function recordSuccessfulLogin(identifier: string) {
  loginAttempts.delete(identifier);
}

const LoginForm = () => {
  const router = useRouter();
  const [showPassword, setShowPassword] = useState(false);
  const [showForgotPassword, setShowForgotPassword] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const { setLoading } = useNavigationLoadingStore();

  const {
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      username: "",
      password: "",
    },
  });

  const onSubmit = async (data: LoginFormData) => {
    // Prevent duplicate form submissions
    if (isSubmitting) {
      return;
    }

    // Enforce rate limiting to prevent brute force attacks
    const rateLimitKey = data.username.toLowerCase();
    const rateLimitCheck = checkRateLimit(rateLimitKey);
    if (!rateLimitCheck.allowed) {
      toast.error(
        `Too many login attempts. Please try again in ${rateLimitCheck.remainingTime} minute(s).`
      );
      return;
    }

    // Initialize loading state after validation passes
    setIsSubmitting(true);
    setLoading(true);

    // Configure timeout to prevent indefinite loading state
    let timeoutId: NodeJS.Timeout | null = null;
    timeoutId = setTimeout(() => {
setIsSubmitting(false);
      setLoading(false);
    }, 30000); // 30 second timeout

    try {
      const response = await signIn("credentials", {
        redirect: false,
        username: data.username,
        password: data.password,
      });
      
      // Always get session to check for requiresPasswordChange
      const session = await getSession();
      const requiresPasswordChange = (session?.user as any)?.requiresPasswordChange === true;
      const username = data.username || (session?.user as any)?.username;
      
      if (requiresPasswordChange) {
        // Pass username via URL params (safer than sessionStorage)
        const usernameParam = encodeURIComponent(username || data.username || "");
        if (timeoutId) clearTimeout(timeoutId);
        setIsSubmitting(false);
        setLoading(false);
        toast.info("Password change required. Please change your password to continue.");
        router.push(`/change-password?username=${usernameParam}`);
        return;
      }
      
      if (response?.ok && !response.error) {
        // Check if this is the first login and redirect to change password page
        const userProfile = (session?.user as any)?.userProfile;
        const isFirstLogin = userProfile?.isFirstLogin === true;
        
        if (timeoutId) clearTimeout(timeoutId);
        setIsSubmitting(false);
        setLoading(false);
        
        // Record successful login to reset rate limit
        recordSuccessfulLogin(rateLimitKey);
        
        if (isFirstLogin) {
          router.push(`/change-password`);
        } else {
          toast.success("Login successful!");
          router.push(`/overview`);
        }
      } else {
        // Check if the error indicates password change is required
        const errorMessage = response?.error || "";
        if (errorMessage.includes("Password change required") || errorMessage.includes("must change your password")) {
          // Pass username via URL params (safer than sessionStorage)
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
          
          // Map NextAuth generic error codes to user-friendly messages
          const errorCode = response?.error || "";
          let userMessage: string;
          switch (errorCode) {
            case "CredentialsSignin":
              userMessage = "Invalid username or password. Please check your credentials and try again.";
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
    } catch (error) {
      // Log authentication errors only in development environment
toast.error("An error occurred during login. Please try again.");
    } finally {
      if (timeoutId) {
        clearTimeout(timeoutId);
      }
      setIsSubmitting(false);
      setLoading(false);
    }
  };

  // Handle form submission with immediate loading state feedback
  const handleFormSubmit = handleSubmit(
    onSubmit,
    // Clear loading state when validation fails
    (errors) => {
      // Log validation errors only in development environment to prevent information disclosure
setIsSubmitting(false);
      setLoading(false);
    }
  );

  return (
    <div className="w-full max-w-md mx-auto space-y-6">
      <Card className="shadow-lg border-0 bg-card">
        <CardHeader className="space-y-1 pb-4">
          <div className="flex items-center justify-center mb-2">
            <Globe className="h-16 w-16 text-primary" />
          </div>
          <CardTitle className="text-2xl font-bold text-center">
            {"SimbaFlow Login"}
          </CardTitle>
          <CardDescription className="text-center text-muted-foreground">
            {"SimbaFlow"}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <form
            name="loginForm"
            onSubmit={handleFormSubmit}
            noValidate
            className="space-y-4"
          >
            <div className="space-y-2">
              <Label htmlFor="username" className="text-sm font-medium">
                Username
              </Label>
              <Controller
                name="username"
                control={control}
                render={({ field }) => (
                  <Input
                    {...field}
                    id="username"
                    type="text"
                    placeholder="Enter your username"
                    className="h-10"
                    autoComplete="username"
                    disabled={isSubmitting}
                  />
                )}
              />
              {errors.username && (
                <p className="text-sm text-red-500">
                  {errors.username.message}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="password" className="text-sm font-medium">
                Password
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
                      placeholder="Enter your password"
                      className="h-10 pr-10"
                      autoComplete="current-password"
                      disabled={isSubmitting}
                    />
                  )}
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="absolute right-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                  onClick={() => setShowPassword(!showPassword)}
                  aria-label={showPassword ? "Hide password" : "Show password"}
                  disabled={isSubmitting}
                >
                  {showPassword ? (
                    <EyeOff className="h-4 w-4" />
                  ) : (
                    <Eye className="h-4 w-4" />
                  )}
                </Button>
              </div>
              {errors.password && (
                <p className="text-sm text-red-500">
                  {errors.password.message}
                </p>
              )}
            </div>
              <Button
              type="submit"
              className="w-full h-10 font-medium"
              disabled={isSubmitting}
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Signing in...
                </>
              ) : (
                "Sign In"
              )}
            </Button>
            <div className="text-center">
              <Button
                type="button"
                variant="link"
                className="text-sm text-muted-foreground hover:text-primary"
                onClick={() => setShowForgotPassword(true)}
                disabled={isSubmitting}
              >
                Forgot Password?
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
      <ForgotPasswordDialog open={showForgotPassword} onOpenChange={setShowForgotPassword} />
    </div>
  );
};

export default LoginForm;
