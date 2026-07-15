"use client";

import React, { useState, useMemo, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "../ui/card";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "../ui/form";
import { Input } from "../ui/input";
import { Button } from "../ui/button";
import { Slider } from "../ui/slider";
import { Eye, EyeOff, CheckCircle2, AlertCircle, Copy, Info } from "lucide-react";
import { toast } from "sonner";
import {
  calculatePasswordStrength,
  getPasswordStrengthColor,
  getPasswordStrengthTextColor,
  getPasswordStrengthLabel,
} from "@/lib/utils/password-strength";
import { changePassword } from "@/lib/server/actions/users";

const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, "Current password is required"),
    newPassword: z.string().min(6, "Password must be at least 6 characters"),
    confirmPassword: z.string().min(1, "Please confirm your password"),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: "Passwords don't match",
    path: ["confirmPassword"],
  })
  .refine((data) => {
    const strength = calculatePasswordStrength(data.newPassword);
    return strength.meetsApiRequirements;
  }, {
    message: "Password must contain at least one letter and one digit",
    path: ["newPassword"],
  });

type ChangePasswordFormData = z.infer<typeof changePasswordSchema>;

export function ChangePasswordPageForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [showSamplePassword, setShowSamplePassword] = useState(false);
  const [samplePassword, setSamplePassword] = useState<string>("");
  const [targetStrength, setTargetStrength] = useState<number[]>([60]);
  
  // Generate a random strong password targeting a specific strength percentage
  const generateSamplePassword = (targetPercent: number): string => {
    const lowercase = "abcdefghijklmnopqrstuvwxyz";
    const uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const numbers = "0123456789";
    const special = "!@#$%^&*()_+-=[]{}|;:,.<>?";
    
    // Calculate required length and character distribution based on target
    // Max score is 85: 30 (length) + 10 (lower) + 10 (upper) + 10 (numbers) + 15 (special) = 85
    // To reach higher percentages, we need to maximize all factors
    
    let targetLength = 8;
    // Always include letters and digits (API requirement)
    let lowerCount = 1;
    let upperCount = 1;
    let numberCount = 1;
    let specialCount = 1;
    
    // Adjust based on target percentage
    if (targetPercent >= 95) {
      // Very high: maximize everything
      targetLength = 24 + Math.floor(Math.random() * 3); // 24-26 chars
      lowerCount = 6 + Math.floor(Math.random() * 3);
      upperCount = 6 + Math.floor(Math.random() * 3);
      numberCount = 4 + Math.floor(Math.random() * 2);
      specialCount = 4 + Math.floor(Math.random() * 2);
    } else if (targetPercent >= 90) {
      targetLength = 22 + Math.floor(Math.random() * 3); // 22-24 chars
      lowerCount = 5 + Math.floor(Math.random() * 3);
      upperCount = 5 + Math.floor(Math.random() * 3);
      numberCount = 3 + Math.floor(Math.random() * 2);
      specialCount = 3 + Math.floor(Math.random() * 2);
    } else if (targetPercent >= 85) {
      targetLength = 20 + Math.floor(Math.random() * 3); // 20-22 chars
      lowerCount = 4 + Math.floor(Math.random() * 2);
      upperCount = 4 + Math.floor(Math.random() * 2);
      numberCount = 3 + Math.floor(Math.random() * 2);
      specialCount = 3 + Math.floor(Math.random() * 2);
    } else if (targetPercent >= 80) {
      targetLength = 18 + Math.floor(Math.random() * 3); // 18-20 chars
      lowerCount = 3 + Math.floor(Math.random() * 2);
      upperCount = 3 + Math.floor(Math.random() * 2);
      numberCount = 2 + Math.floor(Math.random() * 2);
      specialCount = 2 + Math.floor(Math.random() * 2);
    } else if (targetPercent >= 75) {
      targetLength = 16 + Math.floor(Math.random() * 3); // 16-18 chars
      lowerCount = 3 + Math.floor(Math.random() * 2);
      upperCount = 3 + Math.floor(Math.random() * 2);
      numberCount = 2 + Math.floor(Math.random() * 2);
      specialCount = 2;
    } else if (targetPercent >= 70) {
      targetLength = 14 + Math.floor(Math.random() * 3); // 14-16 chars
      lowerCount = 2 + Math.floor(Math.random() * 2);
      upperCount = 2 + Math.floor(Math.random() * 2);
      numberCount = 2;
      specialCount = 2;
    } else {
      // 65-69%
      targetLength = 12 + Math.floor(Math.random() * 3); // 12-14 chars
      lowerCount = 2;
      upperCount = 2;
      numberCount = 2;
      specialCount = 1;
    }
    
    // Build password with required character types
    let password = "";
    
    // Add required characters
    for (let i = 0; i < lowerCount; i++) {
      password += lowercase[Math.floor(Math.random() * lowercase.length)];
    }
    for (let i = 0; i < upperCount; i++) {
      password += uppercase[Math.floor(Math.random() * uppercase.length)];
    }
    for (let i = 0; i < numberCount; i++) {
      password += numbers[Math.floor(Math.random() * numbers.length)];
    }
    for (let i = 0; i < specialCount; i++) {
      password += special[Math.floor(Math.random() * special.length)];
    }
    
    // Fill remaining length with random characters from all types
    const allChars = lowercase + uppercase + numbers + special;
    const remaining = targetLength - password.length;
    for (let i = 0; i < remaining; i++) {
      password += allChars[Math.floor(Math.random() * allChars.length)];
    }
    
    // Shuffle the password to avoid predictable pattern
    return password.split('').sort(() => Math.random() - 0.5).join('');
  };
  
  // Generate sample password based on target strength
  useEffect(() => {
    const target = targetStrength[0];
    let generated = "";
    let strength;
    let attempts = 0;
    const maxAttempts = 100;
    
    // Keep generating until we get one that matches or exceeds target strength
    // AND meets API requirements (has a letter and a digit)
    // Allow some tolerance (within 5% of target)
    const tolerance = 5;
    do {
      generated = generateSamplePassword(target);
      strength = calculatePasswordStrength(generated);
      attempts++;
      // Accept if it meets API requirements AND (within tolerance or above target)
      if (strength.meetsApiRequirements && (strength.percentage >= target - tolerance || strength.percentage >= target)) {
        break;
      }
      // If we've tried many times, accept anything that meets API requirements and is above 60%
      if (attempts > maxAttempts && strength.meetsApiRequirements && strength.percentage >= 60) {
        break;
      }
    } while (attempts < maxAttempts * 2);
    
    setSamplePassword(generated);
  }, [targetStrength]);
  
  const samplePasswordStrength = useMemo(() => {
    if (!samplePassword) return null;
    return calculatePasswordStrength(samplePassword);
  }, [samplePassword]);

  const form = useForm<ChangePasswordFormData>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: {
      currentPassword: "",
      newPassword: "",
      confirmPassword: "",
    },
  });

  const newPasswordValue = form.watch("newPassword");
  const confirmPasswordValue = form.watch("confirmPassword");

  // Calculate password strength for new password
  const newPasswordStrength = useMemo(() => {
    return calculatePasswordStrength(newPasswordValue);
  }, [newPasswordValue]);

  // Calculate password strength for confirm password (when it matches)
  const confirmPasswordStrength = useMemo(() => {
    if (!confirmPasswordValue || confirmPasswordValue !== newPasswordValue) {
      return null;
    }
    return calculatePasswordStrength(confirmPasswordValue);
  }, [confirmPasswordValue, newPasswordValue]);

  const onSubmit = async (data: ChangePasswordFormData) => {
    // Prevent double submission
    if (isSubmitting) {
      return;
    }

    // Reject if user tries to use the sample password
    if (samplePassword && data.newPassword === samplePassword) {
      toast.error("You cannot use the sample password. Please create your own unique password.");
      form.setError("newPassword", {
        type: "manual",
        message: "Sample password cannot be used. Create your own unique password.",
      });
      return;
    }

    // Double-check password strength and API requirements
    const strength = calculatePasswordStrength(data.newPassword);
    if (!strength.meetsApiRequirements) {
      toast.error("Password must contain at least one letter and one digit.");
      form.setError("newPassword", {
        type: "manual",
        message: "Password must contain at least one letter and one digit.",
      });
      return;
    }

    setIsSubmitting(true);
    try {
      // Get username from URL params (safer than sessionStorage)
      const usernameParam = searchParams.get("username");
      const usernameOrEmail = usernameParam ? decodeURIComponent(usernameParam) : "";

      const result = await changePassword({
        currentPassword: data.currentPassword,
        newPassword: data.newPassword,
      });

      if (!result.success) {
        throw new Error(result.error || "Failed to change password");
      }

      toast.success("Password changed successfully! Redirecting...");
      form.reset();
      
      // Redirect to overview after successful password change
      setTimeout(() => {
        router.push("/overview");
      }, 1000);
    } catch (error: any) {
      toast.error(error.message || "Failed to change password");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="w-full max-w-md mx-auto space-y-6">
      <Card className="shadow-lg border-0 bg-card">
        <CardHeader className="space-y-1 pb-4">
          <CardTitle className="text-2xl font-bold text-center">
            Change Password
          </CardTitle>
          <CardDescription className="text-center text-muted-foreground">
            This is your first login. Please change your password to continue.
          </CardDescription>
        </CardHeader>
        
        {/* Sample Strong Password Example - Moved here */}
        {samplePassword && samplePasswordStrength && (
          <div className="px-6 pb-2">
            <div className="rounded-lg border border-border bg-muted/50 p-3 space-y-3">
              <div className="flex items-start gap-2">
                <Info className="h-4 w-4 text-muted-foreground mt-0.5 flex-shrink-0" />
                <div className="flex-1 space-y-3">
                  <div className="flex items-center justify-between">
                    <p className="text-xs font-medium text-foreground">
                      Sample Strong Password (Reference Only)
                    </p>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="h-6 px-2 text-xs"
                      onClick={() => setShowSamplePassword(!showSamplePassword)}
                    >
                      {showSamplePassword ? "Hide" : "Show"}
                    </Button>
                  </div>
                  {showSamplePassword && (
                    <div className="space-y-3">
                      {/* Strength Slider */}
                      <div className="space-y-2">
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-muted-foreground font-medium">Adjust Strength:</span>
                          <span className={getPasswordStrengthTextColor(samplePasswordStrength.level)}>
                            {targetStrength[0]}%
                          </span>
                        </div>
                        <Slider
                          value={targetStrength}
                          onValueChange={setTargetStrength}
                          min={60}
                          max={100}
                          step={1}
                          className="w-full"
                        />
                        {/* <div className="flex items-center justify-between text-xs text-blue-600">
                          <span>60% (Minimum)</span>
                          <span>100% (Maximum)</span>
                        </div> */}
                      </div>
                      
                      {/* Sample Password Display */}
                      <div className="space-y-2">
                        <div className="flex items-center gap-2">
                          <code className="flex-1 px-2 py-1.5 bg-background border border-border rounded text-xs font-mono text-foreground break-all">
                            {samplePassword}
                          </code>
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            className="h-8 px-2 flex-shrink-0"
                            onClick={async () => {
                              try {
                                await navigator.clipboard.writeText(samplePassword);
                                toast.info("Sample password copied for reference only. You cannot use this password.");
                              } catch {
                                toast.error("Failed to copy");
                              }
                            }}
                          >
                            <Copy className="h-3.5 w-3.5" />
                          </Button>
                        </div>
                        {/* <div className="flex items-center justify-between text-xs">
                          <span className="text-blue-700">Current Strength:</span>
                          <span className={getPasswordStrengthTextColor(samplePasswordStrength.level)}>
                            {getPasswordStrengthLabel(samplePasswordStrength.level)} ({samplePasswordStrength.percentage}%)
                          </span>
                        </div> */}
                        {/* <div className="w-full bg-blue-200 rounded-full h-1.5 overflow-hidden">
                          <div
                            className={`h-full transition-all duration-300 ${getPasswordStrengthColor(samplePasswordStrength.level)} ${
                              samplePasswordStrength.meetsMinimum ? "animate-pulse" : ""
                            }`}
                            style={{ width: `${samplePasswordStrength.percentage}%` }}
                          />
                        </div> */}
                        <p className="text-xs text-yellow-600 dark:text-yellow-500 font-medium">
                          ⚠️ This is a reference example only. You cannot use this password - create your own unique password following similar patterns.
                        </p>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>
        )}
        
        <CardContent className="space-y-4">
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
              <FormField
                control={form.control}
                name="currentPassword"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Current Password</FormLabel>
                    <FormControl>
                      <div className="relative">
                        <Input
                          {...field}
                          type={showCurrentPassword ? "text" : "password"}
                          placeholder="Enter current password"
                          className="pr-10"
                          disabled={isSubmitting}
                        />
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="absolute right-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                          onClick={() => setShowCurrentPassword(!showCurrentPassword)}
                          disabled={isSubmitting}
                        >
                          {showCurrentPassword ? (
                            <EyeOff className="h-4 w-4" />
                          ) : (
                            <Eye className="h-4 w-4" />
                          )}
                        </Button>
                      </div>
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              
              <FormField
                control={form.control}
                name="newPassword"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>New Password</FormLabel>
                    <FormControl>
                      <div className="space-y-2">
                        <div className="relative">
                          <Input
                            {...field}
                            type={showNewPassword ? "text" : "password"}
                            placeholder="Enter new password"
                            className="pr-10"
                            disabled={isSubmitting}
                            onChange={(e) => {
                              field.onChange(e);
                              form.trigger("newPassword");
                            }}
                          />
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            className="absolute right-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                            onClick={() => setShowNewPassword(!showNewPassword)}
                            disabled={isSubmitting}
                          >
                            {showNewPassword ? (
                              <EyeOff className="h-4 w-4" />
                            ) : (
                              <Eye className="h-4 w-4" />
                            )}
                          </Button>
                        </div>
                        
                        {/* Password Strength Indicator */}
                        {newPasswordValue && (
                          <div className="space-y-2">
                            <div className="flex items-center justify-between text-xs">
                              <span className="text-muted-foreground">Password Strength</span>
                              <span className={getPasswordStrengthTextColor(newPasswordStrength.level)}>
                                {getPasswordStrengthLabel(newPasswordStrength.level)}
                              </span>
                            </div>
                            <div className="w-full bg-gray-200 rounded-full h-2 overflow-hidden">
                              <div
                                className={`h-full transition-all duration-500 ease-out ${getPasswordStrengthColor(newPasswordStrength.level)} ${
                                  newPasswordStrength.meetsMinimum ? "animate-pulse" : ""
                                }`}
                                style={{ width: `${newPasswordStrength.percentage}%` }}
                              />
                            </div>
                            {newPasswordStrength.meetsApiRequirements && newPasswordStrength.meetsRecommended && (
                              <div className="flex items-center gap-1.5 text-xs text-green-600 animate-in fade-in slide-in-from-top-1">
                                <CheckCircle2 className="h-3.5 w-3.5" />
                                <span>Password meets requirements (65%+ recommended)</span>
                              </div>
                            )}
                            {newPasswordStrength.meetsApiRequirements && newPasswordStrength.meetsMinimum && !newPasswordStrength.meetsRecommended && (
                              <div className="flex items-center gap-1.5 text-xs text-yellow-600 animate-in fade-in slide-in-from-top-1">
                                <AlertCircle className="h-3.5 w-3.5" />
                                <span>60%+ strength achieved. Consider 65%+ for better security.</span>
                              </div>
                            )}
                            {!newPasswordStrength.meetsApiRequirements && newPasswordValue.length > 0 && (
                              <div className="space-y-1">
                                <div className="flex items-center gap-1.5 text-xs text-red-600">
                                  <AlertCircle className="h-3.5 w-3.5" />
                                  <span>Password must contain a letter and a number</span>
                                </div>
                                {newPasswordStrength.feedback.length > 0 && (
                                  <ul className="text-xs text-muted-foreground ml-5 list-disc space-y-0.5">
                                    {newPasswordStrength.feedback.filter(f => 
                                      f.includes('letter') ||
                                      f.includes('digit')
                                    ).map((tip, idx) => (
                                      <li key={idx}>{tip}</li>
                                    ))}
                                  </ul>
                                )}
                              </div>
                            )}
                            {newPasswordStrength.meetsApiRequirements && !newPasswordStrength.meetsMinimum && newPasswordValue.length > 0 && (
                              <div className="space-y-1">
                                <div className="flex items-center gap-1.5 text-xs text-orange-600">
                                  <AlertCircle className="h-3.5 w-3.5" />
                                  <span>Recommended strength is 60%+ (65%+ recommended)</span>
                                </div>
                                {newPasswordStrength.feedback.length > 0 && (
                                  <ul className="text-xs text-muted-foreground ml-5 list-disc space-y-0.5">
                                    {newPasswordStrength.feedback.filter(f => 
                                      !f.includes('lowercase') && 
                                      !f.includes('uppercase') && 
                                      !f.includes('digit') && 
                                      !f.includes('special')
                                    ).slice(0, 3).map((tip, idx) => (
                                      <li key={idx}>{tip}</li>
                                    ))}
                                  </ul>
                                )}
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              
              <FormField
                control={form.control}
                name="confirmPassword"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Confirm New Password</FormLabel>
                    <FormControl>
                      <div className="space-y-2">
                        <div className="relative">
                          <Input
                            {...field}
                            type={showConfirmPassword ? "text" : "password"}
                            placeholder="Confirm new password"
                            className="pr-10"
                            disabled={isSubmitting}
                            onPaste={(e) => {
                              e.preventDefault();
                              toast.info("Please type your password to confirm it. Pasting is not allowed.");
                            }}
                            onChange={(e) => {
                              field.onChange(e);
                              form.trigger("confirmPassword");
                            }}
                          />
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            className="absolute right-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                            onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                            disabled={isSubmitting}
                          >
                            {showConfirmPassword ? (
                              <EyeOff className="h-4 w-4" />
                            ) : (
                              <Eye className="h-4 w-4" />
                            )}
                          </Button>
                        </div>
                        
                        {/* Show strength when passwords match */}
                        {confirmPasswordValue && confirmPasswordValue === newPasswordValue && confirmPasswordStrength && (
                          <div className="space-y-2">
                            <div className="flex items-center justify-between text-xs">
                              <span className="text-muted-foreground">Confirmed Password Strength</span>
                              <span className={getPasswordStrengthTextColor(confirmPasswordStrength.level)}>
                                {getPasswordStrengthLabel(confirmPasswordStrength.level)}
                              </span>
                            </div>
                            <div className="w-full bg-gray-200 rounded-full h-2 overflow-hidden">
                              <div
                                className={`h-full transition-all duration-500 ease-out ${getPasswordStrengthColor(confirmPasswordStrength.level)} ${
                                  confirmPasswordStrength.meetsMinimum ? "animate-pulse" : ""
                                }`}
                                style={{ width: `${confirmPasswordStrength.percentage}%` }}
                              />
                            </div>
                            {confirmPasswordStrength.meetsMinimum && (
                              <div className="flex items-center gap-1.5 text-xs text-green-600 animate-in fade-in slide-in-from-top-1">
                                <CheckCircle2 className="h-3.5 w-3.5" />
                                <span>Password confirmed and meets strength requirements!</span>
                              </div>
                            )}
                          </div>
                        )}
                        
                        {/* Show match indicator */}
                        {confirmPasswordValue && confirmPasswordValue !== newPasswordValue && (
                          <div className="flex items-center gap-1.5 text-xs text-red-600">
                            <AlertCircle className="h-3.5 w-3.5" />
                            <span>Passwords do not match</span>
                          </div>
                        )}
                      </div>
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              
              <Button
                type="submit"
                className="w-full h-10 font-medium"
                disabled={isSubmitting || newPasswordValue.length < 6 || !newPasswordStrength.meetsApiRequirements}
              >
                {isSubmitting ? "Changing Password..." : "Change Password"}
              </Button>
            </form>
          </Form>
        </CardContent>
      </Card>
    </div>
  );
}
