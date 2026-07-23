import LoginForm from "@/components/auth/login-form";
import Link from "next/link";
import { LanguageSwitcher } from "@/components/i18n/language-switcher";

export default function LoginPage() {
  return (
    <div className="fixed inset-0 z-50 overflow-y-auto bg-muted/40">
      <div className="absolute inset-x-0 top-0 z-10 flex items-center justify-between px-4 py-4 md:px-8">
        <Link href="/" className="text-lg font-bold text-primary">
          SimbaFlow
        </Link>
        <LanguageSwitcher />
      </div>
      <div className="flex min-h-full items-center justify-center px-4 py-20">
        <div className="w-full max-w-md space-y-6">
          <LoginForm />
        </div>
      </div>
    </div>
  );
}
