"use client";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Lock, Home, ArrowLeft } from "lucide-react";
import Link from "next/link";

// Force dynamic rendering to avoid prerender serialization of event handlers
export const dynamic = "force-dynamic";

export default function ForbiddenPage() {
  return (
    <div className="flex items-center justify-center min-h-screen bg-gradient-to-br from-background to-muted/40 dark:from-[#18181b] dark:to-[#23232a] p-4">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center space-y-4">
          <div className="mx-auto w-16 h-16 rounded-full bg-orange-500/10 flex items-center justify-center">
            <Lock className="w-8 h-8 text-orange-500" />
          </div>
          <CardTitle className="text-2xl">403 - Access Forbidden</CardTitle>
          <CardDescription>
            You don't have permission to access this resource. Please contact your administrator if you believe this is an error.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="text-sm text-muted-foreground">
            <p className="mb-2">Possible reasons:</p>
            <ul className="list-disc list-inside space-y-1 ml-2">
              <li>Your account doesn't have the required permissions</li>
              <li>This resource is restricted to specific roles</li>
              <li>Your access may have been revoked</li>
            </ul>
          </div>
        </CardContent>
        <CardFooter className="flex flex-col gap-2">
          <Button asChild className="w-full">
            <Link href="/overview">
              <Home className="w-4 h-4 mr-2" />
              Go to Dashboard
            </Link>
          </Button>
          <Button asChild variant="outline" className="w-full">
            <Link href="/" replace>
              <ArrowLeft className="w-4 h-4 mr-2" />
              Go Back
            </Link>
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}

